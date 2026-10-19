using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Water;
using Pantheon.Domain.World;

namespace Pantheon.Tests.Support;

/// <summary>Pinned water rules and a river laid across a test world (ADR-022).</summary>
internal static class WaterWorlds
{
    /// <summary>Rules with fixed values (the balance shipped with Milestone 15, part 2), so water tests keep their meaning when <c>water.json</c> is retuned.</summary>
    public static WaterRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        BaseLevel = 0.45f,
        SoilThreshold = 0.5f,
        SoilLevelGain = 1.5f,
        RainLevelGain = 1.8f,
        LevelResponsePerSecond = 0.03f,
        FloodLevel = 1f,
        MaxFloodLevel = 1.6f,
        FloodBankHeight = 6f,
        FloodRecedePerSecond = 0.01f,
        FloodHousingLossPerSecond = 0.004f,
        FloodDeathsPerSecond = 0.0008f,
        FloodFoodLossPerSecond = 0.02f,
        FloodDreadPerSecond = 0.02f,
        SiltBlessing = 0.4f,
        IrrigationPerSecond = 0.004f,
    };

    /// <summary>
    /// Lays a river along the middle row of a <see cref="DivineWorlds"/> world, from the west edge
    /// (its source) to <paramref name="mouthColumn"/>, and waters the regions it runs through.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="mouthColumn">Column of its last region.</param>
    /// <returns>The river.</returns>
    public static River LayRiver(WorldState world, int mouthColumn = 12)
    {
        var row = 8;
        var regions = Enumerable.Range(0, mouthColumn + 1).Select(column => (row * world.Regions.Columns) + column).ToList();
        var river = new River { Id = 1, Name = "Silverrun", Regions = regions, Level = 0.5f };
        world.Water.Rivers.Add(river);
        foreach (var id in regions)
        {
            world.Regions.Cells[id] = With(world.Regions.Cells[id], freshWater: 1f);
        }

        return river;
    }

    /// <summary>A copy of a region with another fixed fresh water.</summary>
    public static RegionState With(RegionState region, float freshWater) => new()
    {
        Id = region.Id,
        Biome = region.Biome,
        Elevation = region.Elevation,
        Temperature = region.Temperature,
        Moisture = region.Moisture,
        Fertility = region.Fertility,
        Vegetation = region.Vegetation,
        Blessing = region.Blessing,
        SoilMoisture = region.SoilMoisture,
        Fire = region.Fire,
        Scorch = region.Scorch,
        FreshWater = freshWater,
        Flood = region.Flood,
    };

    /// <summary>The ids of a river's regions as a set.</summary>
    public static HashSet<int> RegionsOf(River river) => [.. river.Regions];
}
