using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// The watch for the great events the world's own state makes (spec §52, ADR-034): a world war while
/// most of the gods are at war, in which every believer grows afraid; a great drought while the whole
/// land stays dry; and a great migration while many of the people are on the road. Each lasts until
/// its cause has clearly passed, so none flickers on and off.
/// </summary>
public static class GreatEventWatch
{
    /// <summary>One step of the watch.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="seconds">Seconds since the last step.</param>
    /// <param name="events">Receives great events' beginnings and ends.</param>
    public static void Step(GreatEventRules rules, WorldState world, float seconds, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        // A world war lasts while all but one of the gods it took still fight, so it does not flicker.
        var atWar = GodsAtWar(world);
        var needed = Math.Max(rules.WorldWarMinGods, (int)MathF.Ceiling(rules.WorldWarShare * world.Gods.Count));
        var war = GreatEventModel.Active(world, GreatEventKind.WorldWar);
        Watch(rules, world, GreatEventKind.WorldWar, atWar >= needed || (war is not null && atWar >= needed - 1), atWar, events);
        if (GreatEventModel.Active(world, GreatEventKind.WorldWar) is not null)
        {
            Fear(world, rules.WarFearPerSecond * seconds);
        }

        var soil = LandSoil(world);
        var state = world.GreatEvents;
        state.DrySinceTick = soil <= rules.DroughtSoil ? state.DrySinceTick ?? world.Tick : null;
        var dry = state.DrySinceTick is { } since && world.Tick - since >= (long)(rules.DroughtSeconds * SimulationTime.TicksPerSecond);
        var drought = GreatEventModel.Active(world, GreatEventKind.GreatDrought);
        Watch(rules, world, GreatEventKind.GreatDrought, dry || (drought is not null && soil <= rules.DroughtSoil + 0.05f), 0, events);

        var (people, road) = (0, 0);
        foreach (var settlement in world.Settlements)
        {
            people += Math.Max(0, settlement.Population);
        }

        foreach (var group in world.Migration.Groups)
        {
            road += group.Population;
        }

        var share = people + road > 0 ? (float)road / (people + road) : 0f;
        var migrating = GreatEventModel.Active(world, GreatEventKind.GreatMigration);
        Watch(rules, world, GreatEventKind.GreatMigration, share >= rules.MigrationShare || (migrating is not null && share >= rules.MigrationShare * 0.5f), road, events);
    }

    /// <summary>The gods in at least one war.</summary>
    /// <param name="world">World state.</param>
    /// <returns>Their number.</returns>
    public static int GodsAtWar(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var gods = new HashSet<int>();
        foreach (var war in world.Diplomacy.Wars)
        {
            gods.Add(war.AggressorGodId);
            gods.Add(war.DefenderGodId);
        }

        return gods.Count;
    }

    /// <summary>The land's mean soil water.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The mean (0..1); 1 for a world without land.</returns>
    public static float LandSoil(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var (sum, count) = (0f, 0);
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome != Biome.Sea)
            {
                (sum, count) = (sum + region.SoilMoisture, count + 1);
            }
        }

        return count == 0 ? 1f : sum / count;
    }

    /// <summary>Begins a great event of a kind when its cause holds and none is under way, keeps its peak, and ends it when the cause passes.</summary>
    private static void Watch(GreatEventRules rules, WorldState world, GreatEventKind kind, bool holds, int reach, ICollection<WorldEvent> events)
    {
        var active = GreatEventModel.Active(world, kind);
        if (active is null && holds)
        {
            GreatEventModel.Begin(rules, world, kind, null, null, reach, events);
        }
        else if (active is not null)
        {
            active.Peak = Math.Max(active.Peak, reach);
            if (!holds)
            {
                GreatEventModel.End(world, active, events);
            }
        }
    }

    /// <summary>Every believer grows afraid.</summary>
    private static void Fear(WorldState world, float fear)
    {
        foreach (var settlement in world.Settlements)
        {
            foreach (var belief in settlement.Beliefs)
            {
                belief.Fear = BeliefModel.ApplyChange(belief.Fear, fear);
            }
        }
    }
}
