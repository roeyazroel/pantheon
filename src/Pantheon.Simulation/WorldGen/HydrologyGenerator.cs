using System;
using System.Collections.Generic;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Finds the rivers and lakes of a generated heightfield and cuts them into it (spec §5, §93,
/// ADR-022). Hollows deep enough hold lakes; where enough wet land drains through a cell a river
/// runs, the largest from the sea upstream, then tributaries joining them. A pure function of the
/// seed, the settings and the terrain.
/// </summary>
internal static class HydrologyGenerator
{
    // Cells of one hollow fill to its surface within this many metres (the flood's slope on flats).
    private const float BasinTolerance = 0.25f;

    // Directions around a river point in which the valley floor is sought.
    private const int ValleyDirections = 8;

    /// <summary>Finds the water, carves the channels into <paramref name="heights"/> and returns the map.</summary>
    /// <param name="seed">World seed, for the names.</param>
    /// <param name="settings">Hydrology settings.</param>
    /// <param name="resolution">Height samples per side.</param>
    /// <param name="spacing">Metres between samples.</param>
    /// <param name="heights">Height samples; the channels are cut into them.</param>
    /// <param name="moisture">Moisture samples.</param>
    /// <returns>The world's fresh water.</returns>
    public static HydrologyMap Generate(WorldSeed seed, HydrologySettings settings, int resolution, float spacing, float[] heights, float[] moisture)
    {
        var factor = CellFactor(settings, spacing);
        var grid = FlowGrid.Route(heights, moisture, resolution, spacing, factor);
        var lakeCells = FindLakes(grid, settings, out var lakeSurfaces);
        var paths = TraceRivers(grid, settings);
        var riverNames = Shuffled(seed, "hydrology.rivers", settings.RiverNames);
        var lakeNames = Shuffled(seed, "hydrology.lakes", settings.LakeNames);

        var original = (float[])heights.Clone();
        var field = new TerrainField(resolution, spacing, original, moisture, new float[original.Length], new Domain.Ecology.Biome[original.Length]);
        var rivers = BuildCourses(grid, settings, paths, lakeCells, lakeSurfaces, field, riverNames);
        var lakes = new List<LakeBasin>();
        for (var i = 0; i < lakeSurfaces.Count && i < lakeNames.Count; i++)
        {
            var cells = new List<GroundPosition>();
            for (var cell = 0; cell < lakeCells.Length; cell++)
            {
                if (lakeCells[cell] == i)
                {
                    var (x, z) = grid.CentreOf(cell);
                    cells.Add(new GroundPosition(x, z));
                }
            }

            lakes.Add(new LakeBasin(lakeNames[i], lakeSurfaces[i], cells, grid.CellSize));
        }

        var surface = new float[heights.Length];
        Array.Fill(surface, float.NaN);
        ChannelCarver.Carve(settings, field, heights, surface, rivers, lakes);
        return new HydrologyMap(surface, rivers, lakes);
    }

    /// <summary>Height samples per side of a flow-grid cell: the cell size in samples, at least one.</summary>
    /// <param name="settings">Hydrology settings.</param>
    /// <param name="spacing">Metres between samples.</param>
    /// <returns>The factor.</returns>
    public static int CellFactor(HydrologySettings settings, float spacing) => Math.Max(1, (int)MathF.Round(settings.CellSize / spacing));

    /// <summary>
    /// Hollows that would hold water deeper than <see cref="HydrologySettings.MinLakeDepth"/> over
    /// at least <see cref="HydrologySettings.MinLakeArea"/>: each becomes a lake whose surface is the
    /// level it spills at. Largest first.
    /// </summary>
    /// <returns>Per cell, the index of its lake, or -1.</returns>
    private static int[] FindLakes(FlowGrid grid, HydrologySettings settings, out List<float> surfaces)
    {
        var label = new int[grid.Heights.Length];
        Array.Fill(label, -1);
        var components = new List<(List<int> Cells, float Surface)>();
        var minCells = (int)MathF.Ceiling(settings.MinLakeArea / (grid.CellSize * grid.CellSize));
        var seen = new bool[label.Length];
        for (var start = 0; start < label.Length; start++)
        {
            if (seen[start] || !IsDeep(grid, settings, start))
            {
                continue;
            }

            var cells = new List<int>();
            var queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            var surface = float.MaxValue;
            while (queue.TryDequeue(out var cell))
            {
                cells.Add(cell);
                surface = MathF.Min(surface, grid.Filled[cell]);
                foreach (var next in grid.NeighboursOf(cell))
                {
                    if (!seen[next] && IsDeep(grid, settings, next))
                    {
                        seen[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }

            if (cells.Count >= minCells)
            {
                components.Add((Basin(grid, cells, surface, seen), surface));
            }
        }

        components.Sort(static (a, b) => a.Cells.Count != b.Cells.Count ? b.Cells.Count.CompareTo(a.Cells.Count) : a.Cells[0].CompareTo(b.Cells[0]));
        surfaces = [];
        for (var i = 0; i < components.Count; i++)
        {
            surfaces.Add(components[i].Surface);
            foreach (var cell in components[i].Cells)
            {
                label[cell] = i;
            }
        }

        return label;
    }

    /// <summary>
    /// The whole hollow around a lake's deep water: every cell joined to it whose ground lies below
    /// the lake's surface and which fills to that surface, the shallow margins included.
    /// </summary>
    private static List<int> Basin(FlowGrid grid, List<int> deep, float surface, bool[] seen)
    {
        var basin = new List<int>(deep);
        var queue = new Queue<int>(deep);
        while (queue.TryDequeue(out var cell))
        {
            foreach (var next in grid.NeighboursOf(cell))
            {
                if (!seen[next] && !grid.IsSea(next) && grid.Heights[next] < surface && MathF.Abs(grid.Filled[next] - surface) < BasinTolerance)
                {
                    seen[next] = true;
                    basin.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return basin;
    }

    /// <summary>Whether a land cell lies deep enough below the level its hollow spills at.</summary>
    private static bool IsDeep(FlowGrid grid, HydrologySettings settings, int cell) =>
        !grid.IsSea(cell) && grid.Filled[cell] - grid.Heights[cell] > settings.MinLakeDepth;

    /// <summary>
    /// The rivers as paths of cells: main rivers from the sea upstream, largest first, then
    /// tributaries from where they join a river already found, until <see cref="HydrologySettings.MaxRivers"/>.
    /// </summary>
    private static List<(List<int> Cells, int? Joins)> TraceRivers(FlowGrid grid, HydrologySettings settings)
    {
        var threshold = settings.RiverCatchment;
        var headwater = MathF.Min(settings.HeadwaterCatchment, threshold);
        var riverOf = new int[grid.Heights.Length];
        Array.Fill(riverOf, -1);
        var rivers = new List<(List<int> Cells, int? Joins)>();

        var mouths = new List<int>();
        for (var cell = 0; cell < grid.Heights.Length; cell++)
        {
            var receiver = grid.Receivers[cell];
            if (!grid.IsSea(cell) && grid.Accumulated[cell] >= threshold && (receiver < 0 || grid.IsSea(receiver)))
            {
                mouths.Add(cell);
            }
        }

        // The longest rivers first: on an island the largest basins are not always the longest.
        var mains = new List<List<int>>();
        var scratch = new int[riverOf.Length];
        foreach (var mouth in mouths)
        {
            Array.Fill(scratch, -1);
            var path = TraceUpstream(grid, mouth, headwater, scratch);
            if (grid.Receivers[mouth] >= 0)
            {
                path.Add(grid.Receivers[mouth]);
            }

            mains.Add(path);
        }

        mains.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : a[^1].CompareTo(b[^1]));
        foreach (var path in mains)
        {
            if (rivers.Count >= settings.MaxMainRivers)
            {
                break;
            }

            Keep(grid, settings, rivers, riverOf, path, joins: null);
        }

        while (rivers.Count < settings.MaxRivers && FindTributary(grid, threshold, riverOf) is { } start)
        {
            var junction = grid.Receivers[start];
            var path = TraceUpstream(grid, start, headwater, riverOf);
            path.Add(junction);
            if (!Keep(grid, settings, rivers, riverOf, path, riverOf[junction]))
            {
                // Too short to keep: mark it so the search moves on.
                foreach (var cell in path)
                {
                    if (riverOf[cell] < 0)
                    {
                        riverOf[cell] = int.MaxValue;
                    }
                }
            }
        }

        return rivers;
    }

    /// <summary>Keeps a path as a river when it is long enough, marking its cells.</summary>
    private static bool Keep(FlowGrid grid, HydrologySettings settings, List<(List<int> Cells, int? Joins)> rivers, int[] riverOf, List<int> path, int? joins)
    {
        if (path.Count < 2 || Length(grid, path) < settings.MinRiverLength)
        {
            return false;
        }

        var index = rivers.Count;
        foreach (var cell in path)
        {
            if (riverOf[cell] < 0 || riverOf[cell] == int.MaxValue)
            {
                riverOf[cell] = index;
            }
        }

        rivers.Add((path, joins));
        return true;
    }

    /// <summary>The largest cell not yet on a river that drains straight into one and carries a river's water, if any.</summary>
    private static int? FindTributary(FlowGrid grid, float threshold, int[] riverOf)
    {
        int? best = null;
        for (var cell = 0; cell < grid.Heights.Length; cell++)
        {
            var receiver = grid.Receivers[cell];
            if (riverOf[cell] >= 0 || grid.IsSea(cell) || grid.Accumulated[cell] < threshold || receiver < 0 || riverOf[receiver] < 0 || riverOf[receiver] == int.MaxValue)
            {
                continue;
            }

            if (best is not { } current || grid.Accumulated[cell] > grid.Accumulated[current])
            {
                best = cell;
            }
        }

        return best;
    }

    /// <summary>From a cell upstream along the wettest inflow that carries a river's water; returned source first.</summary>
    private static List<int> TraceUpstream(FlowGrid grid, int start, float threshold, int[] riverOf)
    {
        var path = new List<int> { start };
        var current = start;
        while (true)
        {
            var next = -1;
            foreach (var neighbour in grid.NeighboursOf(current))
            {
                if (grid.Receivers[neighbour] == current && riverOf[neighbour] < 0 && grid.Accumulated[neighbour] >= threshold
                    && (next < 0 || grid.Accumulated[neighbour] > grid.Accumulated[next]))
                {
                    next = neighbour;
                }
            }

            if (next < 0)
            {
                break;
            }

            path.Add(next);
            current = next;
        }

        path.Reverse();
        return path;
    }

    /// <summary>Length of a path of cells in metres.</summary>
    private static float Length(FlowGrid grid, List<int> path)
    {
        var length = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var (ax, az) = grid.CentreOf(path[i - 1]);
            var (bx, bz) = grid.CentreOf(path[i]);
            length += MathF.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
        }

        return length;
    }

    /// <summary>
    /// Turns paths of cells into smoothed courses: width and depth grow with the water a point
    /// carries, and the surface follows the valley floor down, never rising towards the mouth.
    /// </summary>
    private static List<RiverCourse> BuildCourses(FlowGrid grid, HydrologySettings settings, List<(List<int> Cells, int? Joins)> paths, int[] lakeCells, List<float> lakeSurfaces, TerrainField field, List<string> names)
    {
        var largest = settings.RiverCatchment;
        foreach (var (cells, _) in paths)
        {
            foreach (var cell in cells)
            {
                largest = MathF.Max(largest, grid.Accumulated[cell]);
            }
        }

        var courses = new List<RiverCourse>(paths.Count);
        for (var r = 0; r < paths.Count; r++)
        {
            var (cells, joins) = paths[r];
            var positions = Smooth(grid, cells, settings.SmoothingPasses);
            var points = new List<RiverPoint>(cells.Count);
            var surface = float.MaxValue;
            for (var i = 0; i < cells.Count; i++)
            {
                var smallest = MathF.Min(settings.HeadwaterCatchment, settings.RiverCatchment);
                var growth = MathF.Sqrt(SimMath.Clamp01((grid.Accumulated[cells[i]] - smallest) / MathF.Max(1f, largest - smallest)));
                var width = SimMath.Lerp(settings.MinChannelWidth, settings.MaxChannelWidth, growth);
                var depth = SimMath.Lerp(settings.MinChannelDepth, settings.MaxChannelDepth, growth);
                var (x, z) = positions[i];
                var ground = ValleyFloor(field, x, z, (width * 0.5f) + (settings.BankWidth * 0.5f));
                var level = lakeCells[cells[i]] >= 0 ? lakeSurfaces[lakeCells[cells[i]]] : ground - settings.Freeboard;
                surface = MathF.Max(0f, MathF.Min(surface, level));
                points.Add(new RiverPoint(x, z, surface, width, depth));
            }

            courses.Add(new RiverCourse(names[r % names.Count], points, joins));
        }

        return courses;
    }

    /// <summary>
    /// Lowest ground at a point and around it at half and the whole of a radius, so the water lies
    /// below both banks even where the river runs along a slope.
    /// </summary>
    private static float ValleyFloor(TerrainField field, float x, float z, float radius)
    {
        var lowest = field.SampleHeight(x, z);
        for (var i = 0; i < ValleyDirections; i++)
        {
            var angle = i * MathF.Tau / ValleyDirections;
            var (sin, cos) = MathF.SinCos(angle);
            lowest = MathF.Min(lowest, field.SampleHeight(x + (cos * radius), z + (sin * radius)));
            lowest = MathF.Min(lowest, field.SampleHeight(x + (cos * radius * 0.5f), z + (sin * radius * 0.5f)));
        }

        return lowest;
    }

    /// <summary>Cell centres averaged with their neighbours along the path a few times; the ends stay put.</summary>
    private static (float X, float Z)[] Smooth(FlowGrid grid, List<int> cells, int passes)
    {
        var points = new (float X, float Z)[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            points[i] = grid.CentreOf(cells[i]);
        }

        for (var pass = 0; pass < passes; pass++)
        {
            var next = ((float X, float Z)[])points.Clone();
            for (var i = 1; i < points.Length - 1; i++)
            {
                next[i] = (
                    (points[i - 1].X + (2f * points[i].X) + points[i + 1].X) * 0.25f,
                    (points[i - 1].Z + (2f * points[i].Z) + points[i + 1].Z) * 0.25f);
            }

            points = next;
        }

        return points;
    }

    /// <summary>A name pool in an order drawn from the seed (Fisher–Yates).</summary>
    private static List<string> Shuffled(WorldSeed seed, string purpose, IReadOnlyList<string> pool)
    {
        var names = new List<string>(pool);
        var random = new SimulationRandom(SeedMixer.Derive(seed, purpose));
        for (var i = names.Count - 1; i > 0; i--)
        {
            var j = random.NextInt(i + 1);
            (names[i], names[j]) = (names[j], names[i]);
        }

        return names;
    }
}
