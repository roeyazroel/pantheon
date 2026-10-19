using System;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>
/// The ground's textures for Terrain3D (spec §61, ADR-051), made in code from seamless noise: each
/// kind's own pattern between its two colours, its height in the albedo's alpha (Terrain3D blends
/// textures by height), and a normal map with the roughness in its alpha.
/// </summary>
internal static class GroundTextures
{
    private const int Size = 512;

    /// <summary>One texture's albedo and normal map.</summary>
    /// <param name="Albedo">Colour, with height in alpha.</param>
    /// <param name="Normal">Normal, with roughness in alpha.</param>
    public readonly record struct Maps(ImageTexture Albedo, ImageTexture Normal);

    private static readonly System.Collections.Generic.Dictionary<GroundKind, Maps> Made = [];
    private static GroundSettings? _madeFor;

    /// <summary>A ground texture, made once for the settings and kept for every world after.</summary>
    /// <param name="kind">Which.</param>
    /// <param name="settings">Ground settings, for its colours.</param>
    /// <returns>Its maps.</returns>
    public static Maps Create(GroundKind kind, GroundSettings settings)
    {
        if (!ReferenceEquals(_madeFor, settings))
        {
            _madeFor = settings;
            Made.Clear();
        }

        if (!Made.TryGetValue(kind, out var maps))
        {
            maps = Make(kind, settings);
            Made[kind] = maps;
        }

        return maps;
    }

    /// <summary>Makes a ground texture.</summary>
    private static Maps Make(GroundKind kind, GroundSettings settings)
    {
        var (dark, light) = settings.ColorsOf(kind);
        var (pattern, bump, roughness) = Pattern(kind);
        var rgba = new byte[Size * Size * 4];
        var heights = new byte[Size * Size * 4];
        for (var i = 0; i < Size * Size; i++)
        {
            var value = Math.Clamp(pattern[i], 0f, 1f);
            var color = RgbColor.Lerp(dark, light, value);
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], rgba[(i * 4) + 3]) = (RgbColor.ToByte(color.R), RgbColor.ToByte(color.G), RgbColor.ToByte(color.B), RgbColor.ToByte(value));
            var height = RgbColor.ToByte(value);
            (heights[i * 4], heights[(i * 4) + 1], heights[(i * 4) + 2], heights[(i * 4) + 3]) = (height, height, height, 255);
        }

        var albedo = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, rgba);
        albedo.GenerateMipmaps();
        var normal = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, heights);
        normal.BumpMapToNormalMap(bump);
        normal.Convert(Image.Format.Rgba8);
        var data = normal.GetData();
        for (var i = 3; i < data.Length; i += 4)
        {
            data[i] = RgbColor.ToByte(roughness);
        }

        normal = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, data);
        normal.GenerateMipmaps();
        return new Maps(ImageTexture.CreateFromImage(albedo), ImageTexture.CreateFromImage(normal));
    }

    /// <summary>A kind's pattern (0 dark .. 1 light), how strongly it bumps, and how rough it is.</summary>
    private static (float[] Pattern, float Bump, float Roughness) Pattern(GroundKind kind) => kind switch
    {
        // Grass: broad patches of lush and dry, mottled, with fine tufts.
        GroundKind.Grass => (Mix(Mix(Fbm(11, 0.007f, 5), 0.6f, Fbm(13, 0.045f, 3), 0.4f), 0.75f, Cells(12, 0.22f, FastNoiseLite.CellularReturnTypeEnum.CellValue), 0.25f), 3f, 0.95f),
        // Forest floor: dark soil with scattered leaves.
        GroundKind.ForestFloor => (Mix(Mix(Fbm(21, 0.01f, 5), 0.6f, Fbm(23, 0.05f, 3), 0.4f), 0.65f, Cells(22, 0.12f, FastNoiseLite.CellularReturnTypeEnum.CellValue), 0.35f), 4f, 0.9f),
        // Earth: soil with pebbles.
        GroundKind.Earth => (Mix(Fbm(31, 0.015f, 5), 0.65f, Threshold(Cells(32, 0.04f, FastNoiseLite.CellularReturnTypeEnum.Distance), 0.25f), 0.35f), 5f, 0.92f),
        // Rock: broken stone, cracked between its plates.
        GroundKind.Rock => (Mix(Fbm(41, 0.01f, 6), 0.55f, Cells(42, 0.02f, FastNoiseLite.CellularReturnTypeEnum.Distance2Div), 0.45f), 9f, 0.85f),
        // Sand: fine grain over wind ripples.
        GroundKind.Sand => (Mix(Ripples(51, 0.06f), 0.45f, Fbm(52, 0.08f, 3), 0.55f), 2f, 0.97f),
        // Snow: soft drifts.
        GroundKind.Snow => (Mix(Fbm(61, 0.008f, 4), 0.8f, Fbm(62, 0.05f, 2), 0.2f), 1.5f, 0.6f),
        // Moss: lumpy cushions.
        GroundKind.Moss => (Mix(Cells(71, 0.03f, FastNoiseLite.CellularReturnTypeEnum.Distance), 0.5f, Fbm(72, 0.03f, 4), 0.5f), 4f, 0.95f),
        // Mud: wet, with darker puddles.
        GroundKind.Mud => (Mix(Fbm(81, 0.012f, 4), 0.7f, Fbm(82, 0.05f, 3), 0.3f), 2.5f, 0.55f),
        // Ash: dark cinders.
        GroundKind.Ash => (Mix(Fbm(91, 0.02f, 5), 0.5f, Cells(92, 0.07f, FastNoiseLite.CellularReturnTypeEnum.CellValue), 0.5f), 4f, 0.97f),
    };

    /// <summary>Seamless fractal noise, 0..1.</summary>
    private static float[] Fbm(int seed, float frequency, int octaves) =>
        Sample(new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = seed, Frequency = frequency, FractalOctaves = octaves });

    /// <summary>Seamless cellular noise, 0..1.</summary>
    private static float[] Cells(int seed, float frequency, FastNoiseLite.CellularReturnTypeEnum kind) =>
        Sample(new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Seed = seed, Frequency = frequency, FractalType = FastNoiseLite.FractalTypeEnum.None, CellularReturnType = kind, CellularJitter = 0.9f });

    /// <summary>Wind ripples: bands bent by noise.</summary>
    private static float[] Ripples(int seed, float frequency)
    {
        var bend = Fbm(seed, 0.01f, 3);
        var ripples = new float[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                // A whole number of bands across the tile keeps it seamless.
                var bands = MathF.Round(Size * frequency / MathF.Tau) * MathF.Tau / Size;
                ripples[(y * Size) + x] = 0.5f + (0.5f * MathF.Sin((y * bands) + (bend[(y * Size) + x] * 6f)));
            }
        }

        return ripples;
    }

    /// <summary>Values below a threshold become 1 (pebbles), others 0, softened at the edge.</summary>
    private static float[] Threshold(float[] values, float threshold)
    {
        var result = new float[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = Math.Clamp((threshold - values[i]) / 0.05f, 0f, 1f);
        }

        return result;
    }

    /// <summary>A weighted mix of two patterns.</summary>
    private static float[] Mix(float[] a, float weightA, float[] b, float weightB)
    {
        var result = new float[a.Length];
        for (var i = 0; i < a.Length; i++)
        {
            result[i] = (a[i] * weightA) + (b[i] * weightB);
        }

        return result;
    }

    /// <summary>A noise as a seamless tile of values 0..1.</summary>
    private static float[] Sample(FastNoiseLite noise)
    {
        var image = noise.GetSeamlessImage(Size, Size);
        image.Convert(Image.Format.L8);
        var bytes = image.GetData();
        var values = new float[Size * Size];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = bytes[i] / 255f;
        }

        return values;
    }
}
