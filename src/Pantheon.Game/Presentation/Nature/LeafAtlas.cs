using System;
using System.Collections.Generic;

namespace Pantheon.Game.Presentation.Nature;

/// <summary>A cell of the leaf atlas (ADR-073).</summary>
public enum LeafCell
{
    /// <summary>A cluster of broad leaves: the oak's.</summary>
    Broad,

    /// <summary>A cluster of small round leaves: the birch's.</summary>
    Small,

    /// <summary>A spray of needles along a twig, its twig running up the cell: the pine's and the fir's.</summary>
    Needles,

    /// <summary>A frond's leaflets either side of its rib, running up the cell: the palm's.</summary>
    Frond,

    /// <summary>Hanging switches of narrow leaves, running down the cell: the willow's.</summary>
    Willow,

    /// <summary>Dense small dark leaves: a bush's.</summary>
    Shrub,
}

/// <summary>
/// The leaves and the bark, drawn in code (ADR-073). The leaf atlas holds six cells, three across and
/// two down, each a cluster or spray drawn leaf by leaf, grey-green so each species' colour tints it,
/// its alpha the leaves' outline; its smaller mipmaps keep their leaves' coverage so distant crowns do
/// not thin away. The bark holds three patterns: rough furrows, a birch's dark marks and a palm's
/// rings. Pure: the same pixels every time.
/// </summary>
public static class LeafAtlas
{
    /// <summary>A cell's side, in pixels.</summary>
    public const int CellSize = 256;

    /// <summary>The atlas's width: three cells.</summary>
    public const int Width = CellSize * 3;

    /// <summary>The atlas's height: two cells.</summary>
    public const int Height = CellSize * 2;

    /// <summary>The bark's side, in pixels.</summary>
    public const int BarkSize = 256;

    // Cells keep this many pixels clear at their edge, so smaller mipmaps do not bleed into their neighbours.
    private const int Margin = 10;

    /// <summary>A cell's texture coordinates: left, top, right, bottom.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>Its bounds, 0..1 across the atlas.</returns>
    public static (float U0, float V0, float U1, float V1) Bounds(LeafCell cell)
    {
        var (column, row) = ((int)cell % 3, (int)cell / 3);
        return (column / 3f, row / 2f, (column + 1) / 3f, (row + 1) / 2f);
    }

    /// <summary>The atlas's pixels with all their mipmaps, largest first, as RGBA bytes.</summary>
    /// <returns>The bytes, and how many mipmap levels follow the first.</returns>
    public static (byte[] Rgba, int Mipmaps) Leaves()
    {
        var pixels = new float[Width * Height * 4];
        foreach (var cell in Enum.GetValues<LeafCell>())
        {
            Draw(pixels, cell);
        }

        Bleed(pixels, Width, Height);
        return Mipmapped(pixels, Width, Height, alphaCoverage: true);
    }

    /// <summary>The bark's pixels with all their mipmaps: rough furrows in red, a birch's marks in green, a palm's rings in blue.</summary>
    /// <returns>The bytes, and how many mipmap levels follow the first.</returns>
    public static (byte[] Rgba, int Mipmaps) Bark()
    {
        var pixels = new float[BarkSize * BarkSize * 4];
        for (var y = 0; y < BarkSize; y++)
        {
            for (var x = 0; x < BarkSize; x++)
            {
                var (u, v) = (x / (float)BarkSize, y / (float)BarkSize);
                // Furrows: ridges about the trunk, wandering along it.
                var wander = NatureNoise.Fbm(u * 4f, v * 2f, 4, 3, 41);
                var ridge = 1f - MathF.Abs(MathF.Sin(MathF.PI * ((u * 12f) + (wander * 2.2f))));
                var rough = 0.35f + (0.45f * MathF.Pow(ridge, 0.6f)) + (0.2f * NatureNoise.Fbm(u * 16f, v * 16f, 16, 2, 42));
                // A birch: pale, with dark dashes across it.
                var dash = NatureNoise.Fbm(u * 12f, v * 48f, 12, 2, 43);
                var birch = 0.9f - (0.65f * Smooth(0.66f, 0.72f, dash)) - (0.1f * NatureNoise.Fbm(u * 20f, v * 20f, 20, 2, 44));
                // A palm: rings of old leaf scars round its trunk.
                var ring = MathF.Abs(MathF.Sin(MathF.PI * ((v * 10f) + (0.15f * NatureNoise.Fbm(u * 6f, v * 6f, 6, 2, 45)))));
                var palm = 0.45f + (0.4f * MathF.Pow(ring, 0.5f)) + (0.15f * NatureNoise.Fbm(u * 24f, v * 24f, 24, 2, 46));
                var i = ((y * BarkSize) + x) * 4;
                (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (Math.Clamp(rough, 0f, 1f), Math.Clamp(birch, 0f, 1f), Math.Clamp(palm, 0f, 1f), 1f);
            }
        }

        return Mipmapped(pixels, BarkSize, BarkSize, alphaCoverage: false);
    }

    /// <summary>Ground noise for tracks and soil, tiling: broad patches in red, smaller in green, fine grain in blue.</summary>
    /// <returns>The bytes, and how many mipmap levels follow the first.</returns>
    public static (byte[] Rgba, int Mipmaps) Noise()
    {
        var pixels = new float[BarkSize * BarkSize * 4];
        for (var y = 0; y < BarkSize; y++)
        {
            for (var x = 0; x < BarkSize; x++)
            {
                var (u, v) = (x / (float)BarkSize, y / (float)BarkSize);
                var i = ((y * BarkSize) + x) * 4;
                pixels[i] = NatureNoise.Fbm(u * 4f, v * 4f, 4, 4, 61);
                pixels[i + 1] = NatureNoise.Fbm(u * 16f, v * 16f, 16, 3, 62);
                pixels[i + 2] = NatureNoise.Fbm(u * 64f, v * 64f, 64, 2, 63);
                pixels[i + 3] = 1f;
            }
        }

        return Mipmapped(pixels, BarkSize, BarkSize, alphaCoverage: false);
    }

    /// <summary>Draws one cell.</summary>
    private static void Draw(float[] pixels, LeafCell cell)
    {
        var (column, row) = ((int)cell % 3, (int)cell / 3);
        var canvas = new Canvas(pixels, column * CellSize, row * CellSize);
        var random = PlantRandom.For(0xA71A5, (ulong)cell);
        switch (cell)
        {
            case LeafCell.Broad:
                Cluster(canvas, ref random, leaves: 64, (30f, 46f), (15f, 22f), sharpness: 0.75f, twigs: true);
                break;
            case LeafCell.Small:
                Cluster(canvas, ref random, leaves: 95, (18f, 26f), (12f, 16f), sharpness: 0.55f, twigs: true);
                break;
            case LeafCell.Shrub:
                Cluster(canvas, ref random, leaves: 120, (16f, 24f), (10f, 14f), sharpness: 0.6f, twigs: false);
                break;
            case LeafCell.Needles:
                Spray(canvas, ref random);
                break;
            case LeafCell.Frond:
                Frond(canvas, ref random);
                break;
            case LeafCell.Willow:
                Switches(canvas, ref random);
                break;
        }
    }

    /// <summary>A rounded cluster of leaves about the cell's middle, with the twigs that bear them.</summary>
    private static void Cluster(Canvas canvas, ref PlantRandom random, int leaves, (float From, float To) length, (float From, float To) width, float sharpness, bool twigs)
    {
        const float middle = CellSize / 2f;
        var reach = middle - Margin - 20f;
        var placed = new List<(float X, float Y)>();
        for (var i = 0; i < leaves; i++)
        {
            // Leaves crowd towards the cluster's middle but fill it to its rim.
            var angle = random.Range(0f, MathF.Tau);
            var distance = reach * MathF.Sqrt(random.Range(0.02f, 1f));
            placed.Add((middle + (MathF.Cos(angle) * distance), middle + (MathF.Sin(angle) * distance)));
        }

        if (twigs)
        {
            foreach (var (x, y) in placed)
            {
                if (random.Next() < 0.35f)
                {
                    canvas.Line(middle, CellSize - Margin, x, y, 1.6f, 0.28f);
                }
            }
        }

        foreach (var (x, y) in placed)
        {
            // Each leaf points out from the middle, more or less.
            var outward = MathF.Atan2(y - middle, x - middle) + random.Range(-0.8f, 0.8f);
            canvas.Leaf(x, y, outward, random.Range(length.From, length.To), random.Range(width.From, width.To), sharpness, random.Range(0.55f, 1f));
        }
    }

    /// <summary>A twig running up the cell with side twigs, all bristling with needles.</summary>
    private static void Spray(Canvas canvas, ref PlantRandom random)
    {
        const float middle = CellSize / 2f;
        var (bottom, top) = (CellSize - Margin, Margin + 6f);
        var twigs = new List<(float X0, float Y0, float X1, float Y1)> { (middle, bottom, middle + random.Range(-6f, 6f), top) };
        for (var i = 0; i < 9; i++)
        {
            var y = bottom - ((bottom - top) * (0.12f + (0.08f * i)));
            var side = i % 2 == 0 ? 1f : -1f;
            var reach = (middle - Margin - 10f) * random.Range(0.55f, 0.95f) * (1f - (0.05f * i));
            twigs.Add((middle, y, middle + (side * reach), y - random.Range(20f, 45f)));
        }

        foreach (var (x0, y0, x1, y1) in twigs)
        {
            canvas.Line(x0, y0, x1, y1, 2.2f, 0.25f);
            var length = MathF.Sqrt(((x1 - x0) * (x1 - x0)) + ((y1 - y0) * (y1 - y0)));
            var angle = MathF.Atan2(y1 - y0, x1 - x0);
            for (var d = 4f; d < length; d += 2.6f)
            {
                var (x, y) = (x0 + ((x1 - x0) * d / length), y0 + ((y1 - y0) * d / length));
                foreach (var side in new[] { 1f, -1f })
                {
                    var needle = angle + (side * random.Range(0.6f, 1.15f));
                    var reach = random.Range(16f, 24f);
                    canvas.Line(x, y, x + (MathF.Cos(needle) * reach), y + (MathF.Sin(needle) * reach), 1.8f, random.Range(0.5f, 0.85f));
                }
            }
        }
    }

    /// <summary>A frond: its rib up the cell's middle, leaflets either side sweeping towards its tip.</summary>
    private static void Frond(Canvas canvas, ref PlantRandom random)
    {
        const float middle = CellSize / 2f;
        var (bottom, top) = (CellSize - Margin, Margin + 4f);
        canvas.Line(middle, bottom, middle, top, 2.6f, 0.4f);
        for (var y = bottom - 8f; y > top + 6f; y -= 7f)
        {
            var share = (bottom - y) / (bottom - top);
            var reach = (middle - Margin - 6f) * (0.55f + (0.45f * MathF.Sin(MathF.PI * MathF.Min(1f, share * 1.15f))));
            foreach (var side in new[] { 1f, -1f })
            {
                var angle = -MathF.PI / 2f + (side * random.Range(0.95f, 1.15f));
                canvas.Leaf(middle + (MathF.Cos(angle) * reach * 0.5f), y + (MathF.Sin(angle) * reach * 0.5f), angle, reach, random.Range(7f, 9f), 0.85f, random.Range(0.6f, 0.95f));
            }
        }
    }

    /// <summary>Switches hanging down the cell, narrow leaves along them.</summary>
    private static void Switches(Canvas canvas, ref PlantRandom random)
    {
        for (var s = 0; s < 6; s++)
        {
            var x0 = Margin + 16f + ((CellSize - (2f * Margin) - 32f) * s / 5f) + random.Range(-6f, 6f);
            var sway = random.Range(-14f, 14f);
            var (top, bottom) = (Margin + random.Range(0f, 14f), CellSize - Margin - random.Range(0f, 30f));
            canvas.Line(x0, top, x0 + sway, bottom, 1.4f, 0.3f);
            for (var y = top + 6f; y < bottom; y += 6.5f)
            {
                var x = x0 + (sway * (y - top) / (bottom - top));
                var angle = (MathF.PI / 2f) + random.Range(-0.9f, 0.9f);
                canvas.Leaf(x + (MathF.Cos(angle) * 7f), y + (MathF.Sin(angle) * 7f), angle, random.Range(16f, 24f), random.Range(5f, 7f), 0.8f, random.Range(0.55f, 0.95f));
            }
        }
    }

    /// <summary>Gives the clear pixels the leaves' average colour, so smaller mipmaps do not darken at the leaves' edges.</summary>
    private static void Bleed(float[] pixels, int width, int height)
    {
        var (r, g, b, n) = (0.0, 0.0, 0.0, 0);
        for (var i = 0; i < width * height * 4; i += 4)
        {
            if (pixels[i + 3] > 0.5f)
            {
                (r, g, b, n) = (r + pixels[i], g + pixels[i + 1], b + pixels[i + 2], n + 1);
            }
        }

        var average = n == 0 ? (0.5f, 0.5f, 0.5f) : ((float)(r / n), (float)(g / n), (float)(b / n));
        for (var i = 0; i < width * height * 4; i += 4)
        {
            var a = pixels[i + 3];
            pixels[i] = (pixels[i] * a) + (average.Item1 * (1f - a));
            pixels[i + 1] = (pixels[i + 1] * a) + (average.Item2 * (1f - a));
            pixels[i + 2] = (pixels[i + 2] * a) + (average.Item3 * (1f - a));
        }
    }

    /// <summary>
    /// The pixels and every smaller mipmap, halving each time down to one pixel, as bytes. With alpha
    /// coverage, each mipmap's alpha is raised until as much of it stands above one half as of the
    /// largest: averaged leaves otherwise fade below the cut and distant crowns go thin.
    /// </summary>
    private static (byte[] Rgba, int Mipmaps) Mipmapped(float[] pixels, int width, int height, bool alphaCoverage)
    {
        var levels = new List<float[]> { pixels };
        var sizes = new List<(int W, int H)> { (width, height) };
        var target = Coverage(pixels);
        while (width > 1 || height > 1)
        {
            var (w, h) = (Math.Max(1, width / 2), Math.Max(1, height / 2));
            var smaller = new float[w * h * 4];
            var larger = levels[^1];
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    for (var c = 0; c < 4; c++)
                    {
                        float At(int dx, int dy) => larger[(((Math.Min(height - 1, (y * 2) + dy) * width) + Math.Min(width - 1, (x * 2) + dx)) * 4) + c];
                        smaller[(((y * w) + x) * 4) + c] = (At(0, 0) + At(1, 0) + At(0, 1) + At(1, 1)) / 4f;
                    }
                }
            }

            if (alphaCoverage)
            {
                KeepCoverage(smaller, target);
            }

            levels.Add(smaller);
            sizes.Add((w, h));
            (width, height) = (w, h);
        }

        var total = 0;
        foreach (var (w, h) in sizes)
        {
            total += w * h * 4;
        }

        var bytes = new byte[total];
        var offset = 0;
        foreach (var level in levels)
        {
            for (var i = 0; i < level.Length; i++)
            {
                bytes[offset + i] = (byte)Math.Clamp(MathF.Round(level[i] * 255f), 0f, 255f);
            }

            offset += level.Length;
        }

        return (bytes, levels.Count - 1);
    }

    /// <summary>The share of pixels whose alpha stands above one half.</summary>
    private static float Coverage(float[] pixels)
    {
        var (above, all) = (0, pixels.Length / 4);
        for (var i = 3; i < pixels.Length; i += 4)
        {
            above += pixels[i] > 0.5f ? 1 : 0;
        }

        return above / (float)Math.Max(1, all);
    }

    /// <summary>Scales a mipmap's alpha until its coverage meets the largest's.</summary>
    private static void KeepCoverage(float[] pixels, float target)
    {
        var (low, high) = (1f, 8f);
        for (var step = 0; step < 12; step++)
        {
            var scale = (low + high) / 2f;
            var above = 0;
            for (var i = 3; i < pixels.Length; i += 4)
            {
                above += pixels[i] * scale > 0.5f ? 1 : 0;
            }

            if (above / (float)(pixels.Length / 4) < target)
            {
                low = scale;
            }
            else
            {
                high = scale;
            }
        }

        for (var i = 3; i < pixels.Length; i += 4)
        {
            pixels[i] = MathF.Min(1f, pixels[i] * high);
        }
    }

    private static float Smooth(float from, float to, float value)
    {
        var t = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>One cell of the atlas to draw on: leaves and twigs, each pixel's coverage blended over what lies under it.</summary>
    private readonly struct Canvas(float[] pixels, int left, int top)
    {
        /// <summary>A leaf: pointed at both ends, its midrib darker, its shade lighter towards its tip.</summary>
        public void Leaf(float x, float y, float angle, float length, float width, float sharpness, float shade)
        {
            var (dx, dy) = (MathF.Cos(angle), MathF.Sin(angle));
            var reach = (length / 2f) + 2f;
            for (var py = (int)(y - reach); py <= (int)(y + reach); py++)
            {
                for (var px = (int)(x - reach); px <= (int)(x + reach); px++)
                {
                    var (ox, oy) = (px + 0.5f - x, py + 0.5f - y);
                    var along = (ox * dx) + (oy * dy);
                    var across = (-ox * dy) + (oy * dx);
                    var t = (along / length) + 0.5f;
                    if (t is <= 0f or >= 1f)
                    {
                        continue;
                    }

                    var half = width * 0.5f * MathF.Pow(MathF.Sin(MathF.PI * t), sharpness);
                    var cover = Math.Clamp(half - MathF.Abs(across) + 0.5f, 0f, 1f);
                    if (cover <= 0f)
                    {
                        continue;
                    }

                    var rib = MathF.Abs(across) < width * 0.07f ? 0.82f : 1f;
                    var tone = shade * rib * (0.85f + (0.25f * t)) * (0.9f + (0.12f * (1f - (MathF.Abs(across) / MathF.Max(0.5f, half)))));
                    Paint(px, py, cover, 0.62f * tone, 0.78f * tone, 0.5f * tone);
                }
            }
        }

        /// <summary>A twig or a needle: a line of a thickness, in a shade of grey-brown.</summary>
        public void Line(float x0, float y0, float x1, float y1, float thickness, float shade)
        {
            var (minX, maxX) = ((int)MathF.Min(x0, x1) - 3, (int)MathF.Max(x0, x1) + 3);
            var (minY, maxY) = ((int)MathF.Min(y0, y1) - 3, (int)MathF.Max(y0, y1) + 3);
            var (sx, sy) = (x1 - x0, y1 - y0);
            var squared = MathF.Max(1e-4f, (sx * sx) + (sy * sy));
            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var (ox, oy) = (px + 0.5f - x0, py + 0.5f - y0);
                    var t = Math.Clamp(((ox * sx) + (oy * sy)) / squared, 0f, 1f);
                    var (cx, cy) = (ox - (t * sx), oy - (t * sy));
                    var cover = Math.Clamp((thickness * 0.5f * (1f - (0.5f * t))) - MathF.Sqrt((cx * cx) + (cy * cy)) + 0.5f, 0f, 1f);
                    if (cover > 0f)
                    {
                        Paint(px, py, cover, shade * 0.85f, shade, shade * 0.6f);
                    }
                }
            }
        }

        /// <summary>Blends a colour over a pixel of the cell, by its coverage.</summary>
        private void Paint(int x, int y, float cover, float r, float g, float b)
        {
            if (x < Margin || y < Margin || x >= CellSize - Margin || y >= CellSize - Margin)
            {
                return;
            }

            var i = ((((top + y) * Width) + left + x) * 4);
            pixels[i] += (r - pixels[i]) * cover;
            pixels[i + 1] += (g - pixels[i + 1]) * cover;
            pixels[i + 2] += (b - pixels[i + 2]) * cover;
            pixels[i + 3] = MathF.Max(pixels[i + 3], cover);
        }
    }
}
