using System;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>The blooms of a flower bed (ADR-042, ADR-077), scattered over its low mound. Pure.</summary>
public static class FlowerBedMesh
{
    /// <summary>The radius of a bed's mound, in metres.</summary>
    public const float Radius = 1.5f;

    /// <summary>The height of a bed's mound, in metres.</summary>
    public const float Height = 0.32f;

    private const int Blooms = 44;
    private const float BloomSize = 0.13f;
    private const ulong Salt = 42;

    /// <summary>Builds the blooms: small round heads, thick in the middle of the bed and thinner at its edge, each resting on the mound.</summary>
    /// <returns>The triangles.</returns>
    public static TaggedMesh Build()
    {
        var b = new MeshBuilder(detailed: false);
        for (var i = 0; i < Blooms; i++)
        {
            var angle = PresentationHash.Unit(Salt, (ulong)i, 1) * MathF.Tau;
            var reach = MathF.Sqrt(PresentationHash.Unit(Salt, (ulong)i, 2)) * Radius * 0.92f;
            var (x, z) = (MathF.Cos(angle) * reach, MathF.Sin(angle) * reach);
            var over = 1f - ((x * x) + (z * z)) / (Radius * Radius);
            var size = BloomSize * (0.7f + (0.6f * PresentationHash.Unit(Salt, (ulong)i, 3)));
            b.Ball(new Vector3(x, Height * MathF.Sqrt(MathF.Max(0f, over)) + (size * 0.6f), z), new Vector3(size, size * 0.8f, size), 0, 0);
        }

        return b.Finish();
    }
}
