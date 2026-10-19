using System;
using System.Collections.Generic;
using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Navigation;

namespace Pantheon.Simulation.Roads;

/// <summary>
/// Roads as pure functions (spec §29, ADR-033): whether one is open, the road between two villages
/// and its way, how fast a walker goes where it stands, and the neighbour a village builds to next.
/// </summary>
public static class RoadModel
{
    /// <summary>Whether a road is whole and not cut.</summary>
    /// <param name="rules">Road rules.</param>
    /// <param name="road">The road.</param>
    /// <returns><c>true</c> when it is open.</returns>
    public static bool IsOpen(RoadRules rules, RoadState road)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(road);
        return road.Built >= road.Length && road.Wear < rules.CutWear;
    }

    /// <summary>The road between two villages, built or being built, either way round.</summary>
    /// <param name="world">World state.</param>
    /// <param name="first">One village.</param>
    /// <param name="second">The other.</param>
    /// <returns>The road, or <c>null</c>.</returns>
    public static RoadState? Between(WorldState world, int first, int second)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Roads.Find(r => (r.FromSettlementId == first && r.ToSettlementId == second) || (r.FromSettlementId == second && r.ToSettlementId == first));
    }

    /// <summary>The way along the open road from one village to another, if there is one.</summary>
    /// <param name="rules">Road rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="from">The village left.</param>
    /// <param name="to">The village sought.</param>
    /// <returns>The way, ending at the village sought, or <c>null</c>.</returns>
    public static List<GroundPosition>? Way(RoadRules rules, WorldState world, int from, int to)
    {
        if (Between(world, from, to) is not { } road || !IsOpen(rules, road) || road.Path.Count == 0)
        {
            return null;
        }

        var way = new List<GroundPosition>(road.Path);
        if (road.FromSettlementId != from)
        {
            way.Reverse();
        }

        return way;
    }

    /// <summary>A walker's speed factor where they stand: the rules' speed on an open road, 1 elsewhere.</summary>
    /// <param name="rules">Road rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="position">Where the walker is.</param>
    /// <returns>The factor.</returns>
    public static float SpeedAt(RoadRules rules, WorldState world, GroundPosition position)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (!rules.Enabled)
        {
            return 1f;
        }

        foreach (var road in world.Roads)
        {
            if (IsOpen(rules, road) && Near(road.Path, position, rules.Width))
            {
                return rules.Speed;
            }
        }

        return 1f;
    }

    /// <summary>
    /// The neighbour a village builds its next road to: the nearest within range with no road
    /// between them, reachable over land and round the lakes, and the way there; <c>null</c> when
    /// there is none.
    /// </summary>
    /// <param name="rules">Road rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The neighbour and the way, or <c>null</c>.</returns>
    public static (SettlementState Neighbour, List<GroundPosition> Path)? Plan(RoadRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        SettlementState? nearest = null;
        var best = float.MaxValue;
        foreach (var other in world.Settlements)
        {
            var distance = Distance(settlement.Position, other.Position);
            if (other.Id != settlement.Id && other.Population > 0 && distance <= rules.Range && distance < best && Between(world, settlement.Id, other.Id) is null)
            {
                (nearest, best) = (other, distance);
            }
        }

        // A road goes round a lake, and over a river by a ford.
        var lakes = new HashSet<int>();
        foreach (var lake in world.Water.Lakes)
        {
            lakes.UnionWith(lake.Regions);
        }

        if (nearest is null || RegionPathfinder.FindPath(world.Regions, settlement.Position, nearest.Position, r => RegionPathfinder.IsWalkable(r) && !lakes.Contains(r.Id)) is not { Count: > 0 } way)
        {
            return null;
        }

        var path = new List<GroundPosition>(way.Count + 1) { settlement.Position };
        path.AddRange(way);
        return (nearest, path);
    }

    /// <summary>A way's length in metres.</summary>
    /// <param name="path">The way.</param>
    /// <returns>Its length.</returns>
    public static float Length(IReadOnlyList<GroundPosition> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var length = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            length += Distance(path[i - 1], path[i]);
        }

        return length;
    }

    /// <summary>Whether a point lies within a distance of a way.</summary>
    private static bool Near(List<GroundPosition> path, GroundPosition point, float width)
    {
        for (var i = 1; i < path.Count; i++)
        {
            if (SegmentDistance(path[i - 1], path[i], point) <= width)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The distance from a point to a segment.</summary>
    private static float SegmentDistance(GroundPosition a, GroundPosition b, GroundPosition p)
    {
        var (dx, dz) = (b.X - a.X, b.Z - a.Z);
        var lengthSquared = (dx * dx) + (dz * dz);
        var t = lengthSquared <= 0f ? 0f : Math.Clamp((((p.X - a.X) * dx) + ((p.Z - a.Z) * dz)) / lengthSquared, 0f, 1f);
        return Distance(new GroundPosition(a.X + (t * dx), a.Z + (t * dz)), p);
    }

    /// <summary>The distance between two points.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var (dx, dz) = (a.X - b.X, a.Z - b.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
