namespace Pantheon.Domain.Weather;

/// <summary>What the weather beneath a cell is like now (spec §16's states, as far as Milestone 7 goes).</summary>
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
}
