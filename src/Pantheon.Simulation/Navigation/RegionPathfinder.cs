using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Navigation;

/// <summary>
/// Strategic navigation over the region graph (spec §85): A* across land regions, eight
/// neighbours each, then shortened wherever a straight line stays on land. Deterministic: ties
/// break by insertion order, and no random numbers are drawn.
/// </summary>
public static class RegionPathfinder
{
    private const float Diagonal = 1.41421356f;

    // Straight-line checks sample the line this often, in regions.
    private const float LineSampleStep = 0.25f;

    /// <summary>Rubble from which a region cannot be walked through until it is cleared (spec §20's blocked roads, ADR-029).</summary>
    public const float BlockingRubble = 0.5f;

    /// <summary>Whether a land walker can pass through a region.</summary>
    /// <param name="region">The region.</param>
    /// <returns><c>true</c> for land not buried in rubble.</returns>
    public static bool IsWalkable(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Biome != Biome.Sea && region.Rubble < BlockingRubble;
    }

    /// <summary>
    /// Finds waypoints from one point to another over land. The start and goal may lie in the sea
    /// (a beach, a creature wading out); the path between them crosses land only.
    /// </summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="from">Start point.</param>
    /// <param name="to">Goal point.</param>
    /// <returns>Waypoints after the start, ending at <paramref name="to"/>; <c>null</c> when no land path exists or a point is off the grid.</returns>
    public static List<GroundPosition>? FindPath(RegionGrid grid, GroundPosition from, GroundPosition to)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!grid.TryGetRegionIdAt(from.X, from.Z, out var start) || !grid.TryGetRegionIdAt(to.X, to.Z, out var goal))
        {
            return null;
        }

        if (IsLineWalkable(grid, from, to))
        {
            return [to];
        }

        var regions = Search(grid, start, goal);
        if (regions is null)
        {
            return null;
        }

        return Smooth(grid, from, to, regions);
    }

    /// <summary>A* from region to region; the start and goal regions are always allowed.</summary>
    private static List<int>? Search(RegionGrid grid, int start, int goal)
    {
        var cost = new Dictionary<int, float> { [start] = 0f };
        var cameFrom = new Dictionary<int, int>();
        var open = new PriorityQueue<int, float>();
        open.Enqueue(start, Heuristic(grid, start, goal));
        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
            {
                return Reconstruct(cameFrom, goal);
            }

            var (column, row) = grid.GetCoordinates(current);
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var c = column + dx;
                    var r = row + dz;
                    if ((dx == 0 && dz == 0) || c < 0 || r < 0 || c >= grid.Columns || r >= grid.Rows)
                    {
                        continue;
                    }

                    var next = (r * grid.Columns) + c;
                    if (next != goal && !IsWalkable(grid.Cells[next]))
                    {
                        continue;
                    }

                    var step = dx != 0 && dz != 0 ? Diagonal : 1f;
                    var newCost = cost[current] + step;
                    if (!cost.TryGetValue(next, out var known) || newCost < known)
                    {
                        cost[next] = newCost;
                        cameFrom[next] = current;
                        open.Enqueue(next, newCost + Heuristic(grid, next, goal));
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Octile distance in regions.</summary>
    private static float Heuristic(RegionGrid grid, int a, int b)
    {
        var (ca, ra) = grid.GetCoordinates(a);
        var (cb, rb) = grid.GetCoordinates(b);
        var dx = Math.Abs(ca - cb);
        var dz = Math.Abs(ra - rb);
        return Math.Max(dx, dz) + ((Diagonal - 1f) * Math.Min(dx, dz));
    }

    /// <summary>Region ids from the start to the goal.</summary>
    private static List<int> Reconstruct(Dictionary<int, int> cameFrom, int goal)
    {
        var path = new List<int> { goal };
        var current = goal;
        while (cameFrom.TryGetValue(current, out var previous))
        {
            path.Add(previous);
            current = previous;
        }

        path.Reverse();
        return path;
    }

    /// <summary>Region centres as waypoints, skipping those a straight walk over land can cut out.</summary>
    private static List<GroundPosition> Smooth(RegionGrid grid, GroundPosition from, GroundPosition to, List<int> regions)
    {
        var points = new List<GroundPosition>(regions.Count + 1);
        for (var i = 1; i < regions.Count - 1; i++)
        {
            points.Add(grid.GetRegionCenter(regions[i]));
        }

        points.Add(to);
        var result = new List<GroundPosition>();
        var anchor = from;
        var index = 0;
        while (index < points.Count)
        {
            var furthest = index;
            for (var j = points.Count - 1; j > index; j--)
            {
                if (IsLineWalkable(grid, anchor, points[j]))
                {
                    furthest = j;
                    break;
                }
            }

            result.Add(points[furthest]);
            anchor = points[furthest];
            index = furthest + 1;
        }

        return result;
    }

    /// <summary>Whether every region a straight line passes over (apart from its ends' regions) is land.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="a">One end.</param>
    /// <param name="b">The other end.</param>
    /// <returns><c>true</c> when the line stays on land.</returns>
    public static bool IsLineWalkable(RegionGrid grid, GroundPosition a, GroundPosition b)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.TryGetRegionIdAt(a.X, a.Z, out var startRegion);
        grid.TryGetRegionIdAt(b.X, b.Z, out var endRegion);
        var length = MathF.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Z - a.Z) * (b.Z - a.Z)));
        var samples = Math.Max(1, (int)MathF.Ceiling(length / (grid.RegionSize * LineSampleStep)));
        for (var i = 0; i <= samples; i++)
        {
            var t = (float)i / samples;
            var x = a.X + ((b.X - a.X) * t);
            var z = a.Z + ((b.Z - a.Z) * t);
            if (!grid.TryGetRegionIdAt(x, z, out var region))
            {
                return false;
            }

            if (region != startRegion && region != endRegion && !IsWalkable(grid.Cells[region]))
            {
                return false;
            }
        }

        return true;
    }
}
