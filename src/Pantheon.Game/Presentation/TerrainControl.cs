using System;
using Pantheon.Domain.Ecology;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The ground's textures, sample by sample (spec §61, ADR-051), as Terrain3D's control map: a base
/// texture from the biome (sand on beaches), blended over a few metres into a neighbouring biome's
/// so no border is a hard line, an overlay of rock on steep slopes or snow on cold heights, blended
/// as the colour map blends them, and a turn of the texture that breaks its tiling. Also the gentle
/// tint the biomes give the textures. Pure.
/// </summary>
public static class TerrainControl
{
    // Terrain3D's control word: base id in bits 27-31, overlay id in 22-26, blend in 14-21, UV angle in 10-13.
    private const int BaseShift = 27;
    private const int OverlayShift = 22;
    private const int BlendShift = 14;
    private const int AngleShift = 10;
    private const ulong AngleSalt = 61;

    // Neighbouring biomes' textures blend over this many samples each way (6 m at 2 m spacing).
    private const int BorderSamples = 3;

    /// <summary>A sample's ground.</summary>
    /// <param name="Base">Its texture.</param>
    /// <param name="Overlay">The texture over it.</param>
    /// <param name="Blend">How much of the overlay shows, 0..1.</param>
    public readonly record struct Ground(GroundKind Base, GroundKind Overlay, float Blend);

    /// <summary>The ground's texture for a biome.</summary>
    /// <param name="biome">The biome.</param>
    /// <returns>Its texture.</returns>
    public static GroundKind BaseOf(Biome biome) => biome switch
    {
        Biome.Sea => GroundKind.Sand,
        Biome.Grassland => GroundKind.Grass,
        Biome.Forest => GroundKind.ForestFloor,
        Biome.Highland => GroundKind.Earth,
        Biome.Desert => GroundKind.Sand,
        Biome.Tundra => GroundKind.Moss,
        Biome.Swamp => GroundKind.Mud,
        Biome.Coast => GroundKind.Grass,
        Biome.RiverValley => GroundKind.Grass,
        Biome.Island => GroundKind.Grass,
        Biome.Volcanic => GroundKind.Ash,
    };

    /// <summary>One sample's ground, without blending into its neighbours.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="palette">Palette, for the beach, rock and snow thresholds.</param>
    /// <param name="x">Sample column.</param>
    /// <param name="z">Sample row.</param>
    /// <returns>The ground.</returns>
    public static Ground At(TerrainField field, BiomePalette palette, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(palette);
        return Overlaid(field, palette, x, z, new Ground(Bare(field, palette, x, z), GroundKind.Rock, 0f));
    }

    /// <summary>A sample's own texture: its biome's, or sand under the sea.</summary>
    private static GroundKind Bare(TerrainField field, BiomePalette palette, int x, int z)
    {
        var biome = field.Biomes[(z * field.Resolution) + x];
        return biome == Biome.Sea || field.GetHeight(x, z) <= 0f ? GroundKind.Sand : BaseOf(biome);
    }

    /// <summary>
    /// Rock on steep slopes, snow on cold heights or the beach's sand as the land nears the sea, over
    /// a ground, where they show more than its blend: as the colour map fades them in.
    /// </summary>
    private static Ground Overlaid(TerrainField field, BiomePalette palette, int x, int z, Ground ground)
    {
        var height = field.GetHeight(x, z);
        var (overlay, blend) = (GroundKind.Rock, SmoothStep(palette.RockSlopeStart, palette.RockSlopeFull, field.GetSlope(x, z)));
        var snow = SmoothStep(palette.SnowStartHeight, palette.SnowStartHeight + palette.SnowBlendRange, height);
        var sand = ground.Base == GroundKind.Sand ? 0f : 1f - SmoothStep(0f, palette.BeachMaxHeight, height);
        (overlay, blend) = snow > blend ? (GroundKind.Snow, snow) : (overlay, blend);
        (overlay, blend) = sand > blend ? (GroundKind.Sand, sand) : (overlay, blend);
        return blend > ground.Blend ? ground with { Overlay = overlay, Blend = blend } : ground;
    }

    /// <summary>
    /// A sample's ground blended into its neighbours': its own texture as the base and, near a
    /// border, the commonest other texture within a few metres as the overlay, by its share.
    /// </summary>
    private static Ground Blended(TerrainField field, GroundKind[] bare, int x, int z)
    {
        var n = field.Resolution;
        var own = bare[(z * n) + x];
        GroundKind At(int r, int dx, int dz) => bare[(Math.Clamp(z + (dz * r), 0, n - 1) * n) + Math.Clamp(x + (dx * r), 0, n - 1)];

        // Far from any border the outermost ring is all the sample's own ground.
        var near = false;
        foreach (var (dx, dz) in Ring)
        {
            near |= At(BorderSamples, dx, dz) != own;
        }

        if (!near)
        {
            return new Ground(own, GroundKind.Rock, 0f);
        }

        // Counted over rings 1 to BorderSamples out, the other ground's share rises towards the border.
        Span<int> counts = stackalloc int[16];
        var total = 1;
        for (var r = 1; r <= BorderSamples; r++)
        {
            foreach (var (dx, dz) in Ring)
            {
                counts[(int)At(r, dx, dz)]++;
                total++;
            }
        }

        var (other, most) = (own, 0);
        for (var k = 0; k < counts.Length; k++)
        {
            if (k != (int)own && counts[k] > most)
            {
                (other, most) = ((GroundKind)k, counts[k]);
            }
        }

        // At most a half: across the border each side shows a little more of itself, and the two meet in an even mix.
        return new Ground(own, other, Math.Min(0.5f, (float)most / total));
    }

    // The eight directions of a ring of samples around a sample.
    private static readonly (int Dx, int Dz)[] Ring = [(-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1)];

    /// <summary>Terrain3D's control word for a ground.</summary>
    /// <param name="ground">The ground.</param>
    /// <param name="angle">The texture's turn, 0..15 sixteenths of a circle.</param>
    /// <returns>The word.</returns>
    public static uint Encode(Ground ground, int angle) =>
        ((uint)ground.Base << BaseShift) | ((uint)ground.Overlay << OverlayShift)
        | ((uint)MathF.Round(Math.Clamp(ground.Blend, 0f, 1f) * 255f) << BlendShift) | ((uint)(angle & 0xF) << AngleShift);

    /// <summary>The control map: one word per sample, row-major.</summary>
    /// <param name="field">Terrain.</param>
    /// <param name="palette">Palette.</param>
    /// <returns>The words.</returns>
    public static uint[] Compute(TerrainField field, BiomePalette palette)
    {
        ArgumentNullException.ThrowIfNull(field);
        var n = field.Resolution;
        var bare = new GroundKind[n * n];

        // Every sample is worked out on its own, so rows run in parallel with the same result.
        System.Threading.Tasks.Parallel.For(0, n, z =>
        {
            for (var x = 0; x < n; x++)
            {
                bare[(z * n) + x] = Bare(field, palette, x, z);
            }
        });

        var words = new uint[n * n];
        System.Threading.Tasks.Parallel.For(0, n, z =>
        {
            for (var x = 0; x < n; x++)
            {
                var ground = Overlaid(field, palette, x, z, Blended(field, bare, x, z));
                // Rock keeps its strata level; everything else turns at random to hide the tiling.
                var angle = ground.Base == GroundKind.Rock ? 0 : (int)(PresentationHash.Mix(AngleSalt, (ulong)x, (ulong)z) & 0xF);
                words[(z * n) + x] = Encode(ground, angle);
            }
        });

        return words;
    }

    /// <summary>
    /// The tint map: the colour map's hues at <see cref="GroundSettings.TintStrength"/>, each brought
    /// to the brightness of white, so the textures keep their own light and dark.
    /// </summary>
    /// <param name="rgba">The colour map from <see cref="TerrainColorizer.ComputeRgba"/>; changed in place.</param>
    /// <param name="ground">Ground settings.</param>
    public static void Tint(byte[] rgba, GroundSettings ground)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(ground);
        var strength = ground.TintStrength;
        for (var i = 0; i < rgba.Length; i += TerrainColorizer.BytesPerPixel)
        {
            var (r, g, b) = (rgba[i] / 255f, rgba[i + 1] / 255f, rgba[i + 2] / 255f);
            var luminance = Math.Max(0.05f, (0.2126f * r) + (0.7152f * g) + (0.0722f * b));
            rgba[i] = RgbColor.ToByte(1f + (((r / luminance) - 1f) * strength));
            rgba[i + 1] = RgbColor.ToByte(1f + (((g / luminance) - 1f) * strength));
            rgba[i + 2] = RgbColor.ToByte(1f + (((b / luminance) - 1f) * strength));
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
