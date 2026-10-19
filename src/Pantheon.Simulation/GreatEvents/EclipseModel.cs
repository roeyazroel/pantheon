using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// Eclipses (spec §52, ADR-034): the sun goes dark at times drawn from the seed, every believer is
/// afraid, and a miracle worked in the dark moves its witnesses the more.
/// </summary>
public static class EclipseModel
{
    /// <summary>Purpose name of the eclipses' seeds.</summary>
    public const string Purpose = "eclipse";

    /// <summary>The tick of the next eclipse after a tick, drawn from the seed and that tick.</summary>
    /// <param name="rules">Eclipse rules.</param>
    /// <param name="seed">World seed.</param>
    /// <param name="tick">Now.</param>
    /// <param name="first">Whether it is the world's first.</param>
    /// <returns>The tick.</returns>
    public static long Next(EclipseRules rules, WorldSeed seed, long tick, bool first)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var random = new SimulationRandom(SeedMixer.Derive(seed, Purpose, tick));
        var (shortest, longest) = first ? (rules.FirstMinSeconds, rules.FirstMaxSeconds) : (rules.IntervalMinSeconds, rules.IntervalMaxSeconds);
        var seconds = shortest + ((longest - shortest) * random.NextFloat());
        return tick + Math.Max(1L, (long)MathF.Round(seconds * SimulationTime.TicksPerSecond));
    }

    /// <summary>One step: sets the first eclipse, begins one when its time comes, and ends it when the sun returns.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="events">Receives the great event's beginning and end.</param>
    public static void Step(GreatEventRules rules, WorldState world, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var eclipse = rules.Eclipse;
        if (!eclipse.Enabled)
        {
            return;
        }

        var state = world.GreatEvents;
        if (state.NextEclipseTick <= 0)
        {
            state.NextEclipseTick = Next(eclipse, world.Seed, world.Tick, first: true);
        }

        if (GreatEventModel.Active(world, GreatEventKind.Eclipse) is { } dark)
        {
            if (world.Tick >= dark.EndTick)
            {
                GreatEventModel.End(world, dark, events);
            }

            return;
        }

        if (world.Tick >= state.NextEclipseTick)
        {
            Darken(rules, world, events);
        }
    }

    /// <summary>The sun goes dark now: every believer feels the dread, and the next eclipse is set.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="events">Receives the great event's beginning.</param>
    /// <returns>The eclipse.</returns>
    public static GreatEventState Darken(GreatEventRules rules, WorldState world, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var end = world.Tick + Math.Max(1L, (long)MathF.Round(rules.Eclipse.Seconds * SimulationTime.TicksPerSecond));
        var eclipse = GreatEventModel.Begin(rules, world, GreatEventKind.Eclipse, end, null, world.Settlements.FindAll(s => s.Population > 0).Count, events);
        foreach (var settlement in world.Settlements)
        {
            foreach (var belief in settlement.Beliefs)
            {
                BeliefModel.ApplyImpulse(belief, rules.Eclipse.Dread, 1f, 1f);
            }
        }

        world.GreatEvents.NextEclipseTick = Next(rules.Eclipse, world.Seed, end, first: false);
        return eclipse;
    }
}
