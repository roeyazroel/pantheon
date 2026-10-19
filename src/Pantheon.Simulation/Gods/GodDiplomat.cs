using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// The diplomatic half of a god's AI (spec §47–49, §113, ADR-012). It weighs offering an alliance,
/// declaring war, breaking an alliance and asking for peace with every other god, and answers the
/// proposals waiting for it. Every weight comes from how the god sees the other (trust, rivalry,
/// fear, respect, gratitude, suspicion and the grudge of what it remembers), the balance of their
/// followings and the god's personality. It acts through the diplomacy authority.
/// </summary>
public sealed class GodDiplomat
{
    private const float Threshold = 0.25f;
    private const float WarWearinessSeconds = 300f;

    // A third god leading by this share of all followers starts to look like a common threat.
    private const float ThreatShare = 0.3f;

    // Allies this close to winning together weigh loyalty against personal ascendancy (spec §54).
    private const float EndgameMargin = 0.15f;

    private readonly DiplomacyRules _rules;
    private readonly DivineRules _divine;
    private readonly DiplomacyAuthority _authority;
    private readonly PlanningRules _planning;

    /// <summary>Creates the diplomat.</summary>
    /// <param name="rules">Diplomacy rules.</param>
    /// <param name="divine">Divine rules, for the victory share.</param>
    /// <param name="authority">The diplomacy authority it acts through.</param>
    /// <param name="planning">How the gods' goals lean their diplomacy (ADR-036); none when <c>null</c>.</param>
    public GodDiplomat(DiplomacyRules rules, DivineRules divine, DiplomacyAuthority authority, PlanningRules? planning = null)
    {
        _planning = planning ?? PlanningRules.None;
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _divine = divine;
        _authority = authority;
    }

    /// <summary>The diplomacy rules.</summary>
    public DiplomacyRules Rules => _rules;

    /// <summary>Every move the god could make now, best first; including answers to proposals waiting for it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <returns>The candidates.</returns>
    public List<DiplomacyCandidate> Evaluate(WorldState world, GodState god, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        var candidates = new List<DiplomacyCandidate>();
        foreach (var other in world.Gods)
        {
            if (other.Id != god.Id && Acquaintance.Knows(_rules.Acquaintance, world, god.Id, other.Id))
            {
                AddMoves(candidates, world, god, other, profile);
            }
        }

        candidates.Sort(static (a, b) => b.Utility.CompareTo(a.Utility));
        return candidates;
    }

    /// <summary>Answers every proposal waiting for the god, then makes its best move if it is worth making.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its personality's profile.</param>
    /// <param name="events">Receives the events of what it does.</param>
    public void Act(WorldState world, GodState god, GodProfile profile, List<WorldEvent> events)
    {
        var candidates = Evaluate(world, god, profile);
        foreach (var answer in candidates.FindAll(c => c.Kind is DiplomaticCommandKind.Accept or DiplomaticCommandKind.Decline))
        {
            _authority.Execute(world, new DiplomaticCommand(god.Id, answer.OtherGodId, answer.Kind), god.Id, events);
        }

        foreach (var move in candidates)
        {
            if (move.Kind is DiplomaticCommandKind.Accept or DiplomaticCommandKind.Decline || move.Utility < Threshold)
            {
                continue;
            }

            var aim = move.Kind == DiplomaticCommandKind.DeclareWar ? Conflict.WarAims.For(world, god, move.OtherGodId, profile) : null;
            if (_authority.Execute(world, new DiplomaticCommand(god.Id, move.OtherGodId, move.Kind, Aim: aim), god.Id, events) == DiplomacyRejection.None)
            {
                return;
            }
        }
    }

    /// <summary>The moves towards one other god that fit their status.</summary>
    private void AddMoves(List<DiplomacyCandidate> candidates, WorldState world, GodState god, GodState other, GodProfile profile)
    {
        var view = DiplomacyModel.FindRelation(world, god.Id, other.Id) ?? DiplomacyModel.NewRelation(_rules, god.Id, other.Id);
        var mine = GodStanding.CountFollowers(world, god.Id);
        var theirs = GodStanding.CountFollowers(world, other.Id);
        var advantage = (mine - theirs) / (float)Math.Max(1, mine + theirs);
        var grudge = DiplomacyModel.Grudge(view) * profile.Grudge;
        var goodwill = DiplomacyModel.Goodwill(view);
        var theyDominate = other.DominantSinceTick is not null;
        var stance = DiplomacyModel.FindStance(world, god.Id, other.Id);
        var status = stance?.Status ?? DiplomaticStatus.Neutral;
        var (threat, leader) = ThirdPartyThreat(world, god.Id, other.Id);
        var otherLeads = Leading(world, god.Id, other.Id);
        var why = Format(
            "trust {0:0.00}, rivalry {1:0.00}, fear {2:0.00}, grudge {3:0.00}, goodwill {4:0.00}, advantage {5:+0.00;-0.00;0.00}{6}{7}{8}",
            view.Trust,
            view.Rivalry,
            view.Fear,
            grudge,
            goodwill,
            advantage,
            theyDominate ? ", they dominate" : string.Empty,
            threat > 0f ? Format(", against {0} {1:0.00}", leader, threat) : string.Empty,
            otherLeads > 0f ? Format(", they lead {0:0.00}", otherLeads) : string.Empty);

        // Alliance appeals to the cooperative, the grateful, the afraid, the losing, and to those a
        // third god threatens (a coalition against the leader, ADR-015).
        var alliance = (profile.Cooperation * (view.Trust + (0.5f * view.Respect) + view.Gratitude + (0.5f * goodwill) + (0.8f * threat)))
            + (0.6f * view.Fear) + (0.5f * Math.Max(0f, -advantage)) - (0.6f * view.Rivalry) - view.Suspicion - grudge - 0.3f;

        // War-weariness wears on a god that is not winning, and a third god's lead makes a war between
        // two others a gift to it (the balance of power, ADR-015).
        var weariness = WarSeconds(world, stance) / WarWearinessSeconds * (1f - Math.Max(0f, advantage));
        var peace = (profile.Cooperation * (weariness + Math.Max(0f, -advantage) + threat)) + view.Fear - (0.5f * grudge) - 0.3f
            + GoalFocus.Diplomacy(_planning, god, other.Id, DiplomaticCommandKind.RequestPeace);
        alliance += GoalFocus.Diplomacy(_planning, god, other.Id, DiplomaticCommandKind.OfferAlliance);
        if (DiplomacyAuthority.FindProposal(world, other.Id, god.Id) is { } proposal)
        {
            var accept = proposal.Kind switch
            {
                ProposalKind.Alliance => alliance + 0.2f,
                ProposalKind.Peace => peace + 0.3f,
                ProposalKind.Treaty or ProposalKind.Request => TreatyAdvisor.AnswerUtility(_rules, world, god, proposal, profile, view),
                ProposalKind.Surrender => Conflict.WarAims.SurrenderUtility(_rules.Conflict, world, god.Id, other.Id, view, profile),
            };
            candidates.Add(new DiplomacyCandidate(accept > 0f ? DiplomaticCommandKind.Accept : DiplomaticCommandKind.Decline, other.Id, Math.Abs(accept), why));
        }

        switch (status)
        {
            case DiplomaticStatus.Alliance:
                // An ally dominating with this god is its path to a joint victory, not a threat.
                var aloneDominant = theyDominate && !VictorySystem.StandingOf(_divine, world, other.Id).Allies.Contains(god.Id);
                var betray = (profile.Treachery * (Math.Max(0f, advantage) + view.Rivalry)) + (aloneDominant ? 0.8f : 0f) + Endgame(world, god.Id, other.Id, profile) - view.Trust - goodwill - 0.4f
                    + GoalFocus.Diplomacy(_planning, god, other.Id, DiplomaticCommandKind.BreakAlliance);
                candidates.Add(new DiplomacyCandidate(DiplomaticCommandKind.BreakAlliance, other.Id, betray, why));
                break;
            case DiplomaticStatus.War:
                candidates.Add(new DiplomacyCandidate(DiplomaticCommandKind.RequestPeace, other.Id, peace, why));
                if (_authority.Check(world, new DiplomaticCommand(god.Id, other.Id, DiplomaticCommandKind.DemandSurrender), god.Id) == DiplomacyRejection.None)
                {
                    // A war won is ended on the victor's terms (spec §44).
                    candidates.Add(new DiplomacyCandidate(DiplomaticCommandKind.DemandSurrender, other.Id, 0.9f, why + ", goal achieved"));
                }

                break;
            case DiplomaticStatus.Friendly:
            case DiplomaticStatus.Neutral:
            case DiplomaticStatus.Hostile:
                candidates.Add(new DiplomacyCandidate(DiplomaticCommandKind.OfferAlliance, other.Id, alliance, why));
                // A fresh peace or past help holds a god back until the memory fades.
                var war = (profile.Aggression * (view.Rivalry + grudge + Math.Max(0f, advantage) + otherLeads + (status == DiplomaticStatus.Hostile ? 0.3f : 0f)))
                    + (theyDominate ? 0.6f : 0f) + (Politics.CouncilModel.IsCondemned(world, other.Id) ? 0.3f : 0f)
                    - (0.5f * view.Trust) - view.Fear - goodwill - (0.3f * profile.Cooperation) - (0.8f * threat) - 0.4f
                    + GoalFocus.Diplomacy(_planning, god, other.Id, DiplomaticCommandKind.DeclareWar);
                candidates.Add(new DiplomacyCandidate(DiplomaticCommandKind.DeclareWar, other.Id, war, why));
                break;
        }
    }

    /// <summary>
    /// How much a third god (neither this one nor the other, nor an ally of either) threatens both: its share of all
    /// followers past <see cref="ThreatShare"/>, scaled to 1 at the victory share. A dominant third
    /// god is a full threat.
    /// </summary>
    private (float Threat, string Leader) ThirdPartyThreat(WorldState world, int godId, int otherId)
    {
        var threat = 0f;
        var leader = string.Empty;
        foreach (var third in world.Gods)
        {
            if (third.Id == godId || third.Id == otherId || DiplomacyModel.AreAllied(world, godId, third.Id) || DiplomacyModel.AreAllied(world, otherId, third.Id))
            {
                continue;
            }

            var share = VictorySystem.ShareOf(world, third.Id);
            var level = third.DominantSinceTick is not null ? 1f : SimMath.Clamp01((share - ThreatShare) / (_divine.VictoryShare - ThreatShare));
            if (level > threat)
            {
                threat = level;
                leader = third.Name;
            }
        }

        return (threat, leader);
    }

    /// <summary>How far the other god leads the world: its share past <see cref="ThreatShare"/> when it holds more than this god (0..1).</summary>
    private float Leading(WorldState world, int godId, int otherId)
    {
        var theirs = VictorySystem.ShareOf(world, otherId);
        return theirs <= VictorySystem.ShareOf(world, godId) ? 0f : SimMath.Clamp01((theirs - ThreatShare) / (_divine.VictoryShare - ThreatShare));
    }

    /// <summary>
    /// The pull of personal ascendancy near a joint victory (spec §54): when the alliance is close to
    /// the victory share, a treacherous god that holds more than its partner is tempted to go alone,
    /// and a loyal one to stay and win together.
    /// </summary>
    private float Endgame(WorldState world, int godId, int otherId, GodProfile profile)
    {
        var (together, _) = VictorySystem.StandingOf(_divine, world, godId);
        if (!_divine.PantheonVictory || together < _divine.VictoryShare - EndgameMargin)
        {
            return 0f;
        }

        var lead = VictorySystem.ShareOf(world, godId) - VictorySystem.ShareOf(world, otherId);
        return (profile.Treachery * 3f * Math.Max(0f, lead)) - ((1f - profile.Treachery) * 0.5f);
    }

    /// <summary>Simulated seconds the pair has been at war; 0 when not at war.</summary>
    private static float WarSeconds(WorldState world, GodStance? stance) =>
        stance is { Status: DiplomaticStatus.War } ? (world.Tick - stance.SinceTick) / (float)SimulationTime.TicksPerSecond : 0f;

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
