using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// Simulates every Creature each tick (spec §80 Tier 3, §86, §89, ADR-010, ADR-047): its needs and
/// health, its hierarchical state machine (<see cref="CreatureModes"/>: free, engaged, held,
/// banished), the brain's choices and the behaviour tree (<see cref="CreatureTree"/>) that carries
/// them out. It decides at most once every <c>DecisionIntervalTicks</c> ticks.
/// </summary>
public sealed class CreatureSystem : ISimulationSystem
{
    // A returning Creature comes back with this much health and hunger.
    private const float ReturnHealth = 0.5f;
    private const float ReturnHunger = 0.3f;

    private readonly CreatureRules _rules;
    private readonly CreatureBrain _brain;
    private readonly CreatureDeeds _deeds;
    private readonly CreatureMeetings _meetings;
    private readonly CreatureCombat _combat;
    private readonly CreatureTree _tree;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules.</param>
    /// <param name="diplomacy">Diplomacy rules, so Creatures turn on their god's enemies and spare its allies; <c>null</c> to ignore diplomacy.</param>
    /// <param name="ledger">Where attacks on other gods' followers are recorded; <c>null</c> for none.</param>
    /// <param name="domains">Domain rules, for the War domain's harder blows (ADR-021); <c>null</c> for none.</param>
    public CreatureSystem(CreatureRules rules, DivineRules divine, SettlementRules settlementRules, DiplomacyRules? diplomacy = null, IActRecorder? ledger = null, Domain.Gods.DomainRules? domains = null)
    {
        CreatureRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _brain = new CreatureBrain(rules, divine, settlementRules, diplomacy);
        _deeds = new CreatureDeeds(rules, divine, settlementRules, ledger);
        _meetings = new CreatureMeetings(rules, _brain, ledger);
        _combat = new CreatureCombat(rules, ledger, domains);
        _tree = new CreatureTree(rules, divine, settlementRules, _brain, _deeds);
    }

    /// <summary>The brain, for previews and commands.</summary>
    public CreatureBrain Brain => _brain;

    /// <summary>The behaviour trees that carry out the brain's choices (ADR-047), for debug tools.</summary>
    public CreatureTree Tree => _tree;

    /// <summary>The meetings between Creatures, for debug tools.</summary>
    public CreatureMeetings Meetings => _meetings;

    /// <summary>
    /// Steps every Creature, then lets free Creatures that are close meet (on decision ticks), plays a
    /// tick of every fight, and once a second lets the Creatures' views of each other fade (ADR-017).
    /// </summary>
    /// <param name="context">The tick.</param>
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var decides = IntervalSystem.IsDue(world.Tick, _rules.DecisionIntervalTicks);
        foreach (var creature in world.Creatures)
        {
            // A Creature in its god's hand waits there until it is set down (ADR-030).
            if (!CreatureModel.IsHeld(world, creature))
            {
                Step(context, creature, decides);
            }

            CreatureCalling.Grow(_rules.Callings, creature, world.Tick, context.Events);
        }

        if (decides)
        {
            _meetings.Update(world, context.Random, context.Events);
        }

        _combat.Update(world, context.Events);
        if (IntervalSystem.IsDue(world.Tick, SimulationTime.TicksPerSecond))
        {
            foreach (var creature in world.Creatures)
            {
                CreatureBonds.Fade(_rules.Bonds, creature, 1f);
            }
        }
    }

    /// <summary>One tick of one Creature.</summary>
    private void Step(TickContext context, CreatureState creature, bool decides)
    {
        var world = context.World;
        var activity = creature.Activity;
        if (activity.Phase == CreaturePhase.Banished)
        {
            if (--activity.TicksLeft <= 0)
            {
                Return(world, creature, context.Events);
            }

            return;
        }

        CreatureModel.GrowNeeds(_rules, creature.Needs, SimulationTime.SecondsPerTick);
        UpdateHealth(creature);
        if (creature.Health <= 0f)
        {
            Banish(world, creature, context.Events);
            return;
        }

        // The hierarchical state machine (ADR-047): a fight is the combat's to play out; otherwise the
        // brain decides, and the behaviour tree carries out what it chose, playing included.
        if (CreatureModes.ModeOf(world, creature) == CreatureMode.Engaged && CreatureCombat.IsFighting(creature))
        {
            return;
        }

        if (activity.Phase == CreaturePhase.Deciding)
        {
            if (decides)
            {
                _brain.Decide(world, creature, context.Random);
            }

            return;
        }

        if (_tree.Tick(world, creature, context.Events) != TreeStatus.Running)
        {
            activity.Phase = CreaturePhase.Deciding;
        }
    }

    /// <summary>Starvation wears health down; resting restores it.</summary>
    private void UpdateHealth(CreatureState creature)
    {
        var activity = creature.Activity;
        var resting = activity.Action == CreatureAction.Rest && activity.Phase == CreaturePhase.Performing;
        if (creature.Needs.Hunger >= 1f)
        {
            creature.Health -= _rules.StarvationHealthLossPerSecond * SimulationTime.SecondsPerTick / CreatureAttributes.Resilience(_rules, creature);
        }
        else if (resting)
        {
            creature.Health += _rules.RestHealthPerSecond * SimulationTime.SecondsPerTick;
        }

        creature.Health = SimMath.Clamp01(creature.Health);
    }

    /// <summary>Starvation (spec §28): the Creature leaves for a while.</summary>
    private void Banish(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        CreatureBanishment.Banish(_rules, creature);
        events.Add(new CreatureChanged { Tick = world.Tick, CreatureId = creature.Id, Change = CreatureChange.Banished });
    }

    /// <summary>The Creature comes back to its home settlement, weakened.</summary>
    private static void Return(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        var home = world.Settlements.Find(s => s.Id == creature.HomeSettlementId) ?? (world.Settlements.Count > 0 ? world.Settlements[0] : null);
        if (home is not null)
        {
            creature.Position = CreatureFounder.SpawnPoint(world.Regions, home.Position);
        }

        creature.Health = ReturnHealth;
        creature.Needs.Hunger = ReturnHunger;
        creature.Activity.Phase = CreaturePhase.Deciding;
        creature.Activity.TicksLeft = 0;
        events.Add(new CreatureChanged { Tick = world.Tick, CreatureId = creature.Id, Change = CreatureChange.Returned });
    }
}
