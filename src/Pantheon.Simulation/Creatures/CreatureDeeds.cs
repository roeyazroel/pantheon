using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// What a finished Creature action does to the world (spec §22, §111): food given or taken, work
/// on a house, villagers killed, needs eased, experience and morality, a memory, and the change in
/// the belief of the people it acted on towards its god.
/// </summary>
public sealed class CreatureDeeds
{
    // Morality moves this share of an action's kindness per deed.
    private const float MoralityStep = 0.05f;

    private readonly CreatureRules _rules;
    private readonly DivineRules _divine;
    private readonly SettlementRules _settlementRules;
    private readonly IActRecorder? _ledger;

    /// <summary>Creates the deeds over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules, for belief.</param>
    /// <param name="settlementRules">Settlement rules, for storage and houses.</param>
    /// <param name="ledger">Where attacks on other gods' followers are recorded; <c>null</c> for none.</param>
    public CreatureDeeds(CreatureRules rules, DivineRules divine, SettlementRules settlementRules, IActRecorder? ledger = null)
    {
        _rules = rules;
        _divine = divine;
        _settlementRules = settlementRules;
        _ledger = ledger;
    }

    /// <summary>Applies a finished action and records it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="events">Receives <see cref="CreatureActed"/> and, on a new level, <see cref="CreatureChanged"/>.</param>
    public void Complete(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(events);
        var activity = creature.Activity;
        var rule = activity.Action == CreatureAction.GoTo ? null : CreatureModel.RuleFor(_rules, activity.Action);
        var settlement = activity.TargetSettlementId is { } id ? world.Settlements.Find(s => s.Id == id) : null;
        var (food, deaths, share) = rule is null ? (0f, 0, 1f) : ApplyToSettlement(creature, rule, settlement);
        if (rule is not null)
        {
            CreatureModel.Relieve(rule, creature.Needs, share);
            if (settlement is not null && settlement.Population > 0)
            {
                Witness(creature, rule, settlement);
                if (rule.Action == CreatureAction.Attack)
                {
                    NoteAttack(world, creature, settlement);
                }
            }

            creature.Morality = Math.Clamp(creature.Morality + (rule.Kindness * MoralityStep), -1f, 1f);
            Gain(creature, rule.Experience, world.Tick, events);
        }

        Remember(creature, world.Tick, activity.Action, activity.Situation, settlement?.Id);
        events.Add(new CreatureActed
        {
            Tick = world.Tick,
            CreatureId = creature.Id,
            Action = activity.Action,
            SettlementId = settlement?.Id,
            Deaths = deaths,
            Food = food,
        });
    }

    /// <summary>The action's effect on its settlement.</summary>
    /// <returns>Food given (positive) or taken (negative), people killed, and the share of the action's relief earned.</returns>
    private (float Food, int Deaths, float Share) ApplyToSettlement(CreatureState creature, CreatureActionRule rule, SettlementState? settlement)
    {
        var size = rule.Amount * CreatureModel.Strength(_rules, creature);
        if (settlement is null)
        {
            return (0f, 0, 1f);
        }

        switch (rule.Action)
        {
            case CreatureAction.Feed:
                var storage = SettlementEconomy.ComputeStorage(_settlementRules, settlement.Housing);
                var before = settlement.Food;
                settlement.Food = Math.Max(before, Math.Min(storage, before + size));
                return (settlement.Food - before, 0, 1f);
            case CreatureAction.EatFromStores:
                var eaten = Math.Min(settlement.Food, size);
                settlement.Food -= eaten;
                return (-eaten, 0, size > 0f ? eaten / size : 0f);
            case CreatureAction.HelpBuild:
                Build(settlement, size);
                return (0f, 0, 1f);
            case CreatureAction.Attack:
                var killed = Math.Min(settlement.Population, (int)MathF.Round(size));
                settlement.Population -= killed;
                return (0f, killed, 1f);
            case CreatureAction.Wander:
            case CreatureAction.Rest:
            case CreatureAction.Forage:
            case CreatureAction.Play:
            case CreatureAction.Ignore:
            case CreatureAction.GoTo:
                return (0f, 0, 1f);
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule.Action, "Unknown creature action.");
        }
    }

    /// <summary>Adds builder-steps to the settlement's house, finishing it (and starting the next) as the settlement step does.</summary>
    private void Build(SettlementState settlement, float work)
    {
        if (settlement.Housing >= _settlementRules.MaxHousing)
        {
            return;
        }

        settlement.ConstructionProgress += work;
        while (settlement.ConstructionProgress >= _settlementRules.WorkPerHouse && settlement.Housing < _settlementRules.MaxHousing)
        {
            settlement.ConstructionProgress -= _settlementRules.WorkPerHouse;
            settlement.Housing = Math.Min(_settlementRules.MaxHousing, settlement.Housing + _settlementRules.HousingPerHouse);
        }
    }

    /// <summary>The people see what their god's Creature did, and their belief in the god changes.</summary>
    private void Witness(CreatureState creature, CreatureActionRule rule, SettlementState settlement)
    {
        // Only the food need matters here, so the drought is left out.
        var conditions = BeliefModel.AssessConditions(_divine, settlement, drought: 0f);
        var need = rule.Action == CreatureAction.Feed ? conditions.FoodNeed : 1f;
        var loveScale = _divine.GratitudeFloor + ((1f - _divine.GratitudeFloor) * need);
        var belief = BeliefModel.FindOrAdd(_divine, settlement, creature.GodId);
        BeliefModel.ApplyImpulse(belief, rule.Witness, 1f, loveScale);
    }

    /// <summary>Every other god whose followers the Creature attacked remembers it.</summary>
    private void NoteAttack(WorldState world, CreatureState creature, SettlementState settlement)
    {
        if (_ledger is null)
        {
            return;
        }

        foreach (var belief in settlement.Beliefs)
        {
            if (belief.GodId != creature.GodId && belief.Adherence > 0.05f)
            {
                _ledger.Record(world, belief.GodId, creature.GodId, DiplomaticAct.CreatureAttacked, settlement.Id);
            }
        }
    }

    /// <summary>Adds experience and raises the level it earns.</summary>
    private void Gain(CreatureState creature, float experience, long tick, List<WorldEvent> events)
    {
        creature.Experience += Math.Max(0f, experience);
        var level = CreatureModel.LevelFor(_rules, creature.Experience);
        if (level > creature.Level)
        {
            creature.Level = level;
            events.Add(new CreatureChanged { Tick = tick, CreatureId = creature.Id, Change = CreatureChange.LevelledUp });
        }
    }

    /// <summary>Adds a memory, forgetting the oldest beyond the capacity.</summary>
    private void Remember(CreatureState creature, long tick, CreatureAction action, CreatureSituation situation, int? settlementId)
    {
        creature.Memories.Add(new CreatureMemory { Tick = tick, Action = action, Situation = situation, SettlementId = settlementId, Feedback = CreatureFeedback.None });
        if (creature.Memories.Count > _rules.MemoryCapacity)
        {
            creature.Memories.RemoveRange(0, creature.Memories.Count - _rules.MemoryCapacity);
        }
    }
}
