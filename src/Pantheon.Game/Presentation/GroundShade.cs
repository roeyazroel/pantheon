using System;
using System.Threading.Tasks;
using Pantheon.Game.Presentation.Nature;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The land's broad light and colour over the ground's colour map (spec §61, ADR-077): what a
/// repeating texture cannot show from high up. Hollows are darker and ridges lighter, the grass dries
/// in wide patches and grows lush along rivers and lakes, wet sand darkens at the water's edge and
/// rock falls in bands. Both terrain renderers use it. Pure: the same land gives the same colours.
/// </summary>
public static class GroundShade
{
    // Noise lattices repeat after this many cells, further than any world reaches.
    private const int Period = 4096;

    // Which noise each layer draws.
    private const int MottleSalt = 7601;
    private const int DrySalt = 7602;
    private const int StrataSalt = 7603;

    // Metres of hollow, at the fine and the broad scale, that count as a full hollow.
    private const float FineHollow = 1.2f;
    private const float BroadHollow = 6f;

    // How much a blurred ribbon of water counts: a thin river's share of the blur is small.
    private const float WetGain = 3f;

    /// <summary>Shades a colour map in place.</summary>
    /// <param name="rgba">The colour map, <see cref="TerrainColorizer.BytesPerPixel"/> bytes a sample, row-major.</param>
    /// <param name="field">Terrain.</param>
    /// <param name="palette">Palette, for the shade's settings and the rock's slopes.</param>
    public static void Apply(byte[] rgba, TerrainField field, BiomePalette palette)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(palette);
        var n = field.Resolution;
        var land = new float[n * n];
        var water = new float[n * n];
        for (var z = 0; z < n; z++)
        {
            for (var x = 0; x < n; x++)
            {
                land[(z * n) + x] = MathF.Max(field.GetHeight(x, z), 0f);
                water[(z * n) + x] = field.IsUnderFreshWater(x, z) ? 1f : 0f;
            }
        }

        var shade = palette.Ground.Shade;
        var fine = Blurred(land, n, Samples(shade.HollowMetres, field));
        var broad = Blurred(land, n, Samples(shade.HollowMetres * 4f, field));
        var wet = Blurred(water, n, Samples(shade.LushMetres, field));
        Parallel.For(0, n, z =>
        {
            for (var x = 0; x < n; x++)
            {
                var at = (z * n) + x;
                var height = field.GetHeight(x, z);
                var (light, tint) = height <= 0f
                    ? (shade.Neutral, RgbColor.White)
                    : Land(field, palette, shade, x, z, height, ((fine[at] - height) / FineHollow) + ((broad[at] - height) / BroadHollow), wet[at]);
                var offset = at * TerrainColorizer.BytesPerPixel;
                rgba[offset] = RgbColor.ToByte(rgba[offset] / 255f * light * tint.R);
                rgba[offset + 1] = RgbColor.ToByte(rgba[offset + 1] / 255f * light * tint.G);
                rgba[offset + 2] = RgbColor.ToByte(rgba[offset + 2] / 255f * light * tint.B);
            }
        });
    }

    /// <summary>A land sample's brightness and tint.</summary>
    private static (float Light, RgbColor Tint) Land(TerrainField field, BiomePalette palette, GroundShadeSettings shade, int x, int z, float height, float hollow, float wet)
    {
        var (east, south) = (x * field.Spacing, z * field.Spacing);
        var mottle = (NatureNoise.Fbm(east / shade.MottleMetres, south / shade.MottleMetres, Period, 3, MottleSalt) * 2f) - 1f;
        var dry = SmoothStep(0.42f, 0.62f, NatureNoise.Fbm(east / shade.DryMetres, south / shade.DryMetres, Period, 3, DrySalt));
        var light = shade.Neutral * (1f + (shade.Mottle * mottle));
        light *= (1f - (shade.HollowDark * Math.Clamp(hollow, 0f, 1f))) * (1f + (shade.RidgeLight * Math.Clamp(-hollow, 0f, 1f)));
        light *= 1f - (shade.WetShore * (1f - SmoothStep(0f, shade.WetShoreHeight, height)));
        var rock = SmoothStep(palette.RockSlopeStart, palette.RockSlopeFull, field.GetSlope(x, z));
        if (rock > 0f)
        {
            var wander = NatureNoise.Fbm(east / 40f, south / 40f, Period, 2, StrataSalt) * 4f;
            light *= 1f + (shade.Strata * rock * MathF.Sin((height / shade.StrataMetres * MathF.Tau) + wander));
        }

        var lush = Math.Min(1f, wet * WetGain);
        var tint = RgbColor.Lerp(RgbColor.White, shade.DryTint, dry * shade.Dry * (1f - lush) * (1f - rock));
        return (light, tint * RgbColor.Lerp(RgbColor.White, shade.LushTint, lush * shade.Lush));
    }

    /// <summary>A distance in samples, at least one.</summary>
    private static int Samples(float metres, TerrainField field) => Math.Max(1, (int)MathF.Round(metres / field.Spacing));

    /// <summary>A grid blurred by a box of the given radius, in two sliding passes.</summary>
    private static float[] Blurred(float[] values, int n, int radius)
    {
        var across = new float[n * n];
        var blurred = new float[n * n];
        Parallel.For(0, n, z => Slide(values, across, z * n, 1, n, radius));
        Parallel.For(0, n, x => Slide(across, blurred, x, n, n, radius));
        return blurred;
    }

    /// <summary>One line of a box blur: the sum of a window that slides along it, edges held.</summary>
    private static void Slide(float[] from, float[] to, int start, int stride, int n, int radius)
    {
        var sum = 0f;
        for (var i = -radius; i <= radius; i++)
        {
            sum += from[start + (Math.Clamp(i, 0, n - 1) * stride)];
        }

        var share = 1f / ((2 * radius) + 1);
        for (var i = 0; i < n; i++)
        {
            to[start + (i * stride)] = sum * share;
            sum += from[start + (Math.Min(i + radius + 1, n - 1) * stride)] - from[start + (Math.Max(i - radius, 0) * stride)];
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
