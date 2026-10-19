using System.Collections.Generic;
using Pantheon.Domain.Ecology;

namespace Pantheon.Domain.World.Generation;

/// <summary>
/// How generation lays out the forests and stone (spec §93, ADR-033): each biome's forest (as a
/// factor on the land's vegetation) and stone, varied in patches of noise.
/// </summary>
public sealed record ResourceSettings
{
    /// <summary>Each biome's forest and stone; a biome not listed has neither.</summary>
    public required IReadOnlyList<BiomeResources> Biomes { get; init; }

    /// <summary>The size of the patches in metres.</summary>
    public required float PatchSize { get; init; }

    /// <summary>How far the patches vary forest and stone (0 for not at all, 1 from none to double).</summary>
    public required float PatchStrength { get; init; }
}

/// <summary>A biome's forest and stone (ADR-033).</summary>
public sealed record BiomeResources
{
    /// <summary>The biome.</summary>
    public required Biome Biome { get; init; }

    /// <summary>Forest as a factor on the land's vegetation.</summary>
    public required float Forest { get; init; }

    /// <summary>Stone (0..1).</summary>
    public required float Stone { get; init; }
}
