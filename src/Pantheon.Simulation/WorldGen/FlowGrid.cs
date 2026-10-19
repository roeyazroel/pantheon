using System;
using System.Collections.Generic;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// How water runs over a coarse grid of the terrain (ADR-022). A priority flood from the sea and the
/// map's edge raises every hollow to the level it spills at, so each cell drains through its
/// <see cref="Receivers"/> to the sea; <see cref="Accumulated"/> is the wet land draining through
/// each cell.
/// </summary>
internal sealed class FlowGrid
{
    // Each filled cell lies this far above the cell it drains into, so flats drain too.
    private const float FillEpsilon = 0.001f;

    private static readonly (int Dx, int Dz)[] Neighbours =
    [
        (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
    ];

    private FlowGrid(int size, float cellSize, float[] heights, float[] filled, int[] receivers, int[] order, float[] accumulated)
    {
        Size = size;
        CellSize = cellSize;
        Heights = heights;
        Filled = filled;
        Receivers = receivers;
        Order = order;
        Accumulated = accumulated;
    }

    /// <summary>Cells per side.</summary>
    public int Size { get; }

    /// <summary>Side of a cell in metres.</summary>
    public float CellSize { get; }

    /// <summary>Mean terrain height of each cell, row-major.</summary>
    public float[] Heights { get; }

    /// <summary>Height of each cell with its hollow filled to the level it spills at.</summary>
    public float[] Filled { get; }

    /// <summary>The cell each cell drains into, or -1 for the sea and the map's edge.</summary>
    public int[] Receivers { get; }

    /// <summary>Every cell, each after the cell it drains into.</summary>
    public int[] Order { get; }

    /// <summary>Wet land draining through each cell, in square metres of fully wet ground.</summary>
    public float[] Accumulated { get; }

    /// <summary>Whether a cell is sea.</summary>
    public bool IsSea(int cell) => Heights[cell] < 0f;

    /// <summary>The centre of a cell in world metres.</summary>
    public (float X, float Z) CentreOf(int cell) => (((cell % Size) + 0.5f) * CellSize, ((cell / Size) + 0.5f) * CellSize);

    /// <summary>Builds the flow grid by averaging blocks of height and moisture samples.</summary>
    /// <param name="heights">Height samples, row-major.</param>
    /// <param name="moisture">Moisture samples, row-major.</param>
    /// <param name="resolution">Samples per side.</param>
    /// <param name="spacing">Metres between samples.</param>
    /// <param name="factor">Samples per cell side; divides <paramref name="resolution"/>.</param>
    /// <returns>The routed grid.</returns>
    public static FlowGrid Route(float[] heights, float[] moisture, int resolution, float spacing, int factor)
    {
        var size = resolution / factor;
        var cellSize = factor * spacing;
        var coarse = Downsample(heights, resolution, factor);
        var wet = Downsample(moisture, resolution, factor);
        var filled = new float[coarse.Length];
        var receivers = new int[coarse.Length];
        var order = Fill(coarse, size, filled, receivers);
        var accumulated = Accumulate(coarse, wet, cellSize, receivers, order);
        return new FlowGrid(size, cellSize, coarse, filled, receivers, order, accumulated);
    }

    /// <summary>The cells next to a cell, diagonals included, clipped at the edge.</summary>
    public IEnumerable<int> NeighboursOf(int cell)
    {
        var x = cell % Size;
        var z = cell / Size;
        foreach (var (dx, dz) in Neighbours)
        {
            var nx = x + dx;
            var nz = z + dz;
            if (nx >= 0 && nz >= 0 && nx < Size && nz < Size)
            {
                yield return (nz * Size) + nx;
            }
        }
    }

    /// <summary>Mean of each block of samples.</summary>
    private static float[] Downsample(float[] samples, int resolution, int factor)
    {
        var size = resolution / factor;
        var result = new float[size * size];
        var weight = 1f / (factor * factor);
        for (var z = 0; z < resolution; z++)
        {
            for (var x = 0; x < resolution; x++)
            {
                result[((z / factor) * size) + (x / factor)] += samples[(z * resolution) + x] * weight;
            }
        }

        return result;
    }

    /// <summary>
    /// Priority flood (Barnes et al.): from the sea and the edge inwards, lowest first; each cell
    /// drains into the cell it was reached from and lies at least a little above it.
    /// </summary>
    /// <returns>The cells in the order they were reached.</returns>
    private static int[] Fill(float[] heights, int size, float[] filled, int[] receivers)
    {
        var queue = new PriorityQueue<int, (float Height, int Cell)>();
        var reached = new bool[heights.Length];
        for (var cell = 0; cell < heights.Length; cell++)
        {
            var x = cell % size;
            var z = cell / size;
            if (heights[cell] < 0f || x == 0 || z == 0 || x == size - 1 || z == size - 1)
            {
                reached[cell] = true;
                filled[cell] = heights[cell];
                receivers[cell] = -1;
                queue.Enqueue(cell, (filled[cell], cell));
            }
        }

        var order = new int[heights.Length];
        var count = 0;
        while (queue.TryDequeue(out var cell, out _))
        {
            order[count++] = cell;
            var x = cell % size;
            var z = cell / size;
            foreach (var (dx, dz) in Neighbours)
            {
                var nx = x + dx;
                var nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= size || nz >= size)
                {
                    continue;
                }

                var next = (nz * size) + nx;
                if (reached[next])
                {
                    continue;
                }

                reached[next] = true;
                filled[next] = MathF.Max(heights[next], filled[cell] + FillEpsilon);
                receivers[next] = cell;
                queue.Enqueue(next, (filled[next], next));
            }
        }

        return order;
    }

    /// <summary>Each land cell's rain, times its moisture, passed down to the cells it drains through.</summary>
    private static float[] Accumulate(float[] heights, float[] wet, float cellSize, int[] receivers, int[] order)
    {
        var accumulated = new float[heights.Length];
        var area = cellSize * cellSize;
        for (var cell = 0; cell < heights.Length; cell++)
        {
            accumulated[cell] = heights[cell] >= 0f ? area * Math.Clamp(wet[cell], 0f, 1f) : 0f;
        }

        for (var i = order.Length - 1; i >= 0; i--)
        {
            var cell = order[i];
            if (receivers[cell] >= 0)
            {
                accumulated[receivers[cell]] += accumulated[cell];
            }
        }

        return accumulated;
    }
}
