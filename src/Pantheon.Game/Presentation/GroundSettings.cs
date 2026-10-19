using System;
using System.Collections.Generic;

namespace Pantheon.Game.Presentation;

/// <summary>A ground texture of the terrain (ADR-051); the numbers are Terrain3D's texture ids.</summary>
public enum GroundKind
{
    /// <summary>Grass.</summary>
    Grass,

    /// <summary>Forest floor: dark soil and fallen leaves.</summary>
    ForestFloor,

    /// <summary>Bare earth of the highlands.</summary>
    Earth,

    /// <summary>Rock, on steep slopes.</summary>
    Rock,

    /// <summary>Sand of beaches and deserts.</summary>
    Sand,

    /// <summary>Snow on cold heights.</summary>
    Snow,

    /// <summary>Tundra moss.</summary>
    Moss,

    /// <summary>Swamp mud.</summary>
    Mud,

    /// <summary>Volcanic ash.</summary>
    Ash,
}

/// <summary>How the ground is textured (spec §61, ADR-051), from <c>biome_palette.json</c>. Presentation only.</summary>
public sealed record GroundSettings
{
    /// <summary>Each texture's darker and lighter colour; its pattern is made in code between them.</summary>
    public required IReadOnlyDictionary<GroundKind, IReadOnlyList<RgbColor>> Textures { get; init; }

    /// <summary>How strongly the biome colours tint the textures (0: not at all, 1: fully).</summary>
    public required float TintStrength { get; init; }

    /// <summary>Texture repeats per terrain unit (Terrain3D's UV scale).</summary>
    public required float UvScale { get; init; }

    /// <summary>The darker and lighter colour of a texture.</summary>
    /// <param name="kind">The texture.</param>
    /// <returns>Its colours.</returns>
    public (RgbColor Dark, RgbColor Light) ColorsOf(GroundKind kind) => (Textures[kind][0], Textures[kind][1]);

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        foreach (var kind in Enum.GetValues<GroundKind>())
        {
            if (!Textures.TryGetValue(kind, out var colors) || colors.Count != 2)
            {
                throw new ArgumentException($"Ground.Textures needs a darker and a lighter colour for {kind}.");
            }
        }

        if (!(TintStrength is >= 0f and <= 1f) || !(UvScale > 0f))
        {
            throw new ArgumentException("Ground.TintStrength must lie in 0..1 and UvScale be positive.");
        }
    }
}
