using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// Great events as state changes (spec §52, ADR-034): the one of a kind under way, beginning one with
/// its name and ending it, and how much an eclipse's dark moves those who see a miracle.
/// </summary>
public static class GreatEventModel
{
    /// <summary>The great event of a kind under way, if any.</summary>
    /// <param name="world">World state.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="godId">For a reformation, the god; any god when <c>null</c>.</param>
    /// <returns>The event, or <c>null</c>.</returns>
    public static GreatEventState? Active(WorldState world, GreatEventKind kind, int? godId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.GreatEvents.Events.Find(e => e.Kind == kind && (godId is null || e.GodId == godId));
    }

    /// <summary>Begins a great event and tells of it.</summary>
    /// <param name="rules">Great event rules, for its name.</param>
    /// <param name="world">World state.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="endTick">When it ends, for one of set length.</param>
    /// <param name="godId">The god it concerns, if any.</param>
    /// <param name="peak">What it reaches at once.</param>
    /// <param name="events">Receives <see cref="GreatEventBegan"/>.</param>
    /// <returns>The event.</returns>
    public static GreatEventState Begin(GreatEventRules rules, WorldState world, GreatEventKind kind, long? endTick, int? godId, int peak, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        var id = world.GreatEvents.NextId++;
        var great = new GreatEventState { Id = id, Kind = kind, Name = NameFor(rules, kind, id), StartTick = world.Tick, EndTick = endTick, GodId = godId, Peak = peak };
        world.GreatEvents.Events.Add(great);
        events.Add(new GreatEventBegan { Tick = world.Tick, EventId = id, Kind = kind, Name = great.Name, GodId = godId });
        return great;
    }

    /// <summary>Ends a great event and tells of it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="great">The event.</param>
    /// <param name="events">Receives <see cref="GreatEventEnded"/>.</param>
    public static void End(WorldState world, GreatEventState great, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(great);
        ArgumentNullException.ThrowIfNull(events);
        world.GreatEvents.Events.Remove(great);
        events.Add(new GreatEventEnded { Tick = world.Tick, EventId = great.Id, Kind = great.Kind, Name = great.Name, Peak = great.Peak });
    }

    /// <summary>The factor on a miracle's witnesses: the rules' awe while the sun is dark, else 1.</summary>
    /// <param name="rules">Great event rules; none gives 1.</param>
    /// <param name="world">World state.</param>
    /// <returns>The factor.</returns>
    public static float Awe(GreatEventRules? rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return rules is not null && Active(world, GreatEventKind.Eclipse) is not null ? rules.Eclipse.Awe : 1f;
    }

    /// <summary>A great event's name: the kind's names in turn by id, or the kind's own word when it has none.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The event's id.</param>
    /// <returns>The name.</returns>
    public static string NameFor(GreatEventRules rules, GreatEventKind kind, int id)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var names in rules.Names)
        {
            if (names.Kind == kind && names.Names.Count > 0)
            {
                return names.Names[(id - 1) % names.Names.Count];
            }
        }

        return kind switch
        {
            GreatEventKind.Plague => "the Plague",
            GreatEventKind.Eclipse => "the Eclipse",
            GreatEventKind.Reformation => "the Reformation",
            GreatEventKind.WorldWar => "the World War",
            GreatEventKind.GreatDrought => "the Great Drought",
            GreatEventKind.GreatMigration => "the Great Migration",
        };
    }
}
