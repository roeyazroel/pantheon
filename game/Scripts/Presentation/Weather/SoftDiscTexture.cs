using System;
using Godot;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>A white disc that is solid in the middle and fades smoothly to nothing at its edge, drawn once in code for cloud puffs and their shade.</summary>
internal static class SoftDiscTexture
{
    private const int Size = 128;

    private static ImageTexture? _texture;

    /// <summary>The shared soft disc.</summary>
    public static ImageTexture Texture => _texture ??= Create();

    /// <summary>Draws the disc: alpha falls from 1 at the centre to 0 at the edge along a smooth curve.</summary>
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
                var t = Math.Clamp(1f - MathF.Sqrt((dx * dx) + (dy * dy)), 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, t * t * (3f - (2f * t))));
            }
        }

        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
