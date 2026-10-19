using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks the land's wood and stone, the villages' stores and the roads in a loaded save (ADR-033).</summary>
internal static class RoadIntegrity
{
    // Rounding in a save may leave timber a hair above its forest.
    private const float Slack = 0.0001f;

    /// <summary>Records every inconsistency of the resources and roads.</summary>
    /// <param name="world">The world.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, List<string> errors)
    {
        foreach (var region in world.Regions.Cells)
        {
            if (!InShare(region.Forest) || !InShare(region.Timber) || !InShare(region.Stone) || region.Timber > region.Forest + Slack)
            {
                errors.Add($"Region {region.Id} has forest, timber or stone outside 0..1, or more timber than forest.");
            }
        }

        foreach (var settlement in world.Settlements)
        {
            if (!(settlement.Wood >= 0f && float.IsFinite(settlement.Wood)) || !(settlement.Stone >= 0f && float.IsFinite(settlement.Stone)))
            {
                errors.Add($"Settlement {settlement.Id} has a negative or invalid store of wood or stone.");
            }
        }

        if (world.Roads is null || world.NextRoadId < 1)
        {
            errors.Add("The roads are missing, or the next road id is below 1.");
            return;
        }

        var ids = new HashSet<int>();
        foreach (var road in world.Roads)
        {
            if (road is null || road.Id < 1 || road.Id >= world.NextRoadId || !ids.Add(road.Id) || road.FromSettlementId == road.ToSettlementId
                || !world.Settlements.Exists(s => s.Id == road.FromSettlementId) || !world.Settlements.Exists(s => s.Id == road.ToSettlementId)
                || road.Path is null || road.Path.Count < 2 || road.Path.Exists(p => !float.IsFinite(p.X) || !float.IsFinite(p.Z))
                || !(road.Length >= 0f) || !(road.Built >= 0f && road.Built <= road.Length) || !InShare(road.Wear))
            {
                errors.Add("A road is null, has a repeated or unknown id, unknown or equal villages, a way of fewer than two points, or invalid lengths or wear.");
            }
        }
    }

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool InShare(float value) => value is >= 0f and <= 1f;
}
