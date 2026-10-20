using System;
using System.Collections.Generic;
using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.UI;

/// <summary>The pictures on the miracle bar (ADR-059), drawn as vectors so they stay sharp at any size.</summary>
internal enum MiracleGlyph
{
    /// <summary>An ear of wheat: food and abundance.</summary>
    Wheat,

    /// <summary>A heart: healing.</summary>
    Heart,

    /// <summary>A sprout: blessed crops and regrowth.</summary>
    Sprout,

    /// <summary>A lightning bolt.</summary>
    Bolt,

    /// <summary>A cloud with rain.</summary>
    Rain,

    /// <summary>A flame.</summary>
    Flame,

    /// <summary>A shield.</summary>
    Shield,

    /// <summary>Cracked ground.</summary>
    Quake,

    /// <summary>A drop of water.</summary>
    Drop,

    /// <summary>A radiant sun: the holy miracles.</summary>
    Sun,

    /// <summary>A crescent moon: dread and discord.</summary>
    Moon,

    /// <summary>A spiral: a storm turned.</summary>
    Swirl,

    /// <summary>A wave.</summary>
    Wave,

    /// <summary>A mountain: the land reshaped.</summary>
    Mountain,

    /// <summary>A four-pointed sparkle: anything else.</summary>
    Sparkle,
}

/// <summary>Draws the miracle bar's glyphs.</summary>
internal static class MiracleGlyphs
{
    /// <summary>The glyph for a miracle: by its id where the id says more, else by its look.</summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="style">Its look (ADR-055).</param>
    /// <returns>The glyph.</returns>
    public static MiracleGlyph For(string miracleId, MiracleEffectStyle style) => miracleId switch
    {
        "bless_crops" or "regrowth" => MiracleGlyph.Sprout,
        "anoint" => MiracleGlyph.Sun,
        "raise_island" or "split_mountain" or "upheaval" => MiracleGlyph.Mountain,
        "turn_river" or "tidal_wave" => MiracleGlyph.Wave,
        _ => style switch
        {
            MiracleEffectStyle.Harvest => MiracleGlyph.Wheat,
            MiracleEffectStyle.Healing => MiracleGlyph.Heart,
            MiracleEffectStyle.Strike or MiracleEffectStyle.Bolt => MiracleGlyph.Bolt,
            MiracleEffectStyle.Rain => MiracleGlyph.Rain,
            MiracleEffectStyle.Flames => MiracleGlyph.Flame,
            MiracleEffectStyle.Shield => MiracleGlyph.Shield,
            MiracleEffectStyle.Quake => MiracleGlyph.Quake,
            MiracleEffectStyle.Fountain => MiracleGlyph.Drop,
            MiracleEffectStyle.Holy => MiracleGlyph.Sun,
            MiracleEffectStyle.Shadow => MiracleGlyph.Moon,
            MiracleEffectStyle.Storm => MiracleGlyph.Swirl,
            MiracleEffectStyle.Wave => MiracleGlyph.Wave,
            MiracleEffectStyle.Shower or MiracleEffectStyle.Rise => MiracleGlyph.Sparkle,
        },
    };

    /// <summary>Draws a glyph.</summary>
    /// <param name="canvas">What to draw on.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="centre">Its centre.</param>
    /// <param name="size">Its height and width.</param>
    /// <param name="color">Its main colour.</param>
    public static void Draw(CanvasItem canvas, MiracleGlyph glyph, Vector2 centre, float size, Color color)
    {
        var s = size / 2f;
        var dark = color.Darkened(0.55f);
        switch (glyph)
        {
            case MiracleGlyph.Wheat:
                canvas.DrawLine(centre + new Vector2(0, s), centre + new Vector2(0, -s * 0.85f), dark, s * 0.09f, true);
                for (var i = 0; i < 4; i++)
                {
                    var y = (s * 0.55f) - (i * s * 0.36f);
                    Fill(canvas, Ellipse(centre + new Vector2(-s * 0.2f, -y + (s * 0.3f)), s * 0.13f, s * 0.24f, 0.6f), color, dark);
                    Fill(canvas, Ellipse(centre + new Vector2(s * 0.2f, -y + (s * 0.3f)), s * 0.13f, s * 0.24f, -0.6f), color, dark);
                }

                Fill(canvas, Ellipse(centre + new Vector2(0, -s * 0.78f), s * 0.12f, s * 0.22f, 0f), color, dark);
                break;
            case MiracleGlyph.Heart:
                Fill(canvas, HeartShape(centre, s * 0.95f), color, dark);
                break;
            case MiracleGlyph.Sprout:
                canvas.DrawPolyline(Curve(centre + new Vector2(0, s * 0.9f), centre + new Vector2(s * 0.1f, 0), centre + new Vector2(0, -s * 0.3f)), dark, s * 0.1f, true);
                Fill(canvas, Leaf(centre + new Vector2(0, -s * 0.25f), centre + new Vector2(-s * 0.85f, -s * 0.6f), s * 0.32f), color, dark);
                Fill(canvas, Leaf(centre + new Vector2(0, -s * 0.05f), centre + new Vector2(s * 0.8f, -s * 0.35f), s * 0.3f), color, dark);
                canvas.DrawLine(centre + new Vector2(-s * 0.7f, s * 0.9f), centre + new Vector2(s * 0.7f, s * 0.9f), dark, s * 0.08f, true);
                break;
            case MiracleGlyph.Bolt:
                Fill(canvas, Points(centre, s, (0.2f, -1f), (-0.5f, 0.1f), (-0.02f, 0.1f), (-0.25f, 1f), (0.55f, -0.2f), (0.05f, -0.2f), (0.38f, -1f)), color, dark);
                break;
            case MiracleGlyph.Rain:
                for (var i = -1; i <= 1; i++)
                {
                    Fill(canvas, Drop(centre + new Vector2(i * s * 0.42f, s * 0.62f), s * 0.17f), color.Lightened(0.15f), dark);
                }

                Fill(canvas, CloudShape(centre + new Vector2(0, -s * 0.2f), s * 0.85f), new Color(0.88f, 0.9f, 0.95f), dark);
                break;
            case MiracleGlyph.Flame:
                Fill(canvas, Drop(centre + new Vector2(0, s * 0.05f), s * 0.9f), color, dark);
                Fill(canvas, Drop(centre + new Vector2(0, s * 0.35f), s * 0.5f), new Color(1f, 0.92f, 0.5f), null);
                break;
            case MiracleGlyph.Shield:
                Fill(canvas, Points(centre, s, (-0.75f, -0.85f), (0.75f, -0.85f), (0.72f, 0.05f), (0.45f, 0.6f), (0f, 0.95f), (-0.45f, 0.6f), (-0.72f, 0.05f)), color, dark);
                canvas.DrawLine(centre + new Vector2(0, -s * 0.6f), centre + new Vector2(0, s * 0.6f), dark, s * 0.1f, true);
                canvas.DrawLine(centre + new Vector2(-s * 0.5f, -s * 0.15f), centre + new Vector2(s * 0.5f, -s * 0.15f), dark, s * 0.1f, true);
                break;
            case MiracleGlyph.Quake:
                Fill(canvas, Points(centre, s, (-0.95f, -0.05f), (-0.12f, -0.2f), (0.0f, 0.15f), (-0.18f, 0.4f), (-0.95f, 0.6f)), color, dark);
                Fill(canvas, Points(centre, s, (0.95f, 0.2f), (0.18f, 0.02f), (0.3f, 0.35f), (0.1f, 0.62f), (0.95f, 0.85f)), color.Darkened(0.15f), dark);
                canvas.DrawPolyline(Zigzag(centre + new Vector2(0, -s * 0.9f), centre + new Vector2(0, s * 0.95f), s * 0.18f, 5), dark, s * 0.08f, true);
                break;
            case MiracleGlyph.Drop:
                Fill(canvas, Drop(centre + new Vector2(0, s * 0.12f), s * 0.85f), color, dark);
                canvas.DrawArc(centre + new Vector2(-s * 0.15f, s * 0.35f), s * 0.3f, 2.3f, 3.4f, 8, new Color(1, 1, 1, 0.7f), s * 0.08f, true);
                break;
            case MiracleGlyph.Sun:
                for (var i = 0; i < 12; i++)
                {
                    var angle = i * MathF.Tau / 12f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    canvas.DrawLine(centre + (direction * s * 0.55f), centre + (direction * s * (i % 2 == 0 ? 0.98f : 0.8f)), color, s * 0.09f, true);
                }

                canvas.DrawCircle(centre, s * 0.45f, color.Lightened(0.25f));
                canvas.DrawArc(centre, s * 0.45f, 0f, MathF.Tau, 24, dark, s * 0.06f, true);
                break;
            case MiracleGlyph.Moon:
                Fill(canvas, Crescent(centre, s * 0.85f), color.Lightened(0.2f), dark);
                break;
            case MiracleGlyph.Swirl:
                canvas.DrawPolyline(Spiral(centre, s * 0.95f, 2.4f), color.Lightened(0.2f), s * 0.14f, true);
                break;
            case MiracleGlyph.Wave:
                Fill(canvas, WaveShape(centre, s), color, dark);
                break;
            case MiracleGlyph.Mountain:
                Fill(canvas, Points(centre, s, (-0.95f, 0.8f), (-0.25f, -0.55f), (0.05f, -0.05f), (0.35f, -0.8f), (0.95f, 0.8f)), color, dark);
                Fill(canvas, Points(centre, s, (0.2f, -0.5f), (0.35f, -0.8f), (0.5f, -0.5f), (0.35f, -0.4f)), new Color(0.95f, 0.97f, 1f), null);
                break;
            case MiracleGlyph.Sparkle:
                Fill(canvas, Points(centre, s, (0f, -1f), (0.2f, -0.2f), (1f, 0f), (0.2f, 0.2f), (0f, 1f), (-0.2f, 0.2f), (-1f, 0f), (-0.2f, -0.2f)), color, dark);
                break;
        }
    }

    /// <summary>Fills a shape and outlines it.</summary>
    private static void Fill(CanvasItem canvas, Vector2[] points, Color fill, Color? outline)
    {
        canvas.DrawColoredPolygon(points, fill);
        if (outline is { } line)
        {
            var closed = new Vector2[points.Length + 1];
            points.CopyTo(closed, 0);
            closed[^1] = points[0];
            canvas.DrawPolyline(closed, line, 1.5f, true);
        }
    }

    /// <summary>Points given in halves of the glyph's size about its centre.</summary>
    private static Vector2[] Points(Vector2 centre, float s, params (float X, float Y)[] points)
    {
        var result = new Vector2[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            result[i] = centre + new Vector2(points[i].X * s, points[i].Y * s);
        }

        return result;
    }

    /// <summary>An ellipse, turned by an angle.</summary>
    private static Vector2[] Ellipse(Vector2 centre, float rx, float ry, float turn)
    {
        var points = new Vector2[16];
        for (var i = 0; i < points.Length; i++)
        {
            var a = i * MathF.Tau / points.Length;
            points[i] = centre + new Vector2(MathF.Cos(a) * rx, MathF.Sin(a) * ry).Rotated(turn);
        }

        return points;
    }

    /// <summary>A drop or a flame: round below, pointed above.</summary>
    private static Vector2[] Drop(Vector2 centre, float s)
    {
        var points = new List<Vector2> { centre + new Vector2(0, -s) };
        for (var i = 0; i <= 16; i++)
        {
            var a = -0.25f + (i * (MathF.PI + 0.5f) / 16f);
            points.Add(centre + new Vector2(MathF.Cos(a) * s * 0.55f, (s * 0.35f) + (MathF.Sin(a) * s * 0.55f)));
        }

        return [.. points];
    }

    /// <summary>A heart from its classic curve.</summary>
    private static Vector2[] HeartShape(Vector2 centre, float s)
    {
        var points = new Vector2[32];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i * MathF.Tau / points.Length;
            var x = 16f * MathF.Pow(MathF.Sin(t), 3);
            var y = (13f * MathF.Cos(t)) - (5f * MathF.Cos(2 * t)) - (2f * MathF.Cos(3 * t)) - MathF.Cos(4 * t);
            points[i] = centre + (new Vector2(x, -y - 2f) * (s / 17f));
        }

        return points;
    }

    /// <summary>A leaf from its base to its tip.</summary>
    private static Vector2[] Leaf(Vector2 from, Vector2 to, float width)
    {
        var along = to - from;
        var side = new Vector2(-along.Y, along.X).Normalized() * width;
        var points = new Vector2[18];
        for (var i = 0; i < 9; i++)
        {
            var t = i / 8f;
            points[i] = from + (along * t) + (side * MathF.Sin(t * MathF.PI));
            points[17 - i] = from + (along * t) - (side * MathF.Sin(t * MathF.PI) * 0.6f);
        }

        return points;
    }

    /// <summary>A gentle curve through three points.</summary>
    private static Vector2[] Curve(Vector2 a, Vector2 b, Vector2 c)
    {
        var points = new Vector2[10];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i / 9f;
            points[i] = ((1 - t) * (1 - t) * a) + (2 * (1 - t) * t * b) + (t * t * c);
        }

        return points;
    }

    /// <summary>A cloud of overlapping lobes over a flat base.</summary>
    private static Vector2[] CloudShape(Vector2 centre, float s)
    {
        var points = new List<Vector2>();
        (Vector2 C, float R, float From, float To)[] lobes =
        [
            (new Vector2(-0.55f, 0.15f), 0.35f, 1.6f, 4.4f),
            (new Vector2(-0.15f, -0.2f), 0.45f, 3.4f, 5.6f),
            (new Vector2(0.35f, -0.05f), 0.42f, 4.2f, 6.6f),
            (new Vector2(0.65f, 0.2f), 0.3f, 5.2f, 7.9f),
        ];
        foreach (var lobe in lobes)
        {
            for (var i = 0; i <= 8; i++)
            {
                var a = lobe.From + ((lobe.To - lobe.From) * i / 8f);
                points.Add(centre + ((lobe.C + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * lobe.R)) * s));
            }
        }

        return [.. points];
    }

    /// <summary>A crescent: an outer arc and a deeper inner one.</summary>
    private static Vector2[] Crescent(Vector2 centre, float s)
    {
        var points = new List<Vector2>();
        for (var i = 0; i <= 20; i++)
        {
            var a = 0.6f + (i * (MathF.Tau - 1.2f) / 20f);
            points.Add(centre + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * s));
        }

        for (var i = 20; i >= 0; i--)
        {
            var a = 1.0f + (i * (MathF.Tau - 2f) / 20f);
            points.Add(centre + new Vector2(s * 0.45f, 0) + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * s * 0.78f));
        }

        return [.. points];
    }

    /// <summary>A spiral inwards.</summary>
    private static Vector2[] Spiral(Vector2 centre, float s, float turns)
    {
        var points = new Vector2[48];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (points.Length - 1f);
            var a = t * turns * MathF.Tau;
            points[i] = centre + (new Vector2(MathF.Cos(a), MathF.Sin(a)) * s * (1f - (t * 0.85f)));
        }

        return points;
    }

    /// <summary>A breaking wave over the water.</summary>
    private static Vector2[] WaveShape(Vector2 centre, float s)
    {
        var points = new List<Vector2> { centre + new Vector2(-s, s * 0.85f) };
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20f;
            var x = -1f + (t * 2f);
            var y = 0.35f - (0.75f * MathF.Sin(t * MathF.PI * 0.9f)) + (0.15f * MathF.Sin(t * 9f));
            points.Add(centre + new Vector2(x * s, y * s));
        }

        points.Add(centre + new Vector2(s * 0.75f, -s * 0.05f));
        points.Add(centre + new Vector2(s * 0.45f, s * 0.15f));
        points.Add(centre + new Vector2(s, s * 0.85f));
        return [.. points];
    }

    /// <summary>A zigzag crack from one point to another.</summary>
    private static Vector2[] Zigzag(Vector2 from, Vector2 to, float width, int teeth)
    {
        var points = new Vector2[teeth + 1];
        var along = to - from;
        var side = new Vector2(-along.Y, along.X).Normalized() * width;
        for (var i = 0; i <= teeth; i++)
        {
            points[i] = from + (along * i / teeth) + (i % 2 == 0 ? Vector2.Zero : side * (i % 4 == 1 ? 1f : -1f));
        }

        return points;
    }
}
