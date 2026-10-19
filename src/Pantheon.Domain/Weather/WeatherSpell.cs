namespace Pantheon.Domain.Weather;

/// <summary>The run of weather the island is in: how often weather arrives and how stormy it is.</summary>
public enum WeatherSpell
{
    /// <summary>Ordinary weather: showers come and go.</summary>
    Normal,

    /// <summary>Little weather arrives; the land dries out and droughts follow.</summary>
    Dry,

    /// <summary>Weather arrives often and storms are more likely.</summary>
    Wet,

    /// <summary>A heat wave: the land bakes, dries and burns, and the heat sickens (ADR-028).</summary>
    HeatWave,

    /// <summary>A cold wave: frost and snow, poor harvests, and the cold sickens (ADR-028).</summary>
    ColdWave,
}
