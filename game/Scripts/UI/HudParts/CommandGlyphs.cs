using System;
using Godot;
using Pantheon.Game.Controls;

namespace Pantheon.Client.UI;

/// <summary>The pictures of the god's bar and the Veil toggle (ADR-068), drawn in lines.</summary>
internal enum CommandGlyph
{
    /// <summary>An open book.</summary>
    Book,

    /// <summary>A house.</summary>
    House,

    /// <summary>Two figures.</summary>
    Gods,

    /// <summary>A temple's front.</summary>
    Council,

    /// <summary>A star of rays.</summary>
    Domains,

    /// <summary>An hourglass.</summary>
    History,

    /// <summary>A folded map.</summary>
    Map,

    /// <summary>A globe.</summary>
    Overview,

    /// <summary>A keyboard.</summary>
    Keys,

    /// <summary>A beetle.</summary>
    Debug,

    /// <summary>Three bars.</summary>
    Menu,

    /// <summary>An eye under a veil.</summary>
    Veil,
}

/// <summary>Draws the god's bar's pictures in lines, in the miracle glyphs' manner (ADR-068).</summary>
internal static class CommandGlyphs
{
    /// <summary>A bar command's picture.</summary>
    /// <param name="command">The command.</param>
    /// <returns>Its picture.</returns>
    public static CommandGlyph For(BarCommand command) => command switch
    {
        BarCommand.Book => CommandGlyph.Book,
        BarCommand.Villages => CommandGlyph.House,
        BarCommand.Gods => CommandGlyph.Gods,
        BarCommand.Council => CommandGlyph.Council,
        BarCommand.Domains => CommandGlyph.Domains,
        BarCommand.History => CommandGlyph.History,
        BarCommand.Map => CommandGlyph.Map,
        BarCommand.Overview => CommandGlyph.Overview,
        BarCommand.Keys => CommandGlyph.Keys,
        BarCommand.Debug => CommandGlyph.Debug,
        BarCommand.Menu => CommandGlyph.Menu,
    };

    /// <summary>Draws a picture.</summary>
    /// <param name="canvas">Where to draw.</param>
    /// <param name="glyph">The picture.</param>
    /// <param name="centre">Its centre.</param>
    /// <param name="size">Its height and width.</param>
    /// <param name="color">Its lines' colour.</param>
    public static void Draw(CanvasItem canvas, CommandGlyph glyph, Vector2 centre, float size, Color color)
    {
        var pen = new Pen(canvas, centre, size / 2f, color);
        switch (glyph)
        {
            case CommandGlyph.Book:
                pen.Shape(true, (-0.9f, -0.6f), (-0.04f, -0.42f), (-0.04f, 0.72f), (-0.9f, 0.55f));
                pen.Shape(true, (0.9f, -0.6f), (0.04f, -0.42f), (0.04f, 0.72f), (0.9f, 0.55f));
                pen.Line((-0.68f, -0.22f), (-0.26f, -0.13f));
                pen.Line((-0.68f, 0.08f), (-0.26f, 0.17f));
                pen.Line((0.68f, -0.22f), (0.26f, -0.13f));
                pen.Line((0.68f, 0.08f), (0.26f, 0.17f));
                break;
            case CommandGlyph.House:
                pen.Shape(false, (-0.9f, -0.02f), (0f, -0.85f), (0.9f, -0.02f));
                pen.Shape(true, (-0.62f, -0.2f), (-0.62f, 0.8f), (0.62f, 0.8f), (0.62f, -0.2f));
                pen.Shape(false, (-0.17f, 0.8f), (-0.17f, 0.3f), (0.17f, 0.3f), (0.17f, 0.8f));
                break;
            case CommandGlyph.Gods:
                pen.Ring((-0.36f, -0.4f), 0.24f);
                pen.Arc((-0.36f, 0.62f), 0.5f, MathF.PI, MathF.Tau);
                pen.Ring((0.42f, -0.28f), 0.2f);
                pen.Arc((0.42f, 0.66f), 0.42f, MathF.PI * 1.08f, MathF.Tau);
                break;
            case CommandGlyph.Council:
                pen.Shape(true, (-0.92f, -0.38f), (0f, -0.88f), (0.92f, -0.38f));
                pen.Line((-0.6f, -0.24f), (-0.6f, 0.6f));
                pen.Line((0f, -0.24f), (0f, 0.6f));
                pen.Line((0.6f, -0.24f), (0.6f, 0.6f));
                pen.Line((-0.92f, 0.78f), (0.92f, 0.78f));
                break;
            case CommandGlyph.Domains:
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * MathF.Tau / 8f;
                    var reach = i % 2 == 0 ? 0.95f : 0.72f;
                    pen.Line((MathF.Cos(angle) * 0.42f, MathF.Sin(angle) * 0.42f), (MathF.Cos(angle) * reach, MathF.Sin(angle) * reach));
                }

                pen.Ring((0f, 0f), 0.28f);
                break;
            case CommandGlyph.History:
                pen.Line((-0.7f, -0.85f), (0.7f, -0.85f));
                pen.Line((-0.7f, 0.85f), (0.7f, 0.85f));
                pen.Shape(false, (-0.52f, -0.85f), (0.52f, -0.85f), (0.08f, 0f), (0.52f, 0.85f), (-0.52f, 0.85f), (-0.08f, 0f), (-0.52f, -0.85f));
                pen.Fill((-0.3f, 0.85f), (0f, 0.45f), (0.3f, 0.85f));
                break;
            case CommandGlyph.Map:
                pen.Shape(true, (-0.92f, -0.62f), (-0.3f, -0.82f), (0.3f, -0.62f), (0.92f, -0.82f), (0.92f, 0.62f), (0.3f, 0.82f), (-0.3f, 0.62f), (-0.92f, 0.82f));
                pen.Line((-0.3f, -0.82f), (-0.3f, 0.62f));
                pen.Line((0.3f, -0.62f), (0.3f, 0.82f));
                break;
            case CommandGlyph.Overview:
                pen.Ring((0f, 0f), 0.88f);
                pen.Oval(0.38f, 0.88f);
                pen.Line((-0.88f, 0f), (0.88f, 0f));
                pen.Arc((0f, 1.25f), 0.98f, MathF.PI * 1.27f, MathF.PI * 1.73f);
                pen.Arc((0f, -1.25f), 0.98f, MathF.PI * 0.27f, MathF.PI * 0.73f);
                break;
            case CommandGlyph.Keys:
                pen.Shape(true, (-0.95f, -0.55f), (0.95f, -0.55f), (0.95f, 0.6f), (-0.95f, 0.6f));
                for (var i = 0; i < 4; i++)
                {
                    pen.Dot((-0.6f + (i * 0.4f), -0.2f));
                }

                for (var i = 0; i < 3; i++)
                {
                    pen.Dot((-0.4f + (i * 0.4f), 0.08f));
                }

                pen.Line((-0.42f, 0.34f), (0.42f, 0.34f));
                break;
            case CommandGlyph.Debug:
                pen.Oval(0.42f, 0.55f, (0f, 0.18f));
                pen.Ring((0f, -0.55f), 0.2f);
                pen.Line((0f, -0.3f), (0f, 0.72f));
                foreach (var y in new[] { -0.1f, 0.18f, 0.46f })
                {
                    pen.Line((-0.42f, y), (-0.85f, y - 0.12f));
                    pen.Line((0.42f, y), (0.85f, y - 0.12f));
                }

                pen.Line((-0.12f, -0.72f), (-0.32f, -0.95f));
                pen.Line((0.12f, -0.72f), (0.32f, -0.95f));
                break;
            case CommandGlyph.Menu:
                pen.Line((-0.72f, -0.55f), (0.72f, -0.55f));
                pen.Line((-0.72f, 0f), (0.72f, 0f));
                pen.Line((-0.72f, 0.55f), (0.72f, 0.55f));
                break;
            case CommandGlyph.Veil:
                pen.Arc((0f, 0.62f), 1.05f, MathF.PI * 1.2f, MathF.PI * 1.8f);
                pen.Arc((0f, -0.62f), 1.05f, MathF.PI * 0.2f, MathF.PI * 0.8f);
                pen.Ring((0f, 0f), 0.24f);
                pen.Shape(false, (-0.95f, -0.55f), (-0.4f, -0.9f), (0.4f, -0.9f), (0.95f, -0.55f));
                break;
        }
    }

    /// <summary>Draws lines given in halves of the picture's size about its centre.</summary>
    private readonly struct Pen(CanvasItem canvas, Vector2 centre, float s, Color color)
    {
        private float Width => MathF.Max(1.5f, s * 0.13f);

        public void Line((float X, float Y) from, (float X, float Y) to) => canvas.DrawLine(At(from), At(to), color, Width, true);

        public void Shape(bool closed, params (float X, float Y)[] points)
        {
            var line = new Vector2[points.Length + (closed ? 1 : 0)];
            for (var i = 0; i < points.Length; i++)
            {
                line[i] = At(points[i]);
            }

            if (closed)
            {
                line[^1] = line[0];
            }

            canvas.DrawPolyline(line, color, Width, true);
        }

        public void Fill(params (float X, float Y)[] points)
        {
            var shape = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                shape[i] = At(points[i]);
            }

            canvas.DrawColoredPolygon(shape, color);
        }

        public void Ring((float X, float Y) middle, float radius) => canvas.DrawArc(At(middle), radius * s, 0f, MathF.Tau, 28, color, Width, true);

        public void Arc((float X, float Y) middle, float radius, float from, float to) => canvas.DrawArc(At(middle), radius * s, from, to, 20, color, Width, true);

        public void Dot((float X, float Y) middle) => canvas.DrawRect(new Rect2(At(middle) - new Vector2(s * 0.1f, s * 0.1f), new Vector2(s * 0.2f, s * 0.2f)), color);

        public void Oval(float rx, float ry, (float X, float Y) middle = default)
        {
            var points = new Vector2[29];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = i * MathF.Tau / (points.Length - 1);
                points[i] = At((middle.X + (MathF.Cos(angle) * rx), middle.Y + (MathF.Sin(angle) * ry)));
            }

            canvas.DrawPolyline(points, color, Width, true);
        }

        private Vector2 At((float X, float Y) point) => centre + new Vector2(point.X * s, point.Y * s);
    }
}
