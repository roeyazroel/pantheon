using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// Simulates every Creature each tick (spec §80 Tier 3, §89, ADR-010): its needs and health, its
/// state machine (deciding, travelling, performing, banished), its walk along its path, and the
/// effects of what it finishes. It decides at most once every <c>DecisionIntervalTicks</c> ticks.
/// </summary>
public sealed class CreatureSystem : ISimulationSystem
{
    // A returning Creature comes back with this much health and hunger.
    private const float ReturnHealth = 0.5f;
    private const float ReturnHunger = 0.3f;

    // Metres within which a waypoint counts as reached.
    private const float ArrivalTolerance = 1e-3f;

    private readonly CreatureRules _rules;
    private readonly CreatureBrain _brain;
    private readonly CreatureDeeds _deeds;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules.</param>
    public CreatureSystem(CreatureRules rules, DivineRules divine, SettlementRules settlementRules)
    {
        CreatureRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _brain = new CreatureBrain(rules, divine, settlementRules);
        _deeds = new CreatureDeeds(rules, divine, settlementRules);
    }

    /// <summary>The brain, for previews and commands.</summary>
    public CreatureBrain Brain => _brain;

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var decides = IntervalSystem.IsDue(context.World.Tick, _rules.DecisionIntervalTicks);
        foreach (var creature in context.World.Creatures)
        {
            Step(context, creature, decides);
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

        switch (activity.Phase)
        {
            case CreaturePhase.Deciding:
                if (decides)
                {
                    _brain.Decide(world, creature, context.Random);
                }

                break;
            case CreaturePhase.Travelling:
                Walk(creature);
                if (activity.Path.Count == 0)
                {
                    activity.Phase = CreaturePhase.Performing;
                    activity.TicksLeft = _brain.PerformTicks(creature, activity.Action);
                }

                break;
            case CreaturePhase.Performing:
                if (--activity.TicksLeft <= 0)
                {
                    _deeds.Complete(world, creature, context.Events);
                    activity.Phase = CreaturePhase.Deciding;
                }

                break;
            case CreaturePhase.Banished:
                break;
        }
    }

    /// <summary>Starvation wears health down; resting restores it.</summary>
    private void UpdateHealth(CreatureState creature)
    {
        var activity = creature.Activity;
        var resting = activity.Action == CreatureAction.Rest && activity.Phase == CreaturePhase.Performing;
        if (creature.Needs.Hunger >= 1f)
        {
            creature.Health -= _rules.StarvationHealthLossPerSecond * SimulationTime.SecondsPerTick;
        }
        else if (resting)
        {
            creature.Health += _rules.RestHealthPerSecond * SimulationTime.SecondsPerTick;
        }

        creature.Health = SimMath.Clamp01(creature.Health);
    }

    /// <summary>Moves the Creature along its path at its speed, carrying any leftover distance to the next waypoint.</summary>
    private void Walk(CreatureState creature)
    {
        var path = creature.Activity.Path;
        var step = CreatureModel.Speed(_rules, creature) * SimulationTime.SecondsPerTick;
        while (step > 0f && path.Count > 0)
        {
            var next = path[0];
            var dx = next.X - creature.Position.X;
            var dz = next.Z - creature.Position.Z;
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            if (distance > 1e-4f)
            {
                creature.Heading = MathF.Atan2(dx, dz);
            }

            // A hair of tolerance, so rounding never leaves the Creature a millimetre short for a tick.
            if (distance <= step + ArrivalTolerance)
            {
                creature.Position = next;
                path.RemoveAt(0);
                step -= distance;
            }
            else
            {
                creature.Position = new GroundPosition(creature.Position.X + (dx / distance * step), creature.Position.Z + (dz / distance * step));
                step = 0f;
            }
        }
    }

    /// <summary>Defeat (spec §28): the Creature leaves for a while.</summary>
    private void Banish(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        var activity = creature.Activity;
        activity.Phase = CreaturePhase.Banished;
        activity.Path.Clear();
        activity.TargetSettlementId = null;
        activity.TicksLeft = Math.Max(1, (int)MathF.Round(_rules.BanishedSeconds * SimulationTime.TicksPerSecond));
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
