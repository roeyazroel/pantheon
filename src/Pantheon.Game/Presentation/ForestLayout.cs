using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>The shape of a tree drawn (ADR-033).</summary>
public enum TreeKind
{
    /// <summary>A conifer: tiers of cones, mostly in forests and on the heights.</summary>
    Conifer,

    /// <summary>A broadleaf: a round crown, mostly on grassland.</summary>
    Broadleaf,
}

/// <summary>One tree drawn (ADR-033).</summary>
/// <param name="Position">Where it stands, on the ground.</param>
/// <param name="Scale">Its size, around 1.</param>
/// <param name="Yaw">Its turn about the vertical, in radians.</param>
/// <param name="Kind">Its shape.</param>
/// <param name="Shade">How light its leaves are, around 1.</param>
public readonly record struct TreePlacement(Vector3 Position, float Scale, float Yaw, TreeKind Kind, float Shade);

/// <summary>Ground kept clear of trees: a village's houses and fields (ADR-033).</summary>
/// <param name="Center">Its centre.</param>
/// <param name="Radius">Its radius in metres.</param>
public readonly record struct Clearing(Vector2 Center, float Radius);

/// <summary>
/// Where the trees stand (spec §93, ADR-033): each region shows trees in proportion to its standing
/// timber, at places drawn from the seed and the region, never in the sea or a river, in a village's
/// clearing or on a road. Cutting a forest takes its last trees first, so the rest stand still.
/// Presentation only: derived from the world, never saved.
/// </summary>
public static class ForestLayout
{
    /// <summary>Ground each tree of a whole forest takes, in square metres.</summary>
    public const float SquareMetresPerTree = 120f;

    /// <summary>How far trees keep from a road's middle, in metres.</summary>
    public const float RoadClearance = 5f;

    /// <summary>How far trees keep beyond a village's houses and fields, in metres.</summary>
    public const float VillageMargin = 8f;

    /// <summary>How many trees a region shows for its timber.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="region">The region.</param>
    /// <returns>The trees.</returns>
    public static int TreeCount(RegionGrid grid, RegionState region)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(region);
        return region.Biome == Biome.Sea ? 0 : (int)MathF.Round(Math.Clamp(region.Timber, 0f, 1f) * grid.RegionSize * grid.RegionSize / SquareMetresPerTree);
    }

    /// <summary>The clearing around a village's site: its farthest house or field, and a margin.</summary>
    /// <param name="site">The village's site.</param>
    /// <param name="fieldSize">A field's side in metres.</param>
    /// <returns>The clearing.</returns>
    public static Clearing ClearingOf(SettlementSite site, float fieldSize)
    {
        ArgumentNullException.ThrowIfNull(site);
        var center = new Vector2(site.Center.X, site.Center.Z);
        var radius = site.HouseDiscRadius;
        foreach (var house in site.Houses)
        {
            radius = Math.Max(radius, Vector2.Distance(center, new Vector2(house.Position.X, house.Position.Z)));
        }

        foreach (var field in site.Fields)
        {
            radius = Math.Max(radius, Vector2.Distance(center, new Vector2(field.Position.X, field.Position.Z)) + (fieldSize * 0.75f));
        }

        return new Clearing(center, radius + VillageMargin);
    }

    /// <summary>Adds a region's trees.</summary>
    /// <param name="world">World state, for the seed, the grid and the roads.</param>
    /// <param name="terrain">Terrain, for the ground's height and fresh water.</param>
    /// <param name="region">The region.</param>
    /// <param name="clearings">The villages' clearings.</param>
    /// <param name="into">Receives the trees.</param>
    public static void Place(WorldState world, TerrainField terrain, RegionState region, IReadOnlyList<Clearing> clearings, List<TreePlacement> into)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(clearings);
        ArgumentNullException.ThrowIfNull(into);
        var grid = world.Regions;
        var (column, row) = grid.GetCoordinates(region.Id);
        var size = grid.RegionSize;
        var seed = world.Seed.Value;
        var conifers = region.Biome is Biome.Forest or Biome.Highland ? 0.8f : 0.2f;
        var count = TreeCount(grid, region);
        for (var slot = 0; slot < count; slot++)
        {
            var key = (ulong)region.Id;
            var x = (column + PresentationHash.Unit(seed, key, (ulong)slot, 1)) * size;
            var z = (row + PresentationHash.Unit(seed, key, (ulong)slot, 2)) * size;
            var height = terrain.SampleHeight(x, z);
            if (height < 0.5f || terrain.IsUnderFreshWater(x, z) || InClearing(clearings, x, z) || OnRoad(world.Roads, x, z))
            {
                continue;
            }

            var kind = PresentationHash.Unit(seed, key, (ulong)slot, 3) < conifers ? TreeKind.Conifer : TreeKind.Broadleaf;
            into.Add(new TreePlacement(
                new Vector3(x, height, z),
                0.8f + (0.55f * PresentationHash.Unit(seed, key, (ulong)slot, 4)),
                MathF.Tau * PresentationHash.Unit(seed, key, (ulong)slot, 5),
                kind,
                0.85f + (0.25f * PresentationHash.Unit(seed, key, (ulong)slot, 6))));
        }
    }

    /// <summary>Whether a point lies in a village's clearing.</summary>
    private static bool InClearing(IReadOnlyList<Clearing> clearings, float x, float z)
    {
        foreach (var clearing in clearings)
        {
            if (Vector2.DistanceSquared(clearing.Center, new Vector2(x, z)) < clearing.Radius * clearing.Radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a point lies on a road, built or being built.</summary>
    private static bool OnRoad(List<RoadState> roads, float x, float z)
    {
        var point = new Vector2(x, z);
        foreach (var road in roads)
        {
            for (var i = 1; i < road.Path.Count; i++)
            {
                var a = new Vector2(road.Path[i - 1].X, road.Path[i - 1].Z);
                var b = new Vector2(road.Path[i].X, road.Path[i].Z);
                var segment = b - a;
                var t = segment.LengthSquared() <= 0f ? 0f : Math.Clamp(Vector2.Dot(point - a, segment) / segment.LengthSquared(), 0f, 1f);
                if (Vector2.DistanceSquared(a + (t * segment), point) < RoadClearance * RoadClearance)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
