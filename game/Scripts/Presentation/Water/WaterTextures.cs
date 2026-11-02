using System;
using Godot;

namespace Pantheon.Client.Presentation.Water;

/// <summary>
/// The ripples both water shaders read (ADR-077), made in code from seamless noise: the surface's
/// slope across and along in red and green, and a lacy pattern for foam in blue. Made once.
/// </summary>
internal static class WaterTextures
{
    private const int Size = 512;

    // How strongly the noise's height turns into slope.
    private const float SlopeGain = 14f;

    private static ImageTexture? _ripples;

    /// <summary>The ripples.</summary>
    public static ImageTexture Ripples => _ripples ??= Make();

    /// <summary>Makes the ripples from a fractal height and a cellular lace.</summary>
    private static ImageTexture Make()
    {
        var height = Sample(new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = 71, Frequency = 0.012f, FractalOctaves = 5 });
        var lace = Sample(new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Seed = 72, Frequency = 0.02f, CellularReturnType = FastNoiseLite.CellularReturnTypeEnum.Distance2Sub, CellularJitter = 1f, FractalOctaves = 3 });
        var rgba = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var east = height[(y * Size) + ((x + 1) % Size)] - height[(y * Size) + ((x + Size - 1) % Size)];
                var south = height[(((y + 1) % Size) * Size) + x] - height[(((y + Size - 1) % Size) * Size) + x];
                var at = ((y * Size) + x) * 4;
                rgba[at] = Pantheon.Game.Presentation.RgbColor.ToByte(0.5f + (east * SlopeGain));
                rgba[at + 1] = Pantheon.Game.Presentation.RgbColor.ToByte(0.5f + (south * SlopeGain));
                rgba[at + 2] = Pantheon.Game.Presentation.RgbColor.ToByte(lace[(y * Size) + x]);
                rgba[at + 3] = 255;
            }
        }

        var image = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
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
