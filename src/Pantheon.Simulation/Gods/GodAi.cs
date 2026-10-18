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
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// The strategic utility AI of a god (spec §47–48, §87, ADR-011). Every evaluation it scores every
/// miracle it may cast on every settlement, sending its Creature and waiting, by the goals its
/// personality weighs; then it takes the best action through the same authority as a player. It
/// also teaches its Creature what its personality values. Every score keeps its reasons.
/// </summary>
public sealed class GodAi
{
    private const float MaxPopulationFactor = 2f;
    private const float JudgeThreshold = 0.3f;

    private readonly GodAiRules _rules;
    private readonly DivineRules _divine;
    private readonly MiracleBook _book;
    private readonly MiracleCaster _caster;
    private readonly CreatureAuthority? _creatures;
    private readonly CreatureRules? _creatureRules;
    private readonly GodDiplomat? _diplomat;

    /// <summary>Creates the AI over validated rules and the authorities it acts through.</summary>
    /// <param name="rules">AI rules.</param>
    /// <param name="divine">Divine rules.</param>
    /// <param name="book">The miracles.</param>
    /// <param name="caster">The miracle authority.</param>
    /// <param name="creatures">The Creature authority, or <c>null</c> when there are no Creatures.</param>
    /// <param name="creatureRules">Creature rules, for what an action's kindness is.</param>
    /// <param name="diplomat">The diplomatic half of the AI, or <c>null</c> for no diplomacy.</param>
    public GodAi(GodAiRules rules, DivineRules divine, MiracleBook book, MiracleCaster caster, CreatureAuthority? creatures, CreatureRules? creatureRules, GodDiplomat? diplomat = null)
    {
        _diplomat = diplomat;
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
    }

    /// <summary>The profile of a personality.</summary>
    /// <param name="personality">The personality.</param>
    /// <returns>Its profile, or <c>null</c> when the rules have none.</returns>
    public GodProfile? ProfileOf(GodPersonality personality) => _rules.Profiles is { } profiles ? FindProfile(profiles, personality) : null;

    /// <summary>Every action the god could take now, best first, without jitter.</summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god.</param>
    /// <returns>The candidates; always including waiting.</returns>
    public List<AiCandidate> Evaluate(WorldState world, GodState god)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        var candidates = new List<AiCandidate> { new(AiActionKind.Wait, null, null, default, _rules.Patience, 0f, _rules.Patience, "save power") };
        if (ProfileOf(god.Personality) is not { } profile)
        {
            return candidates;
        }

        foreach (var miracle in _book.Miracles)
        {
            foreach (var settlement in world.Settlements)
            {
                AddCast(candidates, world, god, profile, miracle, settlement);
            }
        }

        AddSendCreature(candidates, world, god, profile);
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
        if (_diplomat is not null && ProfileOf(god.Personality) is { } profile)
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
        _diplomat is not null && ProfileOf(god.Personality) is { } profile ? _diplomat.Evaluate(world, god, profile) : [];

    /// <summary>Carries out an action through the authorities, as the god itself (the host may act for any god).</summary>
    private void Execute(WorldState world, GodState god, AiCandidate candidate, List<WorldEvent> events)
    {
        switch (candidate.Kind)
        {
            case AiActionKind.CastMiracle:
                _caster.Cast(world, new CastMiracleRequest(god.Id, candidate.MiracleId!, candidate.Target), god.Id, events);
                break;
            case AiActionKind.SendCreature when _creatures is not null && god.CreatureId is { } creature:
                _creatures.Execute(world, new CreatureCommand(god.Id, creature, CreatureCommandKind.GoTo, candidate.Target), god.Id, events);
                break;
            case AiActionKind.SendCreature:
            case AiActionKind.Wait:
                break;
        }
    }

    /// <summary>Rewards or punishes the Creature's latest unjudged deed by how its kindness suits the god's personality.</summary>
    private void TeachCreature(WorldState world, GodState god, List<WorldEvent> events)
    {
        if (_creatures is null || _creatureRules is null || god.CreatureId is not { } id || ProfileOf(god.Personality) is not { } profile
            || world.Creatures.Find(c => c.Id == id) is not { } creature || _creatures.FindJudged(world, creature) is not { Memory: { Feedback: CreatureFeedback.None } } judged)
        {
            return;
        }

        if (judged.Action == CreatureAction.GoTo)
        {
            return;
        }

        var approval = CreatureModel.RuleFor(_creatureRules, judged.Action).Kindness * profile.CreatureKindness;
        var kind = approval >= JudgeThreshold ? CreatureCommandKind.Reward : approval <= -JudgeThreshold ? CreatureCommandKind.Punish : (CreatureCommandKind?)null;
        if (kind is { } command)
        {
            _creatures.Execute(world, new CreatureCommand(god.Id, id, command), god.Id, events);
        }
    }

    /// <summary>Adds the cast of a miracle on a settlement, when it is allowed and worth something.</summary>
    private void AddCast(List<AiCandidate> candidates, WorldState world, GodState god, GodProfile profile, MiracleDefinition miracle, SettlementState settlement)
    {
        if (settlement.Population <= 0)
        {
            return;
        }

        var check = _caster.Check(world, new CastMiracleRequest(god.Id, miracle.Id, settlement.Position), god.Id);
        if (!check.IsAccepted)
        {
            return;
        }

        var value = 0f;
        var reasons = new List<string>();
        foreach (var reached in _caster.FindReached(world, miracle, settlement.Position))
        {
            value += Value(world, god, profile, miracle, reached, reasons);
        }

        if (value <= 0f)
        {
            return;
        }

        var utility = value * (1f - (profile.Thrift * check.Cost / Math.Max(1f, god.DivinePower)));
        reasons.Add(Format("cost {0:0}", check.Cost));
        candidates.Add(new AiCandidate(AiActionKind.CastMiracle, miracle.Id, settlement.Id, settlement.Position, value, check.Cost, utility, string.Join(", ", reasons)));
    }

    /// <summary>What a miracle is worth to the god in one settlement it reaches, by the god's goals.</summary>
    private float Value(WorldState world, GodState god, GodProfile profile, MiracleDefinition miracle, SettlementState settlement, List<string> reasons)
    {
        var own = BeliefModel.Find(settlement, god.Id);
        var mine = own?.Adherence ?? 0f;
        var rivals = 0f;
        var allies = 0f;
        var rivalDoubt = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.GodId == god.Id)
            {
                continue;
            }

            if (DiplomacyModel.AreAllied(world, god.Id, belief.GodId))
            {
                // An ally's followers are not to be courted or frightened.
                allies += belief.Adherence;
                continue;
            }

            var enmity = DiplomacyModel.AreAtWar(world, god.Id, belief.GodId) ? 1.5f : 1f;
            rivals += belief.Adherence * enmity;
            rivalDoubt = Math.Max(rivalDoubt, belief.Adherence > 0f ? belief.Doubt : 0f);
        }

        var free = Math.Max(0f, 1f - mine - rivals - allies);
        var conditions = BeliefModel.AssessConditions(_divine, settlement);
        var size = Math.Min(MaxPopulationFactor, MathF.Sqrt(settlement.Population / _rules.ReferencePopulation));
        var opportunity = 1f + (profile.Opportunism * Math.Max(conditions.Hardship, rivalDoubt));
        var value = 0f;
        foreach (var effect in miracle.Effects)
        {
            switch (effect.Kind)
            {
                case MiracleEffectKind.AddFood:
                case MiracleEffectKind.Heal:
                    var need = BeliefModel.NeedFor(conditions, miracle.Gratitude);
                    value += Note(reasons, settlement, "protect", profile.Protect * mine * need * size);
                    value += Note(reasons, settlement, "defend", profile.Protect * mine * Math.Min(1f, 2f * rivals) * size);
                    value += Note(reasons, settlement, "convert", profile.Convert * (free + rivals) * (0.3f + (0.7f * need)) * opportunity * size);
                    break;
                case MiracleEffectKind.Bless:
                    value += Note(reasons, settlement, "bless", profile.Bless * (mine + (0.5f * (1f - mine))) * (0.4f + (0.6f * conditions.FoodNeed)) * size);
                    break;
                case MiracleEffectKind.Smite:
                    // Striking a village also strikes its own followers there; it never strikes an ally's.
                    if (allies <= 0.05f)
                    {
                        value += Note(reasons, settlement, "intimidate", profile.Intimidate * (rivals + (0.3f * free)) * opportunity * (1f - mine) * size);
                    }

                    break;
            }
        }

        return value;
    }

    /// <summary>Adds sending the Creature to the settlement the god courts most, if it is far from there.</summary>
    private void AddSendCreature(List<AiCandidate> candidates, WorldState world, GodState god, GodProfile profile)
    {
        if (profile.SendCreature <= 0f || god.CreatureId is not { } id || world.Creatures.Find(c => c.Id == id) is not { } creature
            || creature.Activity.Phase == CreaturePhase.Banished || creature.Activity.Action == CreatureAction.GoTo || !IsFitToSend(creature))
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
            candidates.Add(new AiCandidate(AiActionKind.SendCreature, null, best.Id, best.Position, value, 0f, value, Format("court {0} {1:0.00}", best.Name, value)));
        }
    }

    /// <summary>Whether the Creature can be sent away without neglecting itself: healthy, fed and rested.</summary>
    private bool IsFitToSend(CreatureState creature) =>
        _creatureRules is null
        || (creature.Health >= 0.6f && creature.Needs.Hunger < _creatureRules.HungryThreshold && creature.Needs.Fatigue < _creatureRules.TiredThreshold);

    /// <summary>Records a factor when it counts.</summary>
    private static float Note(List<string> reasons, SettlementState settlement, string goal, float value)
    {
        if (value > 0.005f)
        {
            reasons.Add(Format("{0} {1} {2:0.00}", goal, settlement.Name, value));
        }

        return value;
    }

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

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
