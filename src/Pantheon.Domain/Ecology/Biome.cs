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
}
