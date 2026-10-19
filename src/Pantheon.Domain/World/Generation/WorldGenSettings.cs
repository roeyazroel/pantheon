using System.Collections.Generic;

namespace Pantheon.Domain.World.Generation;

/// <summary>
/// Complete, data-driven description of how a world is generated from a seed. It is loaded from
/// <c>game/Data/worldgen.json</c> and copied into <see cref="WorldState.Generation"/>.
/// </summary>
public sealed record WorldGenSettings
{
    /// <summary>Height samples per side of the square heightfield. Must be divisible by <see cref="RegionsPerSide"/>.</summary>
    public required int HeightmapResolution { get; init; }

    /// <summary>Distance between neighbouring height samples, in metres.</summary>
    public required float VertexSpacing { get; init; }

    /// <summary>Regions per side of the square region grid.</summary>
    public required int RegionsPerSide { get; init; }

    /// <summary>Island shape and elevation remapping.</summary>
    public required TerrainShapeSettings Shape { get; init; }

    /// <summary>Continent-scale fBm elevation noise.</summary>
    public required NoiseLayerSettings ElevationNoise { get; init; }

    /// <summary>Ridged noise that forms mountain ranges on high ground.</summary>
    public required NoiseLayerSettings RidgeNoise { get; init; }

    /// <summary>Moisture noise.</summary>
    public required NoiseLayerSettings MoistureNoise { get; init; }

    /// <summary>Temperature noise.</summary>
    public required NoiseLayerSettings TemperatureNoise { get; init; }

    /// <summary>Temperature and moisture modifiers.</summary>
    public required ClimateSettings Climate { get; init; }

    /// <summary>Ordered biome rules. The first matching rule wins, and the last rule must be a catch-all.</summary>
    public required IReadOnlyList<BiomeRule> Biomes { get; init; }

    /// <summary>Parameters of the settlement founded at world creation.</summary>
    public required StartingSettlementSettings StartingSettlement { get; init; }

    /// <summary>
    /// Rivers and lakes (ADR-022); <c>null</c> for a world without fresh water, as every world saved
    /// before version 14 was, so its terrain regenerates unchanged.
    /// </summary>
    public HydrologySettings? Hydrology { get; init; }
}
