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
}
