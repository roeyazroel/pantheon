using System;

namespace Pantheon.Game.Presentation;

/// <summary>How the territories are painted (ADR-069), in metres on the ground and in opacity.</summary>
/// <param name="Line">The player's border line's width, on its side.</param>
/// <param name="Outline">The dark edge outside the player's line, so it reads on sand and grass alike.</param>
/// <param name="Glow">How far inside the border its glow fades out.</param>
/// <param name="GlowOpacity">The glow's opacity at the border.</param>
/// <param name="Wash">The colour over the whole of the player's own land.</param>
/// <param name="RivalOpacity">How much of the player's opacity a rival's border has; its line is also half as wide.</param>
public readonly record struct TerritoryLook(float Line, float Outline, float Glow, float GlowOpacity, float Wash, float RivalOpacity)
{
    /// <summary>
    /// The look for an island of a width seen from a distance: the widths grow with the island and with
    /// the camera's height, so the borders read from close by and from the whole island's view alike.
    /// </summary>
    /// <param name="extent">The island's width, in metres.</param>
    /// <param name="distance">The camera's distance, in metres.</param>
    /// <returns>The look.</returns>
    public static TerritoryLook For(float extent, float distance = 380f)
    {
        var scale = Math.Max(1f, extent / 2048f) * Math.Clamp(distance / 380f, 0.7f, 4f);
        return new TerritoryLook(4.5f * scale, 2f * scale, 34f * scale, 0.5f, 0.12f, 0.6f);
    }

    /// <summary>The zoom band a distance falls in: the look changes only between bands, each a doubling.</summary>
    /// <param name="distance">The camera's distance, in metres.</param>
    /// <returns>The band.</returns>
    public static int Band(float distance) => (int)MathF.Round(MathF.Log2(Math.Clamp(distance, 100f, 8000f) / 380f));

    /// <summary>The distance a band is painted for.</summary>
    /// <param name="band">The band.</param>
    /// <returns>The distance, in metres.</returns>
    public static float DistanceOf(int band) => 380f * MathF.Pow(2f, band);
}

/// <summary>
/// The territories painted for the ground (ADR-069): one RGBA pixel per patch of the island, which the
/// client lays over the land as a decal. Each god's border is a bright line on its own side, so two
/// neighbours' borders show both colours; a glow fades inward from it; and the player's own land has a
/// wash of its colour, its border edged in dark so it reads on any ground. Only land is painted: a
/// territory's reach over the sea is not shown. Rows run along +Z and columns along +X, as the map
/// overlays'. Pure.
/// </summary>
public static class TerritoryImage
{
    /// <summary>The image's side for an island: about a pixel and a half a metre, within limits.</summary>
    /// <param name="extent">The island's width, in metres.</param>
    /// <returns>Pixels along a side.</returns>
    public static int SideFor(float extent) => Math.Clamp((int)(extent / 1.5f), 512, 1536);

    /// <summary>Paints the territories.</summary>
    /// <param name="territories">The territories.</param>
    /// <param name="side">Pixels along a side.</param>
    /// <param name="colorOf">Each god's colour.</param>
    /// <param name="localGodId">The player's god, whose land is washed and whose border is strongest.</param>
    /// <param name="look">The widths and opacities.</param>
    /// <param name="land">Whether a point (X, Z) is land; everywhere when <c>null</c>.</param>
    /// <returns>RGBA bytes, row by row.</returns>
    public static byte[] Render(Territories territories, int side, Func<int, RgbColor> colorOf, int? localGodId, TerritoryLook look, Func<float, float, bool>? land = null)
    {
        ArgumentNullException.ThrowIfNull(territories);
        ArgumentNullException.ThrowIfNull(colorOf);
        var rgba = new byte[side * side * 4];
        var pixel = territories.Extent / side;
        var colors = new RgbColor[territories.GodIds.Count];
        for (var g = 0; g < colors.Length; g++)
        {
            colors[g] = colorOf(territories.GodIds[g]);
        }

        for (var row = 0; row < side; row++)
        {
            var z = (row + 0.5f) * pixel;
            for (var column = 0; column < side; column++)
            {
                var x = (column + 0.5f) * pixel;
                var (god, alpha, edge) = Paint(territories, x, z, pixel, localGodId, look);
                if (alpha <= 0f || (land is not null && !land(x, z)))
                {
                    continue;
                }

                var color = edge ? Edge : colors[god];
                var at = ((row * side) + column) * 4;
                rgba[at] = Byte(color.R);
                rgba[at + 1] = Byte(color.G);
                rgba[at + 2] = Byte(color.B);
                rgba[at + 3] = Byte(alpha);
            }
        }

        return rgba;
    }

    /// <summary>The god whose land a point is (or nearly is) and how strongly it is painted there.</summary>
    /// <param name="territories">The territories.</param>
    /// <param name="x">World X, in metres.</param>
    /// <param name="z">World Z, in metres.</param>
    /// <param name="pixel">A pixel's width, in metres, for the border's soft outer edge.</param>
    /// <param name="localGodId">The player's god.</param>
    /// <param name="look">The widths and opacities.</param>
    /// <returns>The god's index, the opacity (0..1; 0 outside every territory) and whether it is the dark edge outside the player's border.</returns>
    public static (int God, float Alpha, bool Edge) Paint(Territories territories, float x, float z, float pixel, int? localGodId, TerritoryLook look)
    {
        ArgumentNullException.ThrowIfNull(territories);
        var best = -1;
        var bestLead = float.MinValue;
        for (var g = 0; g < territories.GodIds.Count; g++)
        {
            var lead = territories.Lead(g, x, z);
            if (lead > bestLead)
            {
                (best, bestLead) = (g, lead);
            }
        }

        if (best < 0)
        {
            return (0, 0f, false);
        }

        var (_, inside) = territories.LeadAndSlope(best, x, z);
        var mine = territories.GodIds[best] == localGodId;
        if (inside < 0f)
        {
            // Outside: the dark edge of the player's border, softening over a pixel; nothing outside a rival's.
            var outline = mine ? look.Outline : 0f;
            var away = -inside;
            return away < outline ? (best, 0.5f, true) : away < outline + pixel ? (best, 0.5f * (1f - ((away - outline) / pixel)), outline > 0f) : (best, 0f, false);
        }

        // The line is solid on the god's side and softens over a pixel inside, so it never stair-steps.
        var width = mine ? look.Line : look.Line * 0.5f;
        var line = inside < width ? 1f : inside < width + pixel ? 1f - ((inside - width) / pixel) : 0f;
        var fade = inside < look.Glow ? 1f - (inside / look.Glow) : 0f;
        var alpha = Math.Max(line * 0.95f, fade * fade * look.GlowOpacity);
        return (best, mine ? Math.Max(alpha, look.Wash) : alpha * look.RivalOpacity, false);
    }

    /// <summary>The dark edge outside the player's border.</summary>
    private static readonly RgbColor Edge = new(0.06f, 0.05f, 0.04f);

    /// <summary>A channel from 0..1 to a byte.</summary>
    private static byte Byte(float value) => (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
