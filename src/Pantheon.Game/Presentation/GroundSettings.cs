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

/// <summary>A second ground showing through a texture in patches (ADR-077): grass through the leaf litter.</summary>
/// <param name="With">The ground that shows through.</param>
/// <param name="Share">About how much of the land it takes, 0..1.</param>
/// <param name="Metres">The patches' size.</param>
public sealed record GroundPatch(GroundKind With, float Share, float Metres);

/// <summary>The land's broad light and colour (<see cref="GroundShade"/>, ADR-077). Presentation only.</summary>
public sealed record GroundShadeSettings
{
    /// <summary>Brightness of level ground, below 1 so that ridges have room to lighten.</summary>
    public required float Neutral { get; init; }

    /// <summary>Size of the soft patches of light and dark, in metres.</summary>
    public required float MottleMetres { get; init; }

    /// <summary>How strongly those patches show, 0..1.</summary>
    public required float Mottle { get; init; }

    /// <summary>Size of the fine grain of light and dark that a texture's own detail loses with distance, in metres.</summary>
    public required float GrainMetres { get; init; }

    /// <summary>How strongly the grain shows, 0..1.</summary>
    public required float Grain { get; init; }

    /// <summary>Size of the patches where the grass dries, in metres.</summary>
    public required float DryMetres { get; init; }

    /// <summary>How strongly dry land takes <see cref="DryTint"/>, 0..1.</summary>
    public required float Dry { get; init; }

    /// <summary>The tint of dry land.</summary>
    public required RgbColor DryTint { get; init; }

    /// <summary>Radius in metres of the land around a sample that says whether it lies in a hollow; four times that for the broad folds.</summary>
    public required float HollowMetres { get; init; }

    /// <summary>How much a full hollow darkens, 0..1.</summary>
    public required float HollowDark { get; init; }

    /// <summary>How much a ridge lightens, 0..1.</summary>
    public required float RidgeLight { get; init; }

    /// <summary>How far from rivers and lakes the land grows lush, in metres.</summary>
    public required float LushMetres { get; init; }

    /// <summary>How strongly lush land takes <see cref="LushTint"/>, 0..1.</summary>
    public required float Lush { get; init; }

    /// <summary>The tint of lush land.</summary>
    public required RgbColor LushTint { get; init; }

    /// <summary>How much wet ground darkens at the sea's edge, 0..1.</summary>
    public required float WetShore { get; init; }

    /// <summary>The height above the sea up to which the ground is wet, in metres.</summary>
    public required float WetShoreHeight { get; init; }

    /// <summary>How strongly rock falls in light and dark bands, 0..1.</summary>
    public required float Strata { get; init; }

    /// <summary>The height of a band of rock, in metres.</summary>
    public required float StrataMetres { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(Neutral is > 0f and <= 1f))
        {
            throw new ArgumentException("Ground.Shade.Neutral must lie above 0, up to 1.");
        }

        foreach (var share in new[] { Mottle, Grain, Dry, HollowDark, RidgeLight, Lush, WetShore, Strata })
        {
            if (!(share is >= 0f and <= 1f))
            {
                throw new ArgumentException("Ground.Shade's strengths must lie in 0..1.");
            }
        }

        foreach (var metres in new[] { MottleMetres, GrainMetres, DryMetres, HollowMetres, LushMetres, WetShoreHeight, StrataMetres })
        {
            if (!(metres > 0f))
            {
                throw new ArgumentException("Ground.Shade's distances must be positive.");
            }
        }
    }
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

    /// <summary>How far either side of a border neighbouring grounds blend, in metres (ADR-077).</summary>
    public required float BorderMetres { get; init; }

    /// <summary>How far a border wanders from where the biomes meet, in metres.</summary>
    public required float WarpMetres { get; init; }

    /// <summary>The length of a border's wanderings, in metres.</summary>
    public required float WarpLength { get; init; }

    /// <summary>The grounds that show through a texture in patches.</summary>
    public required IReadOnlyDictionary<GroundKind, GroundPatch> Patches { get; init; }

    /// <summary>The land's broad light and colour.</summary>
    public required GroundShadeSettings Shade { get; init; }

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

        if (!(BorderMetres >= 0f) || !(WarpMetres >= 0f) || !(WarpLength > 0f))
        {
            throw new ArgumentException("Ground.BorderMetres and WarpMetres must not be negative, and WarpLength must be positive.");
        }

        foreach (var (_, patch) in Patches)
        {
            if (!(patch.Share is >= 0f and <= 1f) || !(patch.Metres > 0f))
            {
                throw new ArgumentException("A Ground.Patches entry needs a Share in 0..1 and positive Metres.");
            }
        }

        Shade.Validate();
    }
}
