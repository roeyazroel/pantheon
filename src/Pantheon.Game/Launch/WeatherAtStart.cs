namespace Pantheon.Game.Launch;

/// <summary>Weather the <c>--weather</c> launch option sets up at the start (ADR-013).</summary>
public enum WeatherAtStart
{
    /// <summary>A normal spell.</summary>
    Normal,

    /// <summary>A dry spell.</summary>
    Dry,

    /// <summary>A wet spell.</summary>
    Wet,

    /// <summary>A storm over the focused settlement.</summary>
    Storm,

    /// <summary>A heat wave (ADR-028).</summary>
    HeatWave,

    /// <summary>A cold wave (ADR-028).</summary>
    ColdWave,

    /// <summary>A hurricane over the focused settlement (ADR-028).</summary>
    Hurricane,
}
