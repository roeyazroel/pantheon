using System;
using System.Collections.Generic;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Cuts rivers into the heightfield and marks where fresh water stands (ADR-022). A river's bed is
/// a rounded channel below its water, and its banks slope down to the water's edge; the ground is
/// only ever lowered, so the order of the cuts does not matter.
/// </summary>
internal static class ChannelCarver
{
    // The ground at the water's edge lies just under the surface, so the water meets the bank.
    private const float EdgeDepth = 0.05f;

    // The foot of a bank lies just above the water.
    private const float BankFoot = 0.1f;

    // A river this far below a lake's surface has left the lake.
    private const float OutletDrop = 0.3f;

    /// <summary>Cuts every river and fills every lake.</summary>
    /// <param name="settings">Hydrology settings.</param>
    /// <param name="original">The terrain before any cut.</param>
    /// <param name="heights">Height samples to cut into.</param>
    /// <param name="surface">Fresh water surface per sample, filled in where water stands.</param>
    /// <param name="rivers">River courses.</param>
    /// <param name="lakes">Lakes.</param>
    public static void Carve(HydrologySettings settings, TerrainField original, float[] heights, float[] surface, IReadOnlyList<RiverCourse> rivers, IReadOnlyList<LakeBasin> lakes)
    {
        foreach (var river in rivers)
        {
            for (var i = 1; i < river.Points.Count; i++)
            {
                CarveSegment(settings, original, heights, surface, river.Points[i - 1], river.Points[i]);
            }
        }

        foreach (var lake in lakes)
        {
            Fill(original, surface, lake);
        }
    }

    /// <summary>Cuts the channel and banks of one stretch of river.</summary>
    private static void CarveSegment(HydrologySettings settings, TerrainField field, float[] heights, float[] surface, RiverPoint a, RiverPoint b)
    {
        var reach = (MathF.Max(a.Width, b.Width) * 0.5f) + settings.BankWidth;
        var spacing = field.Spacing;
        var resolution = field.Resolution;
        var minX = Math.Max(0, (int)MathF.Floor((MathF.Min(a.X, b.X) - reach) / spacing));
        var maxX = Math.Min(resolution - 1, (int)MathF.Ceiling((MathF.Max(a.X, b.X) + reach) / spacing));
        var minZ = Math.Max(0, (int)MathF.Floor((MathF.Min(a.Z, b.Z) - reach) / spacing));
        var maxZ = Math.Min(resolution - 1, (int)MathF.Ceiling((MathF.Max(a.Z, b.Z) + reach) / spacing));
        var heightsBefore = field.Heights;
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;
        var lengthSquared = MathF.Max(1e-6f, (dx * dx) + (dz * dz));

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var px = x * spacing;
                var pz = z * spacing;
                var t = Math.Clamp((((px - a.X) * dx) + ((pz - a.Z) * dz)) / lengthSquared, 0f, 1f);
                var ox = px - (a.X + (t * dx));
                var oz = pz - (a.Z + (t * dz));
                var distance = MathF.Sqrt((ox * ox) + (oz * oz));
                var water = SimMath.Lerp(a.Surface, b.Surface, t);
                var half = SimMath.Lerp(a.Width, b.Width, t) * 0.5f;
                var depth = SimMath.Lerp(a.Depth, b.Depth, t);
                var index = (z * resolution) + x;
                var before = heightsBefore[index];

                float target;
                if (distance <= half)
                {
                    var across = distance / half;
                    target = MathF.Min(water - (depth * (1f - (across * across))), water - EdgeDepth);
                }
                else if (distance <= half + settings.BankWidth)
                {
                    var u = (distance - half) / settings.BankWidth;
                    var smooth = u * u * (3f - (2f * u));
                    var foot = water + BankFoot;
                    target = before > foot ? foot + ((before - foot) * smooth) : before;
                }
                else
                {
                    continue;
                }

                heights[index] = MathF.Min(heights[index], target);
                if (distance <= half + (0.75f * spacing))
                {
                    surface[index] = float.IsNaN(surface[index]) ? water : MathF.Max(surface[index], water);
                }
            }
        }
    }

    /// <summary>Water stands at the lake's surface over every sample below it in and next to the lake's cells.</summary>
    private static void Fill(TerrainField field, float[] surface, LakeBasin lake)
    {
        var spacing = field.Spacing;
        var resolution = field.Resolution;
        var heights = field.Heights;
        var margin = lake.CellSize * 0.5f;
        foreach (var cell in lake.Cells)
        {
            var minX = Math.Max(0, (int)MathF.Floor((cell.X - margin) / spacing));
            var maxX = Math.Min(resolution - 1, (int)MathF.Ceiling((cell.X + margin) / spacing));
            var minZ = Math.Max(0, (int)MathF.Floor((cell.Z - margin) / spacing));
            var maxZ = Math.Min(resolution - 1, (int)MathF.Ceiling((cell.Z + margin) / spacing));
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var index = (z * resolution) + x;
                    // A river already flowing below the lake's surface here is its outlet, not the lake.
                    if (heights[index] < lake.Surface && !(surface[index] < lake.Surface - OutletDrop))
                    {
                        surface[index] = float.IsNaN(surface[index]) ? lake.Surface : MathF.Max(surface[index], lake.Surface);
                    }
                }
            }
        }
    }
}
