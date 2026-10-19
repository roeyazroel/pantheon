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

    /// <summary>
    /// Water in the soil now (0 = parched, 1 = soaked), unlike <see cref="Moisture"/>, which is the
    /// region's climate. Rain raises it, warmth dries it, springs refill it, and dry farmland yields
    /// less (ADR-013).
    /// </summary>
    public required float SoilMoisture { get; set; }

    /// <summary>How fiercely the region is burning now (0 = not burning, 1 = an inferno; ADR-014).</summary>
    public required float Fire { get; set; }

    /// <summary>How badly fire has scorched the land (0..1): it has no fuel left to burn and yields less until it regrows.</summary>
    public required float Scorch { get; set; }

    /// <summary>
    /// How much of the region a river or lake waters (0 = none, 1 = fully), fixed when the world is
    /// made (ADR-022). People nearby draw water from it and it keeps the soil moist.
    /// </summary>
    public required float FreshWater { get; init; }

    /// <summary>How deep the region lies under a flood now (0 = dry, 1 = drowned; ADR-022).</summary>
    public required float Flood { get; set; }

    /// <summary>Snow lying on the land (0..1, ADR-028): nothing grows or burns under it, and it melts into the soil.</summary>
    public float Snow { get; set; }

    /// <summary>The wild animals the land holds (0..1, spec §18, ADR-028): they follow its cover and health, and villages hunt them.</summary>
    public float Wildlife { get; set; }

    /// <summary>
    /// Harm to the land's life (0..1, spec §18, ADR-028) from destructive miracles and disasters. It
    /// heals over time, but never below <see cref="Scar"/>, and lowers the land's yield and wildlife.
    /// </summary>
    public float Damage { get; set; }

    /// <summary>The permanent part of <see cref="Damage"/> (0..1): repeated harm scars the land, and only a god of nature heals it.</summary>
    public float Scar { get; set; }

    /// <summary>Pollution (0..1, spec §18, ADR-028) from crowded, working villages; it lowers yield and wildlife and clears slowly.</summary>
    public float Pollution { get; set; }

    /// <summary>Crop blight (0..1, spec §19, ADR-029): the share of the field's crop it takes; it spreads in warm, wet weather.</summary>
    public float Blight { get; set; }

    /// <summary>Landslide rubble (0..1, spec §20, ADR-029): enough of it blocks the way until it is cleared.</summary>
    public float Rubble { get; set; }

    /// <summary>Blessing that never fades (0..1, spec §14, ADR-030): a god hallowed the land, and its <see cref="Blessing"/> stays at least this.</summary>
    public float Hallowed { get; set; }

    /// <summary>The forest the land grows (0..1, spec §93, ADR-033): the most <see cref="Timber"/> it holds, to which cut or burnt wood grows back.</summary>
    public float Forest { get; set; }

    /// <summary>The standing wood (0..<see cref="Forest"/>, spec §5, ADR-033): villages cut it, fire burns it, and it grows back.</summary>
    public float Timber { get; set; }

    /// <summary>The stone that can be quarried (0..1, spec §5, ADR-033); quarrying uses it up for good.</summary>
    public float Stone { get; set; }
}
