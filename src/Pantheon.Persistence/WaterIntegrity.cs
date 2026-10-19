using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the rivers and lakes of a deserialized world (part of <see cref="SaveIntegrity"/>, ADR-022).</summary>
internal static class WaterIntegrity
{
    /// <summary>Records every inconsistency of the rivers and lakes.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, List<string> errors)
    {
        if (world.Water is null || world.Water.Rivers is null || world.Water.Lakes is null)
        {
            errors.Add("The world's water, its rivers or its lakes are missing.");
            return;
        }

        var riverIds = new HashSet<int>();
        foreach (var river in world.Water.Rivers)
        {
            if (river is null || river.Id < 1 || !riverIds.Add(river.Id) || string.IsNullOrWhiteSpace(river.Name) || river.Regions is null)
            {
                errors.Add("A river is null, unnamed, has no regions, or its id is below 1 or repeated.");
                continue;
            }

            if (!float.IsFinite(river.Level) || river.Level < 0f || !AreRegions(world, river.Regions))
            {
                errors.Add($"River {river.Id} has a negative or invalid level, or runs through a region that does not exist or twice.");
            }
        }

        var lakeIds = new HashSet<int>();
        foreach (var lake in world.Water.Lakes)
        {
            if (lake is null || lake.Id < 1 || !lakeIds.Add(lake.Id) || string.IsNullOrWhiteSpace(lake.Name) || lake.Regions is null)
            {
                errors.Add("A lake is null, unnamed, has no regions, or its id is below 1 or repeated.");
                continue;
            }

            if (!float.IsFinite(lake.SurfaceHeight) || !AreRegions(world, lake.Regions))
            {
                errors.Add($"Lake {lake.Id} has an invalid surface, or covers a region that does not exist or twice.");
            }
        }
    }

    /// <summary>Whether every id names a region of the grid, each once.</summary>
    private static bool AreRegions(WorldState world, List<int> regions)
    {
        var seen = new HashSet<int>();
        foreach (var id in regions)
        {
            if (id < 0 || id >= world.Regions.Cells.Count || !seen.Add(id))
            {
                return false;
            }
        }

        return true;
    }
}
