using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Systems;

/// <summary>
/// Runs the simulation systems in a fixed order, one tick at a time. Every step is a pure
/// function of the incoming <see cref="WorldState"/> (including its RNG state), so a loaded save
/// continues exactly like an uninterrupted run.
/// </summary>
public sealed class SimulationEngine
{
    private readonly ISimulationSystem[] _systems;

    /// <summary>
    /// Creates the settlement-only pipeline of Milestone 2: the statistical settlement step, run
    /// once every <see cref="SettlementRules.StepIntervalTicks"/> ticks. Belief does not change.
    /// </summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    public SimulationEngine(SettlementRules settlementRules)
        : this([CreateSettlementStep(settlementRules)])
    {
    }

    /// <summary>
    /// Creates the game's pipeline: the settlement step, then the belief step (ADR-009), each on its
    /// own interval. On a tick where both are due, the belief step sees the settlement step's result.
    /// </summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    /// <param name="divineRules">Tuning of belief, worship and Divine Power.</param>
    public SimulationEngine(SettlementRules settlementRules, DivineRules divineRules)
        : this([CreateSettlementStep(settlementRules), CreateBeliefStep(divineRules, settlementRules)])
    {
    }

    /// <summary>Creates an engine with an explicit system order.</summary>
    /// <param name="systems">Systems, in the order they run each tick.</param>
    public SimulationEngine(IReadOnlyList<ISimulationSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);
        _systems = [.. systems];
    }

    /// <summary>Advances the world by one tick.</summary>
    /// <param name="world">World to mutate.</param>
    /// <param name="events">Receives the events raised during the tick; <c>null</c> to discard them.</param>
    public void Step(WorldState world, List<WorldEvent>? events = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        var random = SimulationRandom.FromState(world.Random);
        var context = new TickContext(world, random, events ?? []);
        foreach (var system in _systems)
        {
            system.Update(context);
        }

        world.Random = random.GetState();
        world.Tick++;
    }

    /// <summary>Advances the world by several ticks.</summary>
    /// <param name="world">World to mutate.</param>
    /// <param name="ticks">Number of ticks; must not be negative.</param>
    public void Run(WorldState world, int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        for (var i = 0; i < ticks; i++)
        {
            Step(world);
        }
    }

    /// <summary>Wraps a validated <see cref="SettlementSystem"/> in its step interval.</summary>
    private static IntervalSystem CreateSettlementStep(SettlementRules rules)
    {
        var settlements = new SettlementSystem(rules);
        return new IntervalSystem(settlements, rules.StepIntervalTicks);
    }

    /// <summary>Wraps a validated <see cref="BeliefSystem"/> in its step interval.</summary>
    private static IntervalSystem CreateBeliefStep(DivineRules rules, SettlementRules settlementRules)
    {
        var belief = new BeliefSystem(rules, settlementRules);
        return new IntervalSystem(belief, rules.StepIntervalTicks);
    }
}
