using System;
using Pantheon.Domain.Events;
using Pantheon.Domain.Roads;
using Pantheon.Simulation.Resources;
using Pantheon.Simulation.Roads;

namespace Pantheon.Game.Resources;

/// <summary>Debug drivers for wood, stone and roads (ADR-033): <c>--roads</c> and <c>--stores</c>. Each returns a status line; only the host changes its world.</summary>
public static class ResourceCommands
{
    // Enough for any island; a guard against a plan that never ends.
    private const int MostRoads = 200;

    /// <summary>Builds at once, whole, every road the villages would plan, each to all its unjoined neighbours in range.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <returns>Status line.</returns>
    public static string BuildRoads(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var rules = session.Content.SettlementRules.Roads;
        if (!rules.Enabled)
        {
            return "This world builds no roads";
        }

        var built = session.Command((_, events) =>
        {
            var count = 0;
            foreach (var village in world.Settlements)
            {
                while (village.Population > 0 && count < MostRoads && RoadModel.Plan(rules, world, village) is { } plan)
                {
                    var length = RoadModel.Length(plan.Path);
                    var road = new RoadState { Id = world.NextRoadId++, FromSettlementId = village.Id, ToSettlementId = plan.Neighbour.Id, Path = plan.Path, Length = length, Built = length };
                    world.Roads.Add(road);
                    events.Add(new RoadBuilt { Tick = world.Tick, RoadId = road.Id, FromSettlementId = village.Id, ToSettlementId = plan.Neighbour.Id });
                    count++;
                }
            }

            return count;
        });
        return ResourceText.Format("{0} roads built at once", built);
    }

    /// <summary>Empties or fills every village's stores of wood and stone.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="full">Whether to fill them to what each wants, or empty them.</param>
    /// <returns>Status line.</returns>
    public static string SetStores(GameSession session, bool full)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var rules = session.Content.SettlementRules.Resources;
        foreach (var village in session.World.Settlements)
        {
            (village.Wood, village.Stone) = full ? ResourceModel.Wanted(rules, village) : (0f, 0f);
        }

        return full ? "Every village's wood and stone are full" : "Every village's wood and stone are gone";
    }
}
