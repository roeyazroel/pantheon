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
}
