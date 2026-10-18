using System;
using System.Collections.Generic;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
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

    /// <summary>Creates the Milestone 1 system pipeline.</summary>
    /// <param name="settlementRules">Tuning of the statistical settlement model.</param>
    public SimulationEngine(SettlementRules settlementRules)
        : this([new SettlementSystem(settlementRules)])
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
    public void Step(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var random = SimulationRandom.FromState(world.Random);
        var context = new TickContext(world, random);
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
}
