using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>Engine-agnostic quads of a water surface (ADR-022).</summary>
/// <param name="Positions">Vertex positions in world metres.</param>
/// <param name="Depths">Depth of the water over the ground at each vertex, 0..1 of the deepest drawn.</param>
/// <param name="Indices">Triangle list, wound clockwise when seen from above, as Godot expects.</param>
public sealed record WaterSurfaceMeshData(Vector3[] Positions, float[] Depths, int[] Indices)
{
    /// <summary>Whether there is nothing to draw.</summary>
    public bool IsEmpty => Indices.Length == 0;
}

/// <summary>
/// Builds the water of the rivers and lakes as quads over the heightfield (ADR-022); floods are
/// <see cref="FloodSurface"/>'s.
/// The banks hide the water's edge, so a quad whose corner lies on dry ground takes the water's
/// height there too.
/// </summary>
public static class WaterSurfaceMesh
{
    /// <summary>Depth of water drawn darkest, in metres.</summary>
    public const float DeepestDrawn = 3f;

    /// <summary>Metres a flood's water stands above the water it rises from before it rises.</summary>
    public const float FloodClearance = 0.3f;

    /// <summary>The rivers and lakes: one quad per <paramref name="step"/>-sample cell that has water at a corner.</summary>
    /// <param name="field">Terrain with its hydrology.</param>
    /// <param name="step">Samples per quad side.</param>
    /// <returns>The mesh; empty for a world without fresh water.</returns>
    public static WaterSurfaceMeshData BuildFreshWater(TerrainField field, int step)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        var builder = new Builder();
        var surface = field.Hydrology.WaterSurface;
        if (field.Hydrology.Rivers.Count == 0 && field.Hydrology.Lakes.Count == 0)
        {
            return builder.Build();
        }

        var last = field.Resolution - 1;
        Span<float> levels = stackalloc float[4];
        for (var z = 0; z < last; z += step)
        {
            for (var x = 0; x < last; x += step)
            {
                var x1 = Math.Min(x + step, last);
                var z1 = Math.Min(z + step, last);
                var wettest = float.NaN;
                levels[0] = surface[(z * field.Resolution) + x];
                levels[1] = surface[(z * field.Resolution) + x1];
                levels[2] = surface[(z1 * field.Resolution) + x];
                levels[3] = surface[(z1 * field.Resolution) + x1];
                foreach (var level in levels)
                {
                    wettest = float.IsNaN(level) ? wettest : float.IsNaN(wettest) ? level : MathF.Max(wettest, level);
                }

                if (float.IsNaN(wettest))
                {
                    continue;
                }

                // A dry corner meets the ground: under a higher bank it hides at the water's height, and where
                // the ground falls away it dips just below it, so the water's edge always meets the land.
                levels[0] = Dry(field, x, z, levels[0], wettest);
                levels[1] = Dry(field, x1, z, levels[1], wettest);
                levels[2] = Dry(field, x, z1, levels[2], wettest);
                levels[3] = Dry(field, x1, z1, levels[3], wettest);
                builder.Quad(field, x, z, x1, z1, levels[0], levels[1], levels[2], levels[3]);
            }
        }

        return builder.Build();
    }

    /// <summary>The height of a quad's corner: its water, or for a dry corner the cell's water or the ground (or the sea) just below it, whichever is lower.</summary>
    private static float Dry(TerrainField field, int x, int z, float level, float wettest) =>
        float.IsNaN(level) ? MathF.Min(wettest, MathF.Max(field.GetHeight(x, z), 0f) - EdgeSink) : level;

    // A dry corner where the ground lies below the water dips this far under the ground.
    private const float EdgeSink = 0.05f;

    /// <summary>Collects quads.</summary>
    private sealed class Builder
    {
        private readonly List<Vector3> _positions = [];
        private readonly List<float> _depths = [];
        private readonly List<int> _indices = [];

        /// <summary>A quad over the cell from sample (x0, z0) to (x1, z1), its corners at the given water heights.</summary>
        public void Quad(TerrainField field, int x0, int z0, int x1, int z1, float topLeft, float topRight, float bottomLeft, float bottomRight)
        {
            var first = _positions.Count;
            Corner(field, x0, z0, topLeft);
            Corner(field, x1, z0, topRight);
            Corner(field, x0, z1, bottomLeft);
            Corner(field, x1, z1, bottomRight);
            _indices.AddRange([first, first + 1, first + 2, first + 1, first + 3, first + 2]);
        }

        /// <summary>The finished mesh.</summary>
        public WaterSurfaceMeshData Build() => new([.. _positions], [.. _depths], [.. _indices]);

        private void Corner(TerrainField field, int x, int z, float level)
        {
            _positions.Add(new Vector3(x * field.Spacing, level, z * field.Spacing));
            _depths.Add(Math.Clamp((level - field.GetHeight(x, z)) / DeepestDrawn, 0f, 1f));
        }
    }
}
