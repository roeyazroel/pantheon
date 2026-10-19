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
        var blended = BlendBiomes(field, palette);
        for (var z = 0; z < resolution; z++)
        {
            for (var x = 0; x < resolution; x++)
            {
                var color = ComputeColor(field, palette, x, z, blended?[(z * resolution) + x]);
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
    public static RgbColor ComputeColor(TerrainField field, BiomePalette palette, int x, int z) => ComputeColor(field, palette, x, z, null);

    /// <summary>Ground colour of one sample, with its land biome colour already blended, if given.</summary>
    private static RgbColor ComputeColor(TerrainField field, BiomePalette palette, int x, int z, RgbColor? biome)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(palette);

        var height = field.GetHeight(x, z);
        biome ??= BlendAt(field, palette, x, z);
        var color = height < 0f ? UnderwaterColor(palette, height)
            : field.IsUnderFreshWater(x, z) ? BedColor(field, palette, x, z, height, biome.Value)
            : LandColor(field, palette, x, z, height, biome.Value);
        return color.Scale(1f + (palette.ColorVariation * Jitter(x, z)));
    }

    /// <summary>Samples of land around a sample whose biome colours blend (ADR-038).</summary>
    private static int BlendRadius(TerrainField field, BiomePalette palette) =>
        Math.Max(0, (int)MathF.Round(palette.BiomeBlendMetres / field.Spacing));

    /// <summary>The mean biome colour of the land samples within the blend radius of one sample; its own biome's colour when none blends.</summary>
    private static RgbColor BlendAt(TerrainField field, BiomePalette palette, int x, int z)
    {
        var radius = BlendRadius(field, palette);
        var (r, g, b, n) = (0f, 0f, 0f, 0);
        for (var dz = -radius; dz <= radius; dz++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var (sx, sz) = (x + dx, z + dz);
                if (sx < 0 || sz < 0 || sx >= field.Resolution || sz >= field.Resolution || field.GetBiome(sx, sz) == Domain.Ecology.Biome.Sea)
                {
                    continue;
                }

                var color = palette.GetBiomeColor(field.GetBiome(sx, sz));
                (r, g, b, n) = (r + color.R, g + color.G, b + color.B, n + 1);
            }
        }

        return n == 0 ? palette.GetBiomeColor(field.GetBiome(x, z)) : new RgbColor(r / n, g / n, b / n);
    }

    /// <summary>Every sample's blended land biome colour, by a box filter in two sliding passes; <c>null</c> when nothing blends.</summary>
    private static RgbColor[]? BlendBiomes(TerrainField field, BiomePalette palette)
    {
        var radius = BlendRadius(field, palette);
        if (radius == 0)
        {
            return null;
        }

        var kinds = Enum.GetValues<Domain.Ecology.Biome>();
        var colors = new RgbColor[kinds.Length];
        foreach (var kind in kinds)
        {
            colors[(int)kind] = palette.GetBiomeColor(kind);
        }

        var n = field.Resolution;
        var biomes = field.Biomes;
        var rows = new float[n * n * 4];
        for (var z = 0; z < n; z++)
        {
            var (r, g, b, count) = (0f, 0f, 0f, 0f);
            for (var x = -radius; x < n + radius; x++)
            {
                var enter = x + radius;
                if (enter < n && biomes[(z * n) + enter] != Domain.Ecology.Biome.Sea)
                {
                    var c = colors[(int)biomes[(z * n) + enter]];
                    (r, g, b, count) = (r + c.R, g + c.G, b + c.B, count + 1f);
                }

                var leave = x - radius - 1;
                if (leave >= 0 && biomes[(z * n) + leave] != Domain.Ecology.Biome.Sea)
                {
                    var c = colors[(int)biomes[(z * n) + leave]];
                    (r, g, b, count) = (r - c.R, g - c.G, b - c.B, count - 1f);
                }

                if (x >= 0 && x < n)
                {
                    var at = ((z * n) + x) * 4;
                    (rows[at], rows[at + 1], rows[at + 2], rows[at + 3]) = (r, g, b, count);
                }
            }
        }

        var blended = new RgbColor[n * n];
        for (var x = 0; x < n; x++)
        {
            var (r, g, b, count) = (0f, 0f, 0f, 0f);
            for (var z = -radius; z < n + radius; z++)
            {
                var enter = z + radius;
                if (enter < n)
                {
                    var at = ((enter * n) + x) * 4;
                    (r, g, b, count) = (r + rows[at], g + rows[at + 1], b + rows[at + 2], count + rows[at + 3]);
                }

                var leave = z - radius - 1;
                if (leave >= 0)
                {
                    var at = ((leave * n) + x) * 4;
                    (r, g, b, count) = (r - rows[at], g - rows[at + 1], b - rows[at + 2], count - rows[at + 3]);
                }

                if (z >= 0 && z < n)
                {
                    blended[(z * n) + x] = count > 0.5f ? new RgbColor(r / count, g / count, b / count) : colors[(int)biomes[(z * n) + x]];
                }
            }
        }

        return blended;
    }

    /// <summary>Sand in the shallows, fading to the sea-floor colour with depth; river and lake beds too (ADR-022).</summary>
    private static RgbColor UnderwaterColor(BiomePalette palette, float height) =>
        RgbColor.Lerp(palette.Beach, palette.SeaFloor, SmoothStep(0f, palette.DeepWaterDepth, -height));

    /// <summary>
    /// The bed of a river or lake (ADR-022): the land's colour in the shallows at its edge, darkening
    /// to the sea floor's with depth, so the bank shows no hard outline.
    /// </summary>
    private static RgbColor BedColor(TerrainField field, BiomePalette palette, int x, int z, float height, RgbColor biome)
    {
        var depth = field.Hydrology.WaterSurface[(z * field.Resolution) + x] - height;
        return RgbColor.Lerp(LandColor(field, palette, x, z, height, biome), palette.SeaFloor, SmoothStep(0f, BedDarkDepth, depth));
    }

    // Depth in metres at which a river or lake bed takes the sea floor's colour.
    private const float BedDarkDepth = 2.5f;

    /// <summary>Biome colour with beach, rock and snow blended in.</summary>
    private static RgbColor LandColor(TerrainField field, BiomePalette palette, int x, int z, float height, RgbColor biome)
    {
        var color = biome;
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
