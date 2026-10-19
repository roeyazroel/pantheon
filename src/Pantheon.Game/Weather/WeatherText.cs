using System;
using System.Globalization;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;

namespace Pantheon.Game.Weather;

/// <summary>Words for the weather (ADR-013), shared by the HUD, alerts and debug readouts.</summary>
public static class WeatherText
{
    private static readonly string[] Compass = ["north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west"];

    /// <summary>A spell's name.</summary>
    /// <param name="spell">The spell.</param>
    /// <returns>For example "A dry spell".</returns>
    public static string Spell(WeatherSpell spell) => spell switch
    {
        WeatherSpell.Normal => "Normal weather",
        WeatherSpell.Dry => "A dry spell",
        WeatherSpell.Wet => "A wet spell",
        WeatherSpell.HeatWave => "A heat wave",
        WeatherSpell.ColdWave => "A cold wave",
    };

    /// <summary>What a spell means for the land.</summary>
    /// <param name="spell">The spell.</param>
    /// <returns>A short clause.</returns>
    public static string SpellEffect(WeatherSpell spell) => spell switch
    {
        WeatherSpell.Normal => "showers come and go",
        WeatherSpell.Dry => "little rain, and the land dries out",
        WeatherSpell.Wet => "rain often, and storms are likely",
        WeatherSpell.HeatWave => "the land bakes and burns, and the heat sickens",
        WeatherSpell.ColdWave => "frost and snow, poor harvests, and the cold sickens",
    };

    /// <summary>The weather beneath a cell.</summary>
    /// <param name="kind">Its kind.</param>
    /// <returns>For example "a thunderstorm".</returns>
    public static string Kind(WeatherKind kind) => kind switch
    {
        WeatherKind.Clear => "clouds",
        WeatherKind.Rain => "rain",
        WeatherKind.Thunderstorm => "a thunderstorm",
        WeatherKind.Windstorm => "a windstorm",
        WeatherKind.Snow => "snow",
        WeatherKind.Fog => "fog",
        WeatherKind.Hurricane => "a hurricane",
    };

    /// <summary>
    /// Where a wind comes from, on an eight-point compass. The ground plane's +X is east and +Z is
    /// south, as the camera first sees it.
    /// </summary>
    /// <param name="direction">Direction the wind blows towards, in radians (0 = towards +X).</param>
    /// <returns>For example "the west".</returns>
    public static string WindFrom(float direction)
    {
        // The wind comes from the opposite direction; bearings run clockwise from north (-Z).
        var fromX = -MathF.Cos(direction);
        var fromZ = -MathF.Sin(direction);
        var bearing = MathF.Atan2(fromX, -fromZ) * (180f / MathF.PI);
        var index = (int)MathF.Round((bearing < 0f ? bearing + 360f : bearing) / 45f) % Compass.Length;
        return "the " + Compass[index];
    }

    /// <summary>The spell, how long it lasts and the wind, for the HUD and the debug panel.</summary>
    /// <param name="weather">The weather.</param>
    /// <param name="tick">The current tick.</param>
    /// <returns>For example "A dry spell (3:20 left): little rain, and the land dries out. Wind from the west."</returns>
    public static string Describe(WeatherState weather, long tick)
    {
        ArgumentNullException.ThrowIfNull(weather);
        var left = weather.SpellEndsTick is { } ends ? Format(" ({0} left)", Divine.DivineText.Clock((ends - tick) / (double)SimulationTime.TicksPerSecond)) : string.Empty;
        return Format("{0}{1}: {2}. Wind from {3}.", Spell(weather.Spell), left, SpellEffect(weather.Spell), WindFrom(weather.WindDirection));
    }

    /// <summary>A settlement's drought in words.</summary>
    /// <param name="drought">Drought (0..1).</param>
    /// <param name="alert">Drought from which a settlement is in drought (<c>SettlementRules.DroughtThreshold</c>).</param>
    /// <returns>For example "a severe drought".</returns>
    public static string Drought(float drought, float alert) =>
        drought >= 2f * alert ? "a severe drought"
        : drought >= alert ? "a drought"
        : drought > 0f ? "dry soil"
        : "well-watered soil";

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
