using Godot;

namespace Pantheon.Client.Presentation.Weather;

/// <summary>The noise the world's look reads (ADR-078), made in code: broad cloud in red and finer patches in green. Made once.</summary>
internal static class LookTextures
{
    private const int Size = 512;

    private static ImageTexture? _clouds;

    /// <summary>The noise.</summary>
    public static ImageTexture Clouds => _clouds ??= Make();

    private static ImageTexture Make()
    {
        var cloud = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = 81, Frequency = 0.006f, FractalOctaves = 5 }.GetSeamlessImage(Size, Size);
        var patches = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = 82, Frequency = 0.03f, FractalOctaves = 4 }.GetSeamlessImage(Size, Size);
        cloud.Convert(Image.Format.L8);
        patches.Convert(Image.Format.L8);
        var (red, green) = (cloud.GetData(), patches.GetData());
        var rgba = new byte[Size * Size * 4];
        for (var i = 0; i < Size * Size; i++)
        {
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], rgba[(i * 4) + 3]) = (red[i], green[i], 0, 255);
        }

        var image = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
