using Godot;

namespace Pantheon.Client.Presentation.Creatures;

/// <summary>Fine relief for a Creature's coat (ADR-079), so light breaks over it as over fur rather than a smooth skin. Made once.</summary>
internal static class FurTexture
{
    private static NoiseTexture2D? _normals;

    /// <summary>The coat's normal map, seamless, laid over the body by triplanar mapping.</summary>
    public static NoiseTexture2D Normals => _normals ??= new NoiseTexture2D
    {
        Width = 256,
        Height = 256,
        Seamless = true,
        AsNormalMap = true,
        BumpStrength = 9f,
        GenerateMipmaps = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Seed = 91, Frequency = 0.09f, FractalOctaves = 3 },
    };
}
