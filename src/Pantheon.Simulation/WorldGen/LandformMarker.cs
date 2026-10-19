using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Marks the biomes that place decides on the terrain samples (spec §5, ADR-038), after the climate
/// biomes and the rivers, in order of precedence:
/// <list type="number">
/// <item>islands: every land mass but the largest that is smaller than <c>IslandMaxArea</c>;</item>
/// <item>volcanic land around the island's highest land region, when it rises high enough to be the volcano;</item>
/// <item>river valleys: low land near fresh water;</item>
/// <item>coasts: low land near the sea.</item>
/// </list>
/// Sea is never replaced. A pure function of the terrain, so the client's terrain matches the world's.
/// </summary>
public static class LandformMarker
{
    /// <summary>Marks the landform biomes in place.</summary>
    /// <param name="settings">Landform settings.</param>
    /// <param name="regionsPerSide">Regions per side, to find the volcano's region as the world will.</param>
    /// <param name="resolution">Samples per side.</param>
    /// <param name="spacing">Metres between samples.</param>
    /// <param name="heights">Sample heights.</param>
    /// <param name="hydrology">Rivers and lakes.</param>
    /// <param name="biomes">Sample biomes, changed in place.</param>
    public static void Mark(LandformSettings settings, int regionsPerSide, int resolution, float spacing, float[] heights, HydrologyMap hydrology, Biome[] biomes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(heights);
        ArgumentNullException.ThrowIfNull(hydrology);
        ArgumentNullException.ThrowIfNull(biomes);
        var marked = new bool[biomes.Length];
        MarkIslands(settings, resolution, spacing, biomes, marked);
        MarkVolcanic(settings, regionsPerSide, resolution, spacing, heights, biomes, marked);
        var surface = hydrology.WaterSurface.ToArray();
        var nearWater = Within(resolution, Steps(settings.ValleyMetres, spacing), i => !float.IsNaN(surface[i]) && surface[i] > heights[i] && biomes[i] != Biome.Sea, biomes);
        Replace(biomes, marked, i => nearWater[i] && heights[i] <= settings.ValleyMaxHeight, settings.ValleyOver, Biome.RiverValley);
        var nearSea = Within(resolution, Steps(settings.CoastMetres, spacing), i => biomes[i] == Biome.Sea, biomes);
        Replace(biomes, marked, i => nearSea[i] && heights[i] <= settings.CoastMaxHeight, settings.CoastOver, Biome.Coast);
    }

    /// <summary>
    /// Whether a region of the grid is land as <see cref="RegionBuilder"/> decides it when landforms
    /// are marked: when its land samples are at least as many as its sea samples.
    /// </summary>
    /// <param name="sea">Its sea samples.</param>
    /// <param name="land">Its land samples.</param>
    /// <returns><c>true</c> for land.</returns>
    public static bool IsLand(int sea, int land) => land >= sea;

    /// <summary>Every land mass but the largest that is small enough becomes an island.</summary>
    private static void MarkIslands(LandformSettings settings, int resolution, float spacing, Biome[] biomes, bool[] marked)
    {
        var label = new int[biomes.Length];
        Array.Fill(label, -1);
        var sizes = new List<int>();
        var stack = new Stack<int>();
        for (var start = 0; start < biomes.Length; start++)
        {
            if (biomes[start] == Biome.Sea || label[start] >= 0)
            {
                continue;
            }

            var id = sizes.Count;
            var size = 0;
            label[start] = id;
            stack.Push(start);
            while (stack.Count > 0)
            {
                var cell = stack.Pop();
                size++;
                var (x, z) = (cell % resolution, cell / resolution);
                Visit(x + 1, z);
                Visit(x - 1, z);
                Visit(x, z + 1);
                Visit(x, z - 1);
            }

            sizes.Add(size);

            void Visit(int x, int z)
            {
                if (x < 0 || z < 0 || x >= resolution || z >= resolution)
                {
                    return;
                }

                var next = (z * resolution) + x;
                if (biomes[next] != Biome.Sea && label[next] < 0)
                {
                    label[next] = id;
                    stack.Push(next);
                }
            }
        }

        var largest = -1;
        for (var i = 0; i < sizes.Count; i++)
        {
            if (largest < 0 || sizes[i] > sizes[largest])
            {
                largest = i;
            }
        }

        var cellArea = spacing * spacing;
        for (var i = 0; i < biomes.Length; i++)
        {
            if (label[i] >= 0 && label[i] != largest && sizes[label[i]] * cellArea < settings.IslandMaxArea)
            {
                biomes[i] = Biome.Island;
                marked[i] = true;
            }
        }
    }

    /// <summary>The land within <c>VolcanicMetres</c> of the centre of the highest land region, if it is high enough to be the volcano.</summary>
    private static void MarkVolcanic(LandformSettings settings, int regionsPerSide, int resolution, float spacing, float[] heights, Biome[] biomes, bool[] marked)
    {
        var size = resolution / regionsPerSide;
        var best = -1;
        var bestElevation = float.MinValue;
        for (var row = 0; row < regionsPerSide; row++)
        {
            for (var column = 0; column < regionsPerSide; column++)
            {
                double sum = 0;
                var (sea, land) = (0, 0);
                for (var z = row * size; z < (row + 1) * size; z++)
                {
                    for (var x = column * size; x < (column + 1) * size; x++)
                    {
                        var index = (z * resolution) + x;
                        sum += heights[index];
                        if (biomes[index] == Biome.Sea)
                        {
                            sea++;
                        }
                        else
                        {
                            land++;
                        }
                    }
                }

                var elevation = (float)(sum / (size * size));
                if (IsLand(sea, land) && (best < 0 || elevation > bestElevation))
                {
                    (best, bestElevation) = ((row * regionsPerSide) + column, elevation);
                }
            }
        }

        if (best < 0 || bestElevation < settings.VolcanoMinElevation)
        {
            return;
        }

        var centreX = ((best % regionsPerSide) + 0.5f) * size * spacing;
        var centreZ = ((best / regionsPerSide) + 0.5f) * size * spacing;

        // The ash lies in lobes, not a circle: its reach swells and shrinks around the crater.
        var (phase, twist) = (best * 1.7f, best * 2.9f);
        for (var i = 0; i < biomes.Length; i++)
        {
            var dx = ((i % resolution) * spacing) - centreX;
            var dz = ((i / resolution) * spacing) - centreZ;
            var angle = MathF.Atan2(dz, dx);
            var reach = settings.VolcanicMetres * (1f + (0.18f * MathF.Sin((3f * angle) + phase)) + (0.1f * MathF.Sin((5f * angle) + twist)));
            if (biomes[i] != Biome.Sea && !marked[i] && (dx * dx) + (dz * dz) <= reach * reach)
            {
                biomes[i] = Biome.Volcanic;
                marked[i] = true;
            }
        }
    }

    /// <summary>Replaces the listed biomes where a condition holds, on samples no landform has taken yet.</summary>
    private static void Replace(Biome[] biomes, bool[] marked, Func<int, bool> condition, IReadOnlyList<Biome> over, Biome landform)
    {
        var replaceable = new HashSet<Biome>(over);
        for (var i = 0; i < biomes.Length; i++)
        {
            if (!marked[i] && replaceable.Contains(biomes[i]) && condition(i))
            {
                biomes[i] = landform;
                marked[i] = true;
            }
        }
    }

    /// <summary>The samples within a number of steps (8-neighbour) of a source, sources included.</summary>
    private static bool[] Within(int resolution, int steps, Func<int, bool> isSource, Biome[] biomes)
    {
        var distance = new int[biomes.Length];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();
        for (var i = 0; i < biomes.Length; i++)
        {
            if (isSource(i))
            {
                distance[i] = 0;
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (distance[cell] >= steps)
            {
                continue;
            }

            var (x, z) = (cell % resolution, cell / resolution);
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var (nx, nz) = (x + dx, z + dz);
                    if (nx < 0 || nz < 0 || nx >= resolution || nz >= resolution)
                    {
                        continue;
                    }

                    var next = (nz * resolution) + nx;
                    if (distance[next] == int.MaxValue)
                    {
                        distance[next] = distance[cell] + 1;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        var within = new bool[biomes.Length];
        for (var i = 0; i < within.Length; i++)
        {
            within[i] = distance[i] <= steps;
        }

        return within;
    }

    /// <summary>Metres in whole samples.</summary>
    private static int Steps(float metres, float spacing) => Math.Max(0, (int)MathF.Round(metres / spacing));
}
