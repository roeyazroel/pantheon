using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// How an AI god takes part in the Divine Council (spec §42, §47–49, ADR-025): it votes on the
/// motion before the council by how it sees the god or place the motion names and by its
/// personality, and keeps its promises of backing; it tables a motion when it has a grievance, a
/// rival dominates, disaster struck its people or a rival works cataclysms.
/// </summary>
public sealed class CouncilAdvisor
{
    // A vote is cast for or against past this utility, else the god abstains.
    private const float Conviction = 0.1f;

    // A motion is tabled at this utility.
    private const float TableThreshold = 0.4f;

    private readonly PoliticsRules _rules;
    private readonly CouncilAuthority _authority;

    /// <summary>Creates the advisor.</summary>
    /// <param name="rules">Politics rules.</param>
    /// <param name="authority">The council authority it acts through.</param>
    public CouncilAdvisor(PoliticsRules rules, CouncilAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(authority);
        _rules = rules;
        _authority = authority;
    }

    /// <summary>Votes on the motion in session, or tables a motion when the council is not sitting.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="profile">Its profile.</param>
    public void Act(WorldState world, GodState god, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(profile);
        var council = world.Diplomacy.Council;
        if (council.Session is { } session)
        {
            var choice = Vote(world, god, session, profile);
            if (session.Votes.Find(v => v.GodId == god.Id)?.Choice != choice)
            {
                _authority.Execute(world, new CouncilCommand(god.Id, null, choice), god.Id);
            }

            return;
        }

        if (!council.Tabled.Exists(m => m.ProposerGodId == god.Id) && Motion(world, god, profile) is { } best && best.Utility >= TableThreshold)
        {
            _authority.Execute(world, new CouncilCommand(god.Id, best.Motion, null), god.Id);
        }
    }

    /// <summary>
    /// How a god votes: as it promised a creditor that has voted; for its own motion; against a
    /// motion aimed at it; otherwise by the motion's worth to it.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="session">The session.</param>
    /// <param name="profile">Its profile.</param>
    /// <returns>Its vote.</returns>
    public static VoteChoice Vote(WorldState world, GodState god, CouncilSession session, GodProfile profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(profile);
        foreach (var promise in world.Diplomacy.Obligations)
        {
            if (promise.Kind == ObligationKind.Back && promise.DebtorGodId == god.Id
                && session.Votes.Find(v => v.GodId == promise.CreditorGodId)?.Choice is { } owed and not VoteChoice.Abstain)
            {
                return owed;
            }
        }

        var motion = session.Motion;
        if (motion.ProposerGodId == god.Id)
        {
            return VoteChoice.Yes;
        }

        if (motion.TargetGodId == god.Id)
        {
            return VoteChoice.No;
        }

        var worth = Worth(world, god, motion, profile);
        return worth > Conviction ? VoteChoice.Yes : worth < -Conviction ? VoteChoice.No : VoteChoice.Abstain;
    }

    /// <summary>What a motion is worth to a god, from about -1 to 1.</summary>
    private static float Worth(WorldState world, GodState god, CouncilMotion motion, GodProfile profile)
    {
        return motion.Kind switch
        {
            CouncilMotionKind.Condemn => motion.TargetGodId is { } target ? Enmity(world, god.Id, target) + Dominance(world, target) - (DiplomacyModel.AreAllied(world, god.Id, target) ? 1f : 0f) - 0.7f : 0f,
            CouncilMotionKind.PantheonWar => motion.TargetGodId is { } enemy
                ? (profile.Aggression * Enmity(world, god.Id, enemy)) + (1.2f * Dominance(world, enemy)) - (DiplomacyModel.AreAllied(world, god.Id, enemy) ? 1.2f : 0f) - (0.5f * profile.Cooperation) - 0.5f
                : 0f,
            CouncilMotionKind.ProtectSacredLand => (0.5f * profile.Cooperation) + (world.HolySites.Find(s => s.Id == motion.SiteId)?.KeeperGodId == god.Id ? 0.5f : 0f) - (0.5f * profile.Intimidate) - 0.1f,
            CouncilMotionKind.ProhibitPractice => (0.5f * profile.Cooperation) - (motion.Practice == Practice.Cataclysms ? profile.Aggression : profile.Intimidate) - 0.1f,
            CouncilMotionKind.CoordinateDisasterResponse => (0.6f * profile.Cooperation) + (0.3f * profile.Protect) - 0.2f,
            CouncilMotionKind.NeutralTerritory => world.Settlements.Find(s => s.Id == motion.SettlementId) is { } place && TreatyModel.Leader(place) == god.Id
                ? 0.4f
                : (0.2f * profile.Cooperation) - (0.3f * profile.Convert) - 0.1f,
        };
    }

    /// <summary>The motion a god most wants to table, if any.</summary>
    private (CouncilMotion Motion, float Utility)? Motion(WorldState world, GodState god, GodProfile profile)
    {
        (CouncilMotion, float)? best = null;
        void Consider(CouncilMotion motion, float utility)
        {
            if (best is null || utility > best.Value.Item2)
            {
                best = (motion, utility);
            }
        }

        foreach (var other in world.Gods)
        {
            if (other.Id == god.Id || DiplomacyModel.AreAllied(world, god.Id, other.Id))
            {
                continue;
            }

            Consider(new CouncilMotion { Kind = CouncilMotionKind.Condemn, TargetGodId = other.Id, Secret = false }, (profile.Grudge * Enmity(world, god.Id, other.Id)) - 1f);
            if (other.DominantSinceTick is not null)
            {
                Consider(new CouncilMotion { Kind = CouncilMotionKind.PantheonWar, TargetGodId = other.Id, Secret = profile.Treachery > 0.5f }, 0.5f + profile.Aggression);
            }

            if (other.Conduct.Upheaval >= 3f && profile.Aggression < 0.5f)
            {
                Consider(new CouncilMotion { Kind = CouncilMotionKind.ProhibitPractice, Practice = Practice.Cataclysms, Secret = false }, 0.6f * profile.Cooperation + 0.2f);
            }
        }

        var since = world.Tick - (long)(_rules.StruckSeconds * SimulationTime.TicksPerSecond);
        if (!world.Diplomacy.Council.Decrees.Exists(d => d.Motion.Kind == CouncilMotionKind.CoordinateDisasterResponse)
            && world.Settlements.Exists(s => TreatyModel.Leader(s) == god.Id && CouncilModel.Struck(world, s, since)))
        {
            Consider(new CouncilMotion { Kind = CouncilMotionKind.CoordinateDisasterResponse, Secret = false }, (0.6f * profile.Cooperation) + (0.3f * profile.Protect));
        }

        foreach (var site in world.HolySites)
        {
            if (site.KeeperGodId == god.Id && site.ClaimantGodId is { } claimant && claimant != god.Id)
            {
                Consider(new CouncilMotion { Kind = CouncilMotionKind.ProtectSacredLand, SiteId = site.Id, Secret = false }, 0.3f + (0.4f * profile.Cooperation));
            }
        }

        return best;
    }

    /// <summary>How much a god hates another: rivalry, grudge and distrust.</summary>
    private static float Enmity(WorldState world, int godId, int otherId) =>
        DiplomacyModel.FindRelation(world, godId, otherId) is { } view ? view.Rivalry + DiplomacyModel.Grudge(view) + (0.5f - view.Trust) : 0f;

    /// <summary>How far a god leads past a third of all followers (0..1); 1 while it dominates.</summary>
    private static float Dominance(WorldState world, int godId) =>
        GodStanding.Find(world, godId)?.DominantSinceTick is not null ? 1f : SimMath.Clamp01((VictorySystem.ShareOf(world, godId) - 0.33f) * 3f);
}
