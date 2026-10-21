using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// The strategic utility AI of a god (spec §47–48, §87, ADR-011). Every evaluation it scores every
/// miracle it may cast on every settlement, sending its Creature and waiting, by the goals its
/// personality weighs and the goals it set itself in its last major plan (ADR-036); then it takes
/// the best action through the same authority as a player. It also teaches its Creature what its
/// personality values. Every score keeps its reasons.
/// </summary>
public sealed class GodAi
{
    private const float MaxPopulationFactor = 2f;
    private const float JudgeThreshold = 0.3f;

    // An AI god sends its Creature against an enemy Creature only when it is at least this sure of it (power and health).
    private const float MatchConfidence = 0.8f;

    // A Creature this close to a holy site is there already.
    private const float ClaimDistance = 100f;

    private readonly GodAiRules _rules;
    private readonly DivineRules _divine;
    private readonly MiracleBook _book;
    private readonly MiracleCaster _caster;
    private readonly CreatureAuthority? _creatures;
    private readonly CreatureRules? _creatureRules;
    private readonly GodDiplomat? _diplomat;
    private readonly DomainAuthority? _domains;
    private readonly MiracleValuer _valuer;
    private readonly GodPlanner _planner;

    /// <summary>Creates the AI over validated rules and the authorities it acts through.</summary>
    /// <param name="rules">AI rules.</param>
    /// <param name="divine">Divine rules.</param>
    /// <param name="book">The miracles.</param>
    /// <param name="caster">The miracle authority.</param>
    /// <param name="creatures">The Creature authority, or <c>null</c> when there are no Creatures.</param>
    /// <param name="creatureRules">Creature rules, for what an action's kindness is.</param>
    /// <param name="diplomat">The diplomatic half of the AI, or <c>null</c> for no diplomacy.</param>
    /// <param name="domains">The domain authority, through which it takes up domains (ADR-021); <c>null</c> where gods have none.</param>
    public GodAi(GodAiRules rules, DivineRules divine, MiracleBook book, MiracleCaster caster, CreatureAuthority? creatures, CreatureRules? creatureRules, GodDiplomat? diplomat = null, DomainAuthority? domains = null)
    {
        _diplomat = diplomat;
        _domains = domains;
        GodAiRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(caster);
        _rules = rules;
        _divine = divine;
        _book = book;
        _caster = caster;
        _creatures = creatures;
        _creatureRules = creatureRules;
        _valuer = new MiracleValuer(rules, caster);
        _planner = new GodPlanner(rules, diplomat?.Rules);
    }

    /// <summary>The god's major planning (ADR-036), for previews in the debug view.</summary>
    public GodPlanner Planner => _planner;

    /// <summary>The profile of a personality.</summary>
    /// <param name="personality">The personality.</param>
    /// <returns>Its profile, or <c>null</c> when the rules have none.</returns>
    public GodProfile? ProfileOf(GodPersonality personality) => _rules.Profiles is { } profiles ? FindProfile(profiles, personality) : null;

    /// <summary>A god's own profile (ADR-065): its personality's, leaning as its temper leans.</summary>
    /// <param name="god">The god.</param>
    /// <returns>Its profile, or <c>null</c> when the rules have none for its personality.</returns>
    public GodProfile? ProfileOf(GodState god)
    {
        ArgumentNullException.ThrowIfNull(god);
        return ProfileOf(god.Personality) is { } profile ? GodTempers.Apply(profile, god.Persona?.Temper, _rules.TemperReach) : null;
    }

    /// <summary>Every action the god could take now, best first, without jitter.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <returns>The candidates; always including waiting.</returns>
    public List<AiCandidate> Evaluate(WorldState world, GodState god)
    {
        ArgumentNullException.ThrowIfNull(god);
        return Evaluate(world, god, ProfileOf(god));
    }

    /// <summary>
    /// Every action a god could take now, best first, as a god of another profile would weigh them: the
    /// player's angel weighs a player's god's choices with a kind heart and a harsh one (ADR-074).
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <param name="profile">The profile that weighs them; none weighs nothing.</param>
    /// <returns>The candidates; always including waiting.</returns>
    public List<AiCandidate> Evaluate(WorldState world, GodState god, GodProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        var candidates = new List<AiCandidate> { new(AiActionKind.Wait, null, null, default, _rules.Patience, 0f, _rules.Patience, "save power") };
        if (profile is null)
        {
            return candidates;
        }

        // The god's influence at each settlement, once: a miracle outside it would be refused (spec §10),
        // so those checks are skipped. In a world of forty villages this is most of the AI's time (ADR-027).
        var cache = new InfluenceCache(_divine, world);
        var field = cache.For(god.Id);
        var influence = new float[world.Settlements.Count];
        for (var i = 0; i < influence.Length; i++)
        {
            influence[i] = field.At(world.Settlements[i].Position);
        }

        foreach (var miracle in _book.Miracles)
        {
            for (var i = 0; i < world.Settlements.Count; i++)
            {
                if (influence[i] >= miracle.InfluenceRequirement)
                {
                    AddCast(candidates, world, god, profile, miracle, world.Settlements[i], cache);
                }
            }
        }

        GoalFocus.AddSmites(_rules.Planning, world, god, profile, _book, _caster, cache, candidates);
        AddSendCreature(candidates, world, god, profile);
        GoalFocus.Apply(_rules.Planning, world, god, _book, candidates);
        candidates.Sort(static (a, b) => b.Utility.CompareTo(a.Utility));
        return candidates;
    }

    /// <summary>Evaluates, picks the best action with a little jitter and carries it out; then teaches the Creature.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The AI god.</param>
    /// <param name="random">The simulation's random stream.</param>
    /// <param name="events">Receives the events of what it does.</param>
    /// <returns>The action taken.</returns>
    public AiCandidate Act(WorldState world, GodState god, SimulationRandom random, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (ProfileOf(god) is { } planned)
        {
            _planner.PlanIfDue(world, god, planned, events);
        }

        var candidates = Evaluate(world, god);
        var best = candidates[0];
        var bestScore = float.NegativeInfinity;
        foreach (var candidate in candidates)
        {
            var score = candidate.Utility + (_rules.DecisionJitter * random.NextFloat());
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        Execute(world, god, best, events);
        TeachCreature(world, god, events);
        ChooseDomain(world, god, events);
        if (_diplomat is not null && ProfileOf(god) is { } profile)
        {
            _diplomat.Act(world, god, profile, events);
        }

        return best;
    }

    /// <summary>The diplomatic moves the god is weighing now, best first, for the debug view.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <returns>The candidates; none without diplomacy or a profile.</returns>
    public List<DiplomacyCandidate> EvaluateDiplomacy(WorldState world, GodState god) =>
        _diplomat is not null && ProfileOf(god) is { } profile ? _diplomat.Evaluate(world, god, profile) : [];

    /// <summary>Whether a treacherous god veils a harsh miracle on a people it is not openly at war with.</summary>
    private bool Veils(WorldState world, GodState god, AiCandidate candidate)
    {
        if (ProfileOf(god) is not { Treachery: >= 0.8f } || _book.Find(candidate.MiracleId!) is not { } miracle || !Culture.CultureModel.Harms(miracle)
            || candidate.SettlementId is not { } id || world.Settlements.Find(s => s.Id == id) is not { } settlement)
        {
            return false;
        }

        return Politics.TreatyModel.Leader(settlement) is { } leader && leader != god.Id && !Diplomacy.DiplomacyModel.AreAtWar(world, god.Id, leader)
            && Diplomacy.DiplomacyModel.FindRelation(world, god.Id, leader) is { Rivalry: >= 0.4f };
    }

    /// <summary>Carries out an action through the authorities, as the god itself (the host may act for any god).</summary>
    private void Execute(WorldState world, GodState god, AiCandidate candidate, List<WorldEvent> events)
    {
        switch (candidate.Kind)
        {
            case AiActionKind.CastMiracle:
                // A treacherous god strikes its rivals' people in secret when it can afford to (spec §45).
                var veiled = Veils(world, god, candidate);
                if (!veiled || !_caster.Cast(world, new CastMiracleRequest(god.Id, candidate.MiracleId!, candidate.Target, Veiled: true), god.Id, events).IsAccepted)
                {
                    _caster.Cast(world, new CastMiracleRequest(god.Id, candidate.MiracleId!, candidate.Target), god.Id, events);
                }

                break;
            case AiActionKind.SendCreature when _creatures is not null && god.CreatureId is { } creature && !OnAHunt(world, creature):
                _creatures.Execute(world, new CreatureCommand(god.Id, creature, CreatureCommandKind.GoTo, candidate.Target), god.Id, events);
                break;
            case AiActionKind.SetCreatureOn when _creatures is not null && god.CreatureId is { } hunter:
                _creatures.Execute(world, new CreatureCommand(god.Id, hunter, CreatureCommandKind.Fight, candidate.Target), god.Id, events);
                break;
            case AiActionKind.SendCreature:
            case AiActionKind.SetCreatureOn:
            case AiActionKind.Wait:
                break;
        }
    }

    /// <summary>The domain the god would take up next, if its rank leaves a slot free: its personality's next preference, or the first it lacks (ADR-021).</summary>
    /// <param name="god">The god.</param>
    /// <returns>The domain, or <c>null</c> when it has no free slot or nothing left to choose.</returns>
    public GodDomain? NextDomain(GodState god)
    {
        ArgumentNullException.ThrowIfNull(god);
        if (_domains is null || DomainModel.FreeSlots(_domains.Rules, god) <= 0)
        {
            return null;
        }

        foreach (var domain in ProfileOf(god)?.Domains ?? [])
        {
            if (!god.Domains.Contains(domain) && DomainModel.Find(_domains.Rules, domain) is not null)
            {
                return domain;
            }
        }

        foreach (var definition in _domains.Rules.Domains)
        {
            if (!god.Domains.Contains(definition.Domain))
            {
                return definition.Domain;
            }
        }

        return null;
    }

    /// <summary>Takes up the next domain when a slot is free.</summary>
    private void ChooseDomain(WorldState world, GodState god, List<WorldEvent> events)
    {
        if (_domains is not null && NextDomain(god) is { } domain)
        {
            _domains.Choose(world, god.Id, domain, god.Id, events);
        }
    }

    /// <summary>Rewards or punishes the Creature's latest unjudged deed by how its kindness suits the god's personality.</summary>
    private void TeachCreature(WorldState world, GodState god, List<WorldEvent> events)
    {
        if (_creatures is null || _creatureRules is null || god.CreatureId is not { } id || ProfileOf(god) is not { } profile
            || world.Creatures.Find(c => c.Id == id) is not { } creature || _creatures.FindJudged(world, creature) is not { Memory: { Feedback: CreatureFeedback.None } } judged)
        {
            return;
        }

        if (judged.Action == CreatureAction.GoTo)
        {
            return;
        }

        var approval = SocialApproval(world, god, judged.Action, judged.Memory.OtherCreatureId)
            ?? (CreatureModel.RuleFor(_creatureRules, judged.Action).Kindness * profile.CreatureKindness);
        var kind = approval >= JudgeThreshold ? CreatureCommandKind.Reward : approval <= -JudgeThreshold ? CreatureCommandKind.Punish : (CreatureCommandKind?)null;
        if (kind is { } command)
        {
            _creatures.Execute(world, new CreatureCommand(god.Id, id, command), god.Id, events);
        }
    }

    /// <summary>
    /// What any god makes of its Creature's dealings with another god's Creature (ADR-017): fighting
    /// an enemy's Creature and playing with an ally's are good; fighting an ally's and playing with an
    /// enemy's are not. With neither, the deed is judged by its kindness.
    /// </summary>
    private static float? SocialApproval(WorldState world, GodState god, CreatureAction action, int? otherCreatureId)
    {
        if (otherCreatureId is not { } id || world.Creatures.Find(c => c.Id == id) is not { } other)
        {
            return null;
        }

        var war = DiplomacyModel.AreAtWar(world, god.Id, other.GodId);
        var allied = DiplomacyModel.AreAllied(world, god.Id, other.GodId);
        return action switch
        {
            CreatureAction.Fight when war => 1f,
            CreatureAction.Fight when allied => -1f,
            CreatureAction.Befriend when war => -1f,
            CreatureAction.Befriend when allied => 1f,
            _ => null,
        };
    }

    /// <summary>Adds the cast of a miracle on a settlement, when it is allowed and worth something.</summary>
    private void AddCast(List<AiCandidate> candidates, WorldState world, GodState god, GodProfile profile, MiracleDefinition miracle, SettlementState settlement, InfluenceCache cache)
    {
        if (settlement.Population <= 0)
        {
            return;
        }

        var check = _caster.Check(world, new CastMiracleRequest(god.Id, miracle.Id, settlement.Position), god.Id, cache);
        if (!check.IsAccepted)
        {
            return;
        }

        var value = 0f;
        var reasons = new List<string>();
        foreach (var reached in _caster.FindReached(world, miracle, settlement.Position))
        {
            value += _valuer.Value(world, god, profile, miracle, reached, settlement.Position, reasons);
        }

        if (value <= 0f)
        {
            return;
        }

        var utility = value * (1f - (profile.Thrift * check.Cost / Math.Max(1f, god.DivinePower)));
        reasons.Add(MiracleValuer.Format("cost {0:0}", check.Cost));
        candidates.Add(new AiCandidate(AiActionKind.CastMiracle, miracle.Id, settlement.Id, settlement.Position, value, check.Cost, utility, string.Join(", ", reasons)));
    }

    /// <summary>Adds sending the Creature to the settlement the god courts most, if it is far from there.</summary>
    private void AddSendCreature(List<AiCandidate> candidates, WorldState world, GodState god, GodProfile profile)
    {
        if (profile.SendCreature <= 0f || god.CreatureId is not { } id || world.Creatures.Find(c => c.Id == id) is not { } creature
            || creature.Activity.Phase == CreaturePhase.Banished || creature.Activity.Action == CreatureAction.GoTo || CreatureBrain.IsEngaged(creature) || !IsFitToSend(creature))
        {
            return;
        }

        SettlementState? best = null;
        var bestCourt = 0f;
        foreach (var settlement in world.Settlements)
        {
            var mine = BeliefModel.Find(settlement, god.Id)?.Adherence ?? 0f;
            var court = (1f - mine) * Math.Min(MaxPopulationFactor, MathF.Sqrt(settlement.Population / _rules.ReferencePopulation));
            var dx = settlement.Position.X - creature.Position.X;
            var dz = settlement.Position.Z - creature.Position.Z;
            if (court > bestCourt && MathF.Sqrt((dx * dx) + (dz * dz)) > _rules.CreatureSendDistance)
            {
                bestCourt = court;
                best = settlement;
            }
        }

        if (best is not null)
        {
            var value = profile.SendCreature * bestCourt;
            candidates.Add(new AiCandidate(AiActionKind.SendCreature, null, best.Id, best.Position, value, 0f, value, MiracleValuer.Format("court {0} {1:0.00}", best.Name, value)));
        }

        AddConfrontCreature(candidates, world, god, profile, creature);
        AddClaimHolySites(candidates, world, god, profile, creature);
        GoalFocus.AddHunt(_rules.Planning, world, god, profile, creature, candidates);
    }

    /// <summary>
    /// Adds sending the Creature to a holy site (ADR-021): its presence lends the god influence there,
    /// to take a site it does not keep or to hold its own against a claimant. Nearer sites count for more.
    /// </summary>
    private static void AddClaimHolySites(List<AiCandidate> candidates, WorldState world, GodState god, GodProfile profile, CreatureState creature)
    {
        foreach (var site in world.HolySites)
        {
            var defending = site.KeeperGodId == god.Id;
            if ((defending && site.ClaimantGodId is null) || (site.KeeperGodId is { } keeper && DiplomacyModel.AreAllied(world, god.Id, keeper)))
            {
                continue;
            }

            var dx = site.Position.X - creature.Position.X;
            var dz = site.Position.Z - creature.Position.Z;
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            if (distance <= ClaimDistance)
            {
                continue;
            }

            var value = profile.HolySites * (defending ? 1f : 0.7f) / (1f + (distance / 1000f));
            if (value > 0f)
            {
                candidates.Add(new AiCandidate(AiActionKind.SendCreature, null, null, site.Position, value, 0f, value, MiracleValuer.Format("{0} {1} {2:0.00}", defending ? "hold" : "claim", site.Name, value)));
            }
        }
    }

    /// <summary>
    /// Adds sending the Creature against the Creature of a god at war with this one (ADR-017): worth
    /// more the more of this god's people it threatens (Protect) and the more warlike the god
    /// (Aggression), and only when its own Creature is a match for it.
    /// </summary>
    private void AddConfrontCreature(List<AiCandidate> candidates, WorldState world, GodState god, GodProfile profile, CreatureState creature)
    {
        if (_creatureRules is null)
        {
            return;
        }

        foreach (var enemy in world.Creatures)
        {
            if (enemy.GodId == god.Id || enemy.Activity.Phase == CreaturePhase.Banished || !DiplomacyModel.AreAtWar(world, god.Id, enemy.GodId))
            {
                continue;
            }

            var confidence = CreatureBonds.Confidence(_creatureRules, creature, enemy);
            var dx = enemy.Position.X - creature.Position.X;
            var dz = enemy.Position.Z - creature.Position.Z;
            if (confidence < MatchConfidence || MathF.Sqrt((dx * dx) + (dz * dz)) <= _creatureRules.Bonds.MeetRadius)
            {
                continue;
            }

            var threat = 0f;
            foreach (var settlement in world.Settlements)
            {
                var sx = settlement.Position.X - enemy.Position.X;
                var sz = settlement.Position.Z - enemy.Position.Z;
                if ((sx * sx) + (sz * sz) <= _creatureRules.PerceptionRadius * _creatureRules.PerceptionRadius)
                {
                    threat = Math.Max(threat, BeliefModel.Find(settlement, god.Id)?.Adherence ?? 0f);
                }
            }

            var value = profile.SendCreature * ((profile.Protect * threat) + (0.5f * profile.Aggression)) * confidence;
            if (value > 0f)
            {
                candidates.Add(new AiCandidate(AiActionKind.SetCreatureOn, null, null, enemy.Position, value, 0f, value, MiracleValuer.Format("confront {0} {1:0.00}", enemy.Name, value)));
            }
        }
    }

    /// <summary>Whether the god's Creature hunts a rival it was set on, which the god does not call it off for an errand (ADR-070).</summary>
    private static bool OnAHunt(WorldState world, int creatureId) =>
        world.Creatures.Find(c => c.Id == creatureId) is { Activity.TargetCreatureId: { } rival } hunter && CreatureBrain.IsHunting(hunter, rival);

    /// <summary>Whether the Creature can be sent away without neglecting itself: healthy, fed and rested.</summary>
    private bool IsFitToSend(CreatureState creature) =>
        _creatureRules is null
        || (creature.Health >= 0.6f && creature.Needs.Hunger < _creatureRules.HungryThreshold && creature.Needs.Fatigue < _creatureRules.TiredThreshold);

    /// <summary>The profile of a personality, if listed.</summary>
    private static GodProfile? FindProfile(IReadOnlyList<GodProfile> profiles, GodPersonality personality)
    {
        foreach (var profile in profiles)
        {
            if (profile.Personality == personality)
            {
                return profile;
            }
        }

        return null;
    }
}
