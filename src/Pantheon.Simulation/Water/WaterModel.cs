using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Water;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Water;

/// <summary>The rules of rivers and floods as pure functions of the state and the rules (ADR-022).</summary>
public static class WaterModel
{
    /// <summary>
    /// The level the weather sets for a river: <c>BaseLevel + SoilLevelGain x max(0, soil - SoilThreshold) +
    /// RainLevelGain x rain</c>, where soil is the mean water in the soil of its regions and their
    /// neighbours, and rain the mean rain falling on its regions.
    /// </summary>
    /// <param name="rules">Water rules.</param>
    /// <param name="weatherRules">Weather rules, for the rain.</param>
    /// <param name="world">World state.</param>
    /// <param name="river">The river.</param>
    /// <returns>The level it moves towards.</returns>
    public static float TargetLevel(WaterRules rules, WeatherRules weatherRules, WorldState world, River river)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(river);
        if (river.Regions.Count == 0)
        {
            return rules.BaseLevel;
        }

        var grid = world.Regions;
        var soil = 0f;
        var soilCount = 0;
        var rain = 0f;
        foreach (var regionId in river.Regions)
        {
            rain += WeatherModel.At(weatherRules, world.Weather, grid.GetRegionCenter(regionId)).Rain;
            var (column, row) = grid.GetCoordinates(regionId);
            for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
            {
                for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
                {
                    var region = grid.Cells[(r * grid.Columns) + c];
                    if (region.Biome != Biome.Sea)
                    {
                        soil += SimMath.Clamp01(region.SoilMoisture);
                        soilCount++;
                    }
                }
            }
        }

        var meanSoil = soilCount > 0 ? soil / soilCount : 0f;
        var meanRain = rain / river.Regions.Count;
        return rules.BaseLevel + (rules.SoilLevelGain * Math.Max(0f, meanSoil - rules.SoilThreshold)) + (rules.RainLevelGain * meanRain);
    }

    /// <summary>How far a river is over its banks (0 = within them, 1 = the worst flood).</summary>
    /// <param name="rules">Water rules.</param>
    /// <param name="level">The river's level.</param>
    /// <returns>The flood (0..1).</returns>
    public static float Overflow(WaterRules rules, float level)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return SimMath.Clamp01((level - rules.FloodLevel) / (rules.MaxFloodLevel - rules.FloodLevel));
    }

    /// <summary>
    /// The flood a river overflowing by <paramref name="overflow"/> spreads: deepest at its mouth and
    /// shallowest at its source, and half as deep on the land beside it that lies less than
    /// <see cref="WaterRules.FloodBankHeight"/> above it. Adds each region's depth to <paramref name="depths"/>, keeping the deepest.
    /// </summary>
    /// <param name="rules">Water rules.</param>
    /// <param name="grid">Region grid.</param>
    /// <param name="river">The river.</param>
    /// <param name="overflow">How far it is over its banks (0..1).</param>
    /// <param name="depths">Flood per region id, updated.</param>
    public static void Spread(WaterRules rules, RegionGrid grid, River river, float overflow, Dictionary<int, float> depths)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(river);
        ArgumentNullException.ThrowIfNull(depths);
        if (overflow <= 0f)
        {
            return;
        }

        for (var i = 0; i < river.Regions.Count; i++)
        {
            var regionId = river.Regions[i];
            var region = grid.GetRegion(regionId);
            var depth = overflow * (i + 1) / river.Regions.Count;
            Deepen(depths, regionId, depth);
            var (column, row) = grid.GetCoordinates(regionId);
            for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
            {
                for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
                {
                    var bank = grid.Cells[(r * grid.Columns) + c];
                    if (bank.Biome != Biome.Sea && bank.Elevation <= region.Elevation + rules.FloodBankHeight)
                    {
                        Deepen(depths, bank.Id, 0.5f * depth);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A wave from the water (ADR-022): every land region within <paramref name="radius"/> of the
    /// target lying less than <paramref name="height"/> metres above the sea floods, the lowest
    /// deepest.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="target">Where the wave strikes.</param>
    /// <param name="radius">How far it reaches, in metres.</param>
    /// <param name="height">How high it rises, in metres.</param>
    /// <returns>The flooded region where the water lies deepest, or <c>null</c> when none flooded.</returns>
    public static int? Inundate(WorldState world, GroundPosition target, float radius, float height)
    {
        ArgumentNullException.ThrowIfNull(world);
        int? deepest = null;
        var deepestFlood = 0f;
        foreach (var region in Reach(world.Regions, target, radius, height))
        {
            var flood = 1f - (Math.Max(0f, region.Elevation) / height);
            region.Flood = Math.Max(region.Flood, flood);
            if (flood > deepestFlood)
            {
                deepestFlood = flood;
                deepest = region.Id;
            }
        }

        return deepest;
    }

    /// <summary>The land regions a wave of a height would flood around a target.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="target">Where the wave strikes.</param>
    /// <param name="radius">How far it reaches, in metres.</param>
    /// <param name="height">How high it rises, in metres.</param>
    /// <returns>The regions, in id order.</returns>
    public static List<RegionState> Reach(RegionGrid grid, GroundPosition target, float radius, float height)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var reached = new List<RegionState>();
        foreach (var region in grid.Cells)
        {
            var centre = grid.GetRegionCenter(region.Id);
            var dx = centre.X - target.X;
            var dz = centre.Z - target.Z;
            if (region.Biome != Biome.Sea && region.Elevation < height && MathF.Sqrt((dx * dx) + (dz * dz)) <= radius)
            {
                reached.Add(region);
            }
        }

        return reached;
    }

    /// <summary>The river whose water reaches a region, if any: the first that runs through it.</summary>
    /// <param name="water">The world's water.</param>
    /// <param name="regionId">The region.</param>
    /// <returns>The river, or <c>null</c>.</returns>
    public static River? RiverAt(WaterState water, int regionId)
    {
        ArgumentNullException.ThrowIfNull(water);
        foreach (var river in water.Rivers)
        {
            if (river.Regions.Contains(regionId))
            {
                return river;
            }
        }

        return null;
    }

    /// <summary>Keeps the deeper of two floods for a region.</summary>
    private static void Deepen(Dictionary<int, float> depths, int regionId, float depth)
    {
        depths[regionId] = depths.TryGetValue(regionId, out var current) ? Math.Max(current, depth) : depth;
    }
}
