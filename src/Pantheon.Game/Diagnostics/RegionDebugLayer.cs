namespace Pantheon.Game.Diagnostics;

/// <summary>Region statistic shown by the F3 debug map.</summary>
public enum RegionDebugLayer
{
    /// <summary>Dominant biome, in the palette's colours.</summary>
    Biome,

    /// <summary>Mean height in metres.</summary>
    Elevation,

    /// <summary>Mean normalised temperature.</summary>
    Temperature,

    /// <summary>Mean normalised moisture.</summary>
    Moisture,

    /// <summary>Capacity of the land to grow food.</summary>
    Fertility,

    /// <summary>Vegetation cover.</summary>
    Vegetation,

    /// <summary>Divine blessing of the land.</summary>
    Blessing,

    /// <summary>The local player's god's influence at the region centre.</summary>
    Influence,

    /// <summary>Water in the soil now (ADR-013).</summary>
    SoilMoisture,

    /// <summary>How fiercely the region burns now (ADR-014).</summary>
    Fire,

    /// <summary>How badly fire has scorched the land (ADR-014).</summary>
    Scorch,

    /// <summary>How much of the region a river or lake waters (ADR-022).</summary>
    FreshWater,

    /// <summary>How deep the region lies under a flood now (ADR-022).</summary>
    Flood,
}
