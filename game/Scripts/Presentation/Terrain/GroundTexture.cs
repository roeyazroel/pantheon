using Godot;
using Pantheon.Game.Presentation;

namespace Pantheon.Client.Presentation.Terrain;

/// <summary>
/// Procedural, tileable greyscale detail texture for Terrain3D. The biome colour map tints it, so
/// it only adds fine brightness variation up close.
/// </summary>
internal static class GroundTexture
{
    private const int Size = 256;
    private const int NoiseSeed = 7;
    private const float NoiseFrequency = 0.035f;
    private const int NoiseOctaves = 4;
    private const float MinBrightness = 0.8f;
    private const float BrightnessRange = 0.2f;

    // Terrain3D reads albedo alpha as height for blending between textures; with one texture any constant works.
    private const byte HeightAlpha = 128;

    /// <summary>Builds the texture with mipmaps, as Terrain3D requires.</summary>
    /// <returns>The texture.</returns>
    public static ImageTexture Create()
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Seed = NoiseSeed,
            Frequency = NoiseFrequency,
            FractalOctaves = NoiseOctaves,
        };
        var grey = noise.GetSeamlessImage(Size, Size);
        grey.Convert(Image.Format.L8);
        var source = grey.GetData();

        var rgba = new byte[Size * Size * 4];
        for (var i = 0; i < Size * Size; i++)
        {
            var value = RgbColor.ToByte(MinBrightness + (BrightnessRange * (source[i] / 255f)));
            rgba[i * 4] = value;
            rgba[(i * 4) + 1] = value;
            rgba[(i * 4) + 2] = value;
            rgba[(i * 4) + 3] = HeightAlpha;
        }

        var image = Image.CreateFromData(Size, Size, false, Image.Format.Rgba8, rgba);
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
