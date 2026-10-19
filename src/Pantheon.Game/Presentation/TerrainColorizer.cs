using System;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Computes the ground colour of every terrain sample from its biome, height and slope. Both
/// terrain renderers use it: as the Terrain3D colour map and as fallback vertex colours.
/// </summary>
public static class TerrainColorizer
{
    /// <summary>Bytes per pixel of the RGBA8 output.</summary>
    public const int BytesPerPixel = 4;

    /// <summary>Alpha written to every pixel. Terrain3D reads colour-map alpha as a roughness modifier, and 0.5 is neutral.</summary>
    public const byte NeutralAlpha = 128;

    /// <summary>Computes RGBA8 colours for all samples, row-major.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="palette">Validated palette.</param>
    /// <returns><c>Resolution * Resolution * 4</c> bytes.</returns>
    public static byte[] ComputeRgba(TerrainField field, BiomePalette palette)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(palette);

        var resolution = field.Resolution;
        var pixels = new byte[resolution * resolution * BytesPerPixel];
        for (var z = 0; z < resolution; z++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var color = ComputeColor(field, palette, x, z);
                var offset = ((z * resolution) + x) * BytesPerPixel;
                pixels[offset] = RgbColor.ToByte(color.R);
                pixels[offset + 1] = RgbColor.ToByte(color.G);
                pixels[offset + 2] = RgbColor.ToByte(color.B);
                pixels[offset + 3] = NeutralAlpha;
            }
        }

        return pixels;
    }

    /// <summary>Ground colour of one sample.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="palette">Palette.</param>
    /// <param name="x">Sample column.</param>
    /// <param name="z">Sample row.</param>
    /// <returns>The sample's colour.</returns>
    public static RgbColor ComputeColor(TerrainField field, BiomePalette palette, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(palette);

        var height = field.GetHeight(x, z);
        var color = height < 0f ? UnderwaterColor(palette, height)
            : field.IsUnderFreshWater(x, z) ? BedColor(field, palette, x, z, height)
            : LandColor(field, palette, x, z, height);
        return color.Scale(1f + (palette.ColorVariation * Jitter(x, z)));
    }

    /// <summary>Sand in the shallows, fading to the sea-floor colour with depth; river and lake beds too (ADR-022).</summary>
    private static RgbColor UnderwaterColor(BiomePalette palette, float height) =>
        RgbColor.Lerp(palette.Beach, palette.SeaFloor, SmoothStep(0f, palette.DeepWaterDepth, -height));

    /// <summary>
    /// The bed of a river or lake (ADR-022): the land's colour in the shallows at its edge, darkening
    /// to the sea floor's with depth, so the bank shows no hard outline.
    /// </summary>
    private static RgbColor BedColor(TerrainField field, BiomePalette palette, int x, int z, float height)
    {
        var depth = field.Hydrology.WaterSurface[(z * field.Resolution) + x] - height;
        return RgbColor.Lerp(LandColor(field, palette, x, z, height), palette.SeaFloor, SmoothStep(0f, BedDarkDepth, depth));
    }

    // Depth in metres at which a river or lake bed takes the sea floor's colour.
    private const float BedDarkDepth = 2.5f;

    /// <summary>Biome colour with beach, rock and snow blended in.</summary>
    private static RgbColor LandColor(TerrainField field, BiomePalette palette, int x, int z, float height)
    {
        var color = palette.GetBiomeColor(field.GetBiome(x, z));
        color = RgbColor.Lerp(palette.Beach, color, SmoothStep(0f, palette.BeachMaxHeight, height));
        color = RgbColor.Lerp(color, palette.Rock, SmoothStep(palette.RockSlopeStart, palette.RockSlopeFull, field.GetSlope(x, z)));
        return RgbColor.Lerp(color, palette.Snow, SmoothStep(palette.SnowStartHeight, palette.SnowStartHeight + palette.SnowBlendRange, height));
    }

    /// <summary>Deterministic per-sample value in -1..1 that breaks up flat colour areas.</summary>
    private static float Jitter(int x, int z)
    {
        var hash = unchecked(((uint)x * 0x8DA6B343u) ^ ((uint)z * 0xD8163841u));
        hash ^= hash >> 13;
        hash = unchecked(hash * 0x5BD1E995u);
        hash ^= hash >> 15;
        return ((hash & 0xFFFF) / 32767.5f) - 1f;
    }

    /// <summary>Hermite smoothstep between two edges.</summary>
    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
