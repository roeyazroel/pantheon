using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// The magical anomaly (spec §52, ADR-035): now and then, at a place and a time drawn from the seed,
/// power surges for a while. Gods draw Divine Power there by their influence, and miracles cast
/// within its reach cost less, so the gods vie for it.
/// </summary>
public static class AnomalyModel
{
    /// <summary>Purpose name of the anomalies' seeds.</summary>
    public const string Purpose = "anomaly";

    /// <summary>One step: sets the first anomaly, begins one when its time comes, gives the gods their power, and ends it.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="divine">Divine rules, for the gods' influence.</param>
    /// <param name="world">World state.</param>
    /// <param name="seconds">Seconds since the last step.</param>
    /// <param name="events">Receives the great event's beginning and end.</param>
    public static void Step(GreatEventRules rules, DivineRules divine, WorldState world, float seconds, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(world);
        var anomaly = rules.Anomaly;
        if (!anomaly.Enabled)
        {
            return;
        }

        var state = world.GreatEvents;
        if (state.NextAnomalyTick <= 0)
        {
            state.NextAnomalyTick = Next(anomaly, world.Seed, world.Tick, first: true);
        }

        if (GreatEventModel.Active(world, GreatEventKind.Anomaly) is { Position: { } place } surge)
        {
            if (world.Tick >= surge.EndTick)
            {
                GreatEventModel.End(world, surge, events);
                return;
            }

            foreach (var god in world.Gods)
            {
                god.DivinePower += anomaly.PowerPerSecond * InfluenceField.Build(divine, world, god.Id).At(place) * seconds;
            }

            return;
        }

        if (world.Tick >= state.NextAnomalyTick)
        {
            Surge(rules, world, Place(world), events);
        }
    }

    /// <summary>Power surges at a place now, and the next anomaly is set.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="place">Where.</param>
    /// <param name="events">Receives the great event's beginning.</param>
    /// <returns>The anomaly.</returns>
    public static GreatEventState Surge(GreatEventRules rules, WorldState world, GroundPosition place, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var end = world.Tick + Math.Max(1L, (long)MathF.Round(rules.Anomaly.Seconds * SimulationTime.TicksPerSecond));
        var surge = GreatEventModel.Begin(rules, world, GreatEventKind.Anomaly, end, null, 0, events, position: place);
        world.GreatEvents.NextAnomalyTick = Next(rules.Anomaly, world.Seed, end, first: false);
        return surge;
    }

    /// <summary>The factor on a miracle's cost at a point: the rules' factor within an anomaly's reach, else 1.</summary>
    /// <param name="rules">Great event rules; none gives 1.</param>
    /// <param name="world">World state.</param>
    /// <param name="target">Where the miracle is cast.</param>
    /// <returns>The factor.</returns>
    public static float CostFactor(GreatEventRules? rules, WorldState world, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (rules is null || GreatEventModel.Active(world, GreatEventKind.Anomaly) is not { Position: { } place })
        {
            return 1f;
        }

        var (dx, dz) = (target.X - place.X, target.Z - place.Z);
        return (dx * dx) + (dz * dz) <= rules.Anomaly.Radius * rules.Anomaly.Radius ? rules.Anomaly.CostFactor : 1f;
    }

    /// <summary>The tick of the next anomaly after a tick, drawn from the seed and that tick.</summary>
    private static long Next(AnomalyRules rules, WorldSeed seed, long tick, bool first)
    {
        var random = new SimulationRandom(SeedMixer.Derive(seed, Purpose, tick));
        var (shortest, longest) = first ? (rules.FirstMinSeconds, rules.FirstMaxSeconds) : (rules.IntervalMinSeconds, rules.IntervalMaxSeconds);
        return tick + Math.Max(1L, (long)MathF.Round((shortest + ((longest - shortest) * random.NextFloat())) * SimulationTime.TicksPerSecond));
    }

    /// <summary>A land region's centre drawn from the seed and the tick; the island's middle when the draws find only sea.</summary>
    private static GroundPosition Place(WorldState world)
    {
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, Purpose + ".place", world.Tick));
        var grid = world.Regions;
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var region = grid.Cells[random.NextInt(grid.Cells.Count)];
            if (region.Biome != Biome.Sea)
            {
                return grid.GetRegionCenter(region.Id);
            }
        }

        return new GroundPosition(grid.Columns * grid.RegionSize * 0.5f, grid.Rows * grid.RegionSize * 0.5f);
    }
}
