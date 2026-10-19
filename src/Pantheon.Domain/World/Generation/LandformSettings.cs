using System.Collections.Generic;
using Pantheon.Domain.Ecology;

namespace Pantheon.Domain.World.Generation;

/// <summary>The base traits of a landform biome, which climate does not decide (ADR-038).</summary>
public sealed record LandformTraits
{
    /// <summary>The biome: coast, river valley, island or volcanic.</summary>
    public required Biome Biome { get; init; }

    /// <summary>Fertility of ideal land of this biome (0..1).</summary>
    public required float BaseFertility { get; init; }

    /// <summary>Vegetation cover of ideal land of this biome (0..1).</summary>
    public required float BaseVegetation { get; init; }
}

/// <summary>
/// The biomes that place, not climate, decides (spec §5, ADR-038): small islands, coasts, river
/// valleys and the volcanic land around the island's highest peak. They are marked on the terrain
/// after the climate biomes and the rivers, in that order of precedence.
/// </summary>
public sealed record LandformSettings
{
    /// <summary>Land, apart from the largest mass, whose area is below this many square metres is an island.</summary>
    public required float IslandMaxArea { get; init; }

    /// <summary>Land this close to the sea, in metres, is coast.</summary>
    public required float CoastMetres { get; init; }

    /// <summary>Coast lies no higher than this, in metres.</summary>
    public required float CoastMaxHeight { get; init; }

    /// <summary>The climate biomes coast replaces.</summary>
    public required IReadOnlyList<Biome> CoastOver { get; init; }

    /// <summary>Land this close to a river or lake, in metres, is a river valley.</summary>
    public required float ValleyMetres { get; init; }

    /// <summary>A river valley lies no higher than this, in metres.</summary>
    public required float ValleyMaxHeight { get; init; }

    /// <summary>The biomes a river valley replaces.</summary>
    public required IReadOnlyList<Biome> ValleyOver { get; init; }

    /// <summary>The island's highest land region is a volcano when it rises this high, in metres; keep it equal to the volcano's own threshold.</summary>
    public required float VolcanoMinElevation { get; init; }

    /// <summary>Land this close to the volcano's region, in metres from its centre, is volcanic.</summary>
    public required float VolcanicMetres { get; init; }

    /// <summary>The base traits of coast, river valley, island and volcanic land.</summary>
    public required IReadOnlyList<LandformTraits> Traits { get; init; }
}
