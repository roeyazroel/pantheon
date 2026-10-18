using Pantheon.Domain.Ecology;

namespace Pantheon.Domain.World;

/// <summary>
/// Simulation state of one cell of the region grid. Regions are the unit of the statistical and
/// regional simulation tiers (ecology, influence and, later, belief).
/// </summary>
public sealed class RegionState
{
    /// <summary>Row-major index in the grid: <c>row * Columns + column</c>.</summary>
    public required int Id { get; init; }

    /// <summary>Dominant biome of the region (the majority of its terrain samples).</summary>
    public required Biome Biome { get; set; }

    /// <summary>Mean terrain height in metres. Sea level is 0; negative values are underwater.</summary>
    public required float Elevation { get; set; }

    /// <summary>Mean normalised temperature (0 = freezing, 1 = hot).</summary>
    public required float Temperature { get; set; }

    /// <summary>Mean normalised moisture (0 = arid, 1 = saturated).</summary>
    public required float Moisture { get; set; }

    /// <summary>Normalised capacity of the land to grow food (0..1).</summary>
    public required float Fertility { get; set; }

    /// <summary>Normalised vegetation cover (0..1).</summary>
    public required float Vegetation { get; set; }

    /// <summary>Divine blessing of the land (0..1) from miracles; blessed farmland yields more, and it fades.</summary>
    public required float Blessing { get; set; }
}
