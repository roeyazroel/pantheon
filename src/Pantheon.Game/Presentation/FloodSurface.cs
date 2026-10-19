using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Floodwater drawn as standing water risen over the land beside it (ADR-022): each point near a
/// river, lake or the sea floods from that water's level, by as much as the flood of the regions
/// around it (blended between their centres), so a flood follows the valley and slopes with the river.
/// </summary>
public sealed class FloodSurface
{
    private readonly TerrainField _field;
    private readonly int _step;
    private readonly int _size;
    private readonly float[] _bases;

    private FloodSurface(TerrainField field, int step, int size, float[] bases)
    {
        _field = field;
        _step = step;
        _size = size;
        _bases = bases;
    }

    /// <summary>
    /// Finds, for every point of a grid <paramref name="step"/> samples apart, the level of the
    /// nearest river, lake or sea within <paramref name="reach"/> metres: where a flood rises from.
    /// </summary>
    /// <param name="field">Terrain with its hydrology.</param>
    /// <param name="step">Samples between grid points.</param>
    /// <param name="reach">How far from water a flood spreads, in metres.</param>
    /// <returns>The flood surface.</returns>
    public static FloodSurface Build(TerrainField field, int step, float reach)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var size = ((field.Resolution - 1) / step) + 1;
        var bases = new float[size * size];
        Array.Fill(bases, float.NaN);
        var distance = new int[size * size];
        Array.Fill(distance, int.MaxValue);
        var queue = new Queue<int>();
        var surface = field.Hydrology.WaterSurface;
        for (var gz = 0; gz < size; gz++)
        {
            for (var gx = 0; gx < size; gx++)
            {
                var (x, z) = (gx * step, gz * step);
                var height = field.GetHeight(x, z);
                var water = surface[(z * field.Resolution) + x];
                var level = !float.IsNaN(water) && water > height ? water : height < 0f ? 0f : float.NaN;
                if (!float.IsNaN(level))
                {
                    var point = (gz * size) + gx;
                    bases[point] = level;
                    distance[point] = 0;
                    queue.Enqueue(point);
                }
            }
        }

        var limit = Math.Max(1, (int)MathF.Ceiling(reach / (step * field.Spacing)));
        while (queue.TryDequeue(out var point))
        {
            var next = distance[point] + 1;
            if (next > limit)
            {
                continue;
            }

            var (px, pz) = (point % size, point / size);
            Visit(px - 1, pz);
            Visit(px + 1, pz);
            Visit(px, pz - 1);
            Visit(px, pz + 1);

            void Visit(int x, int z)
            {
                if (x < 0 || z < 0 || x >= size || z >= size)
                {
                    return;
                }

                var neighbour = (z * size) + x;
                if (distance[neighbour] > next)
                {
                    distance[neighbour] = next;
                    bases[neighbour] = bases[point];
                    queue.Enqueue(neighbour);
                }
            }
        }

        return new FloodSurface(field, step, size, bases);
    }

    /// <summary>
    /// The floodwater for the regions' floods: at each point near water, the water's level plus
    /// <see cref="WaterSurfaceMesh.FloodClearance"/> and <paramref name="rise"/> metres at full
    /// flood, drawn where it covers the ground.
    /// </summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="floods">Flood per region id (0..1).</param>
    /// <param name="rise">Metres the water rises at full flood.</param>
    /// <returns>The mesh; empty when nothing is flooded.</returns>
    public WaterSurfaceMeshData Mesh(RegionGrid grid, IReadOnlyList<float> floods, float rise)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(floods);
        var positions = new List<Vector3>();
        var depths = new List<float>();
        var indices = new List<int>();
        Span<float> levels = stackalloc float[4];
        for (var gz = 0; gz < _size - 1; gz++)
        {
            for (var gx = 0; gx < _size - 1; gx++)
            {
                if (!Wet(grid, floods, gx, gz))
                {
                    continue;
                }

                var drawn = false;
                var land = false;
                var highest = float.MinValue;
                for (var corner = 0; corner < 4; corner++)
                {
                    var (cx, cz) = (gx + (corner & 1), gz + (corner >> 1));
                    var ground = _field.GetHeight(cx * _step, cz * _step);
                    land |= ground >= 0f;

                    // The sea does not flood: over it the floodwater runs down to the sea's own level.
                    levels[corner] = ground < 0f ? float.NaN : Level(grid, floods, rise, cx, cz);
                    if (!float.IsNaN(levels[corner]))
                    {
                        highest = MathF.Max(highest, levels[corner]);
                        drawn |= levels[corner] > ground;
                    }
                }

                if (!drawn || !land)
                {
                    continue;
                }

                var first = positions.Count;
                for (var corner = 0; corner < 4; corner++)
                {
                    var (sx, sz) = ((gx + (corner & 1)) * _step, (gz + (corner >> 1)) * _step);
                    var ground = _field.GetHeight(sx, sz);
                    var level = float.IsNaN(levels[corner]) ? MathF.Min(highest, MathF.Max(ground, 0f) - 0.05f) : levels[corner];
                    positions.Add(new Vector3(sx * _field.Spacing, level, sz * _field.Spacing));
                    depths.Add(Math.Clamp((level - ground) / WaterSurfaceMesh.DeepestDrawn, 0f, 1f));
                }

                indices.AddRange([first, first + 1, first + 2, first + 1, first + 3, first + 2]);
            }
        }

        return new WaterSurfaceMeshData([.. positions], [.. depths], [.. indices]);
    }

    /// <summary>The flood's level at a grid point, or NaN where no water lies near or nothing floods.</summary>
    private float Level(RegionGrid grid, IReadOnlyList<float> floods, float rise, int gx, int gz)
    {
        var level = _bases[(gz * _size) + gx];
        if (float.IsNaN(level))
        {
            return float.NaN;
        }

        var flood = FloodAt(grid, floods, gx * _step * _field.Spacing, gz * _step * _field.Spacing);
        return flood <= 0f ? float.NaN : level + WaterSurfaceMesh.FloodClearance + (rise * flood);
    }

    /// <summary>Whether the region of a grid cell, or one beside it, is flooded at all.</summary>
    private bool Wet(RegionGrid grid, IReadOnlyList<float> floods, int gx, int gz)
    {
        if (!grid.TryGetRegionIdAt(gx * _step * _field.Spacing, gz * _step * _field.Spacing, out var id))
        {
            return false;
        }

        var (column, row) = grid.GetCoordinates(id);
        for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
        {
            for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
            {
                if (floods[(r * grid.Columns) + c] > 0f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The regions' floods blended between their centres at a point.</summary>
    private static float FloodAt(RegionGrid grid, IReadOnlyList<float> floods, float x, float z)
    {
        var fx = Math.Clamp((x / grid.RegionSize) - 0.5f, 0f, grid.Columns - 1);
        var fz = Math.Clamp((z / grid.RegionSize) - 0.5f, 0f, grid.Rows - 1);
        var (x0, z0) = ((int)fx, (int)fz);
        var (x1, z1) = (Math.Min(x0 + 1, grid.Columns - 1), Math.Min(z0 + 1, grid.Rows - 1));
        var (tx, tz) = (fx - x0, fz - z0);
        float At(int c, int r) => floods[(r * grid.Columns) + c];
        var top = At(x0, z0) + ((At(x1, z0) - At(x0, z0)) * tx);
        var bottom = At(x0, z1) + ((At(x1, z1) - At(x0, z1)) * tx);
        return top + ((bottom - top) * tz);
    }
}
