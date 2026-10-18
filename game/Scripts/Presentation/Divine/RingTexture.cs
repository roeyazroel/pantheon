using System;
using Godot;

namespace Pantheon.Client.Presentation.Divine;

/// <summary>A white ring with a faint fill, drawn once in code, for decals that mark a circle on the ground.</summary>
internal static class RingTexture
{
    private const int Size = 256;
    private const float RingStart = 0.9f;
    private const float FillAlpha = 0.14f;

    private static ImageTexture? _texture;

    /// <summary>The shared ring texture.</summary>
    public static ImageTexture Texture => _texture ??= Create();

    /// <summary>Draws the ring: opaque between 90% and 100% of the radius, faint inside, clear outside.</summary>
    private static ImageTexture Create()
    {
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);
        var half = Size / 2f;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var dx = (x + 0.5f - half) / half;
                var dy = (y + 0.5f - half) / half;
                var r = MathF.Sqrt((dx * dx) + (dy * dy));
                var alpha = r > 1f ? 0f
                    : r >= RingStart ? 1f - (MathF.Abs(r - ((1f + RingStart) / 2f)) / ((1f - RingStart) / 2f) * 0.6f)
                    : FillAlpha;
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
