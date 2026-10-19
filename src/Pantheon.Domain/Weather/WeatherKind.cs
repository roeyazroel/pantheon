namespace Pantheon.Domain.Weather;

/// <summary>What the weather beneath a cell is like now (spec §16's states; heat and cold waves are spells, ADR-028).</summary>
public enum WeatherKind
{
    /// <summary>Clouds, but no rain to speak of.</summary>
    Clear,

    /// <summary>Rain.</summary>
    Rain,

    /// <summary>Rain with lightning.</summary>
    Thunderstorm,

    /// <summary>Storm winds that ruin stores and building work.</summary>
    Windstorm,

    /// <summary>Snow: cold precipitation that lies on the land until it melts (ADR-028).</summary>
    Snow,

    /// <summary>Fog: a humid, calm, cool cell that does not rain (ADR-028).</summary>
    Fog,

    /// <summary>A hurricane: a storm grown over warm sea, whose winds tear houses down (ADR-028).</summary>
    Hurricane,
}
