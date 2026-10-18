using System;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Builds the single-channel sea depth map read by the water shader. The shader tints and fades
/// the water by depth without reading the depth buffer, so it works on every renderer.
/// </summary>
public static class WaterDepthMap
{
    /// <summary>Computes depth values for a square texture covering the terrain's footprint.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="resolution">Texels per side.</param>
    /// <param name="deepWaterDepth">Depth in metres that maps to 255; deeper water is clamped.</param>
    /// <returns>Row-major bytes: 0 on land and at the shoreline, 255 in deep water.</returns>
    public static byte[] Compute(TerrainField field, int resolution, float deepWaterDepth)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolution);
        if (!(deepWaterDepth > 0f))
        {
            throw new ArgumentOutOfRangeException(nameof(deepWaterDepth), deepWaterDepth, "Deep water depth must be positive.");
        }

        var texelSize = field.WorldSize / resolution;
        var depths = new byte[resolution * resolution];
        for (var row = 0; row < resolution; row++)
        {
            for (var column = 0; column < resolution; column++)
            {
                // Sample at texel centres, which is where linear filtering reproduces the value exactly.
                var height = field.SampleHeight((column + 0.5f) * texelSize, (row + 0.5f) * texelSize);
                depths[(row * resolution) + column] = RgbColor.ToByte(-height / deepWaterDepth);
            }
        }

        return depths;
    }
}
