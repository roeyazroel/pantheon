namespace Pantheon.Domain.Ecology;

/// <summary>Surface classification of a terrain sample or a region.</summary>
public enum Biome
{
    /// <summary>Open water below sea level. Not habitable.</summary>
    Sea = 0,

    /// <summary>Temperate open land with moderate moisture. The most fertile biome.</summary>
    Grassland = 1,

    /// <summary>Wet land with dense vegetation.</summary>
    Forest = 2,

    /// <summary>Cold, rocky uplands with poor soil.</summary>
    Highland = 3,

    /// <summary>Hot, dry land: sand and scrub, little to farm (ADR-038).</summary>
    Desert = 4,

    /// <summary>Cold land below the heights: moss and low shrubs (ADR-038).</summary>
    Tundra = 5,

    /// <summary>Wet, low-lying land: reeds, standing water and dense growth (ADR-038).</summary>
    Swamp = 6,

    /// <summary>Low land by the sea: dunes and salt meadows (ADR-038).</summary>
    Coast = 7,

    /// <summary>Low land along a river or lake: the richest soil (ADR-038).</summary>
    RiverValley = 8,

    /// <summary>A small island off the main one (ADR-038).</summary>
    Island = 9,

    /// <summary>The ash-covered land around the volcano: fertile but bare (ADR-038).</summary>
    Volcanic = 10,
}
