using System;
using System.Collections.Generic;
using Pantheon.Domain.Land;

namespace Pantheon.Simulation.Terrain;

/// <summary>
/// Sculpts the land's reshapings into the ground before the rivers are found (spec §14, ADR-045):
/// an island rises from the sea floor in a lobed dome, a gorge cuts a mountain down to a floor
/// that runs from one low side to the other, and a channel falls from a river to its new mouth
/// with a dam across the old course. Each only touches the samples it reaches.
/// </summary>
internal static class TerrainReshaper
{
    // How far below the sea an island's shore slopes away, in metres at its radius.
    private const float ShoreDepth = 14f;

    // How much an island's outline swells and shrinks round its centre, as a share of its radius.
    private const float Lobes = 0.18f;

    /// <summary>Applies every reshaping, in order, to the heights.</summary>
    /// <param name="reshapes">The reshapings.</param>
    /// <param name="resolution">Samples per side.</param>
    /// <param name="spacing">Metres between samples.</param>
    /// <param name="heights">The ground's heights; changed in place.</param>
    public static void Apply(IReadOnlyList<TerrainReshape> reshapes, int resolution, float spacing, float[] heights)
    {
        foreach (var reshape in reshapes)
        {
            switch (reshape.Kind)
            {
                case ReshapeKind.Island:
                    Island(reshape, resolution, spacing, heights);
                    break;
                case ReshapeKind.Gorge:
                    Cut(reshape, resolution, spacing, heights, reshape.FromHeight, reshape.ToHeight);
                    break;
                case ReshapeKind.Channel:
                    Cut(reshape, resolution, spacing, heights, reshape.FromHeight - reshape.Peak, reshape.ToHeight - 1f);
                    Dam(reshape, resolution, spacing, heights);
                    break;
            }
        }
    }

    /// <summary>Raises a lobed dome from the sea floor: the peak at the centre, the shore at about the radius.</summary>
    private static void Island(TerrainReshape island, int resolution, float spacing, float[] heights)
    {
        var reach = island.Radius * (1f + Lobes) * 1.3f;
        var phase = island.Id * 1.7f;
        ForEach(island.X, island.Z, reach, resolution, spacing, (index, x, z) =>
        {
            var (dx, dz) = (x - island.X, z - island.Z);
            var swell = 1f + (Lobes * MathF.Sin((3f * MathF.Atan2(dz, dx)) + phase));
            var t = MathF.Sqrt((dx * dx) + (dz * dz)) / (island.Radius * swell);
            var height = island.Peak - ((island.Peak + ShoreDepth) * t * t);
            heights[index] = MathF.Max(heights[index], height);
        });
    }

    /// <summary>Lowers the ground along a line to a floor that runs from one height to another, steep-sided within the half width.</summary>
    private static void Cut(TerrainReshape cut, int resolution, float spacing, float[] heights, float fromFloor, float toFloor)
    {
        var (ax, az, bx, bz) = (cut.X, cut.Z, cut.ToX, cut.ToZ);
        var (minX, maxX) = (MathF.Min(ax, bx) - cut.Radius, MathF.Max(ax, bx) + cut.Radius);
        var (minZ, maxZ) = (MathF.Min(az, bz) - cut.Radius, MathF.Max(az, bz) + cut.Radius);
        var (lx, lz) = (bx - ax, bz - az);
        var lengthSquared = MathF.Max(1e-3f, (lx * lx) + (lz * lz));
        ForBox(minX, minZ, maxX, maxZ, resolution, spacing, (index, x, z) =>
        {
            var s = Math.Clamp((((x - ax) * lx) + ((z - az) * lz)) / lengthSquared, 0f, 1f);
            var (px, pz) = (ax + (s * lx) - x, az + (s * lz) - z);
            var distance = MathF.Sqrt((px * px) + (pz * pz));
            if (distance >= cut.Radius)
            {
                return;
            }

            var floor = fromFloor + ((toFloor - fromFloor) * s);
            var wall = SimMath.SmoothStep(0f, 1f, distance / cut.Radius);
            heights[index] = MathF.Min(heights[index], floor + ((heights[index] - floor) * wall * wall));
        });
    }

    /// <summary>Raises a dam across a river's old course, above where the channel leaves.</summary>
    private static void Dam(TerrainReshape channel, int resolution, float spacing, float[] heights)
    {
        if (channel.DamTop <= 0f)
        {
            return;
        }

        var radius = channel.Radius * 1.5f;
        ForEach(channel.DamX, channel.DamZ, radius, resolution, spacing, (index, x, z) =>
        {
            var (dx, dz) = (x - channel.DamX, z - channel.DamZ);
            var t = MathF.Sqrt((dx * dx) + (dz * dz)) / radius;
            heights[index] = MathF.Max(heights[index], channel.DamTop - (channel.Peak * t * t));
        });
    }

    /// <summary>Visits the samples within a radius of a point.</summary>
    private static void ForEach(float cx, float cz, float radius, int resolution, float spacing, Action<int, float, float> visit) =>
        ForBox(cx - radius, cz - radius, cx + radius, cz + radius, resolution, spacing, (index, x, z) =>
        {
            if (((x - cx) * (x - cx)) + ((z - cz) * (z - cz)) <= radius * radius)
            {
                visit(index, x, z);
            }
        });

    /// <summary>Visits the samples within a box.</summary>
    private static void ForBox(float minX, float minZ, float maxX, float maxZ, int resolution, float spacing, Action<int, float, float> visit)
    {
        var (x0, x1) = (Math.Max(0, (int)MathF.Floor(minX / spacing)), Math.Min(resolution - 1, (int)MathF.Ceiling(maxX / spacing)));
        var (z0, z1) = (Math.Max(0, (int)MathF.Floor(minZ / spacing)), Math.Min(resolution - 1, (int)MathF.Ceiling(maxZ / spacing)));
        for (var z = z0; z <= z1; z++)
        {
            for (var x = x0; x <= x1; x++)
            {
                visit((z * resolution) + x, x * spacing, z * spacing);
            }
        }
    }
}
