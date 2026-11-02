using System;
using System.Collections.Generic;
using Pantheon.Persistence;

namespace Pantheon.Game.Presentation;

/// <summary>What the light is at an hour; hours between two keys blend them.</summary>
public sealed record DaylightKey
{
    /// <summary>The hour, from 0 up to 24.</summary>
    public required float Hour { get; init; }

    /// <summary>The colour of the light from the sun or moon.</summary>
    public required RgbColor Light { get; init; }

    /// <summary>The strength of that light; 0 at sunrise and sunset, when the one light changes from moon to sun.</summary>
    public required float Energy { get; init; }

    /// <summary>The strength of the light from the sky.</summary>
    public required float Ambient { get; init; }

    /// <summary>The colour of the sky overhead.</summary>
    public required RgbColor SkyTop { get; init; }

    /// <summary>The colour of the sky at the horizon.</summary>
    public required RgbColor SkyHorizon { get; init; }

    /// <summary>The colour of the haze.</summary>
    public required RgbColor Haze { get; init; }

    /// <summary>How much of night this is, 0..1: it lights windows, brings out the stars and sends people indoors.</summary>
    public required float Night { get; init; }
}

/// <summary>How the day passes (ADR-079), from <c>day_night.json</c>. Presentation only: the simulation never reads it.</summary>
public sealed record DayNightSettings
{
    /// <summary>File name of the day and night content file.</summary>
    public const string FileName = "day_night.json";

    /// <summary>How long a day lasts, in seconds of the world's time.</summary>
    public required float DaySeconds { get; init; }

    /// <summary>The hour a new world begins at.</summary>
    public required float StartHour { get; init; }

    /// <summary>The hour the sun rises.</summary>
    public required float Sunrise { get; init; }

    /// <summary>The hour the sun sets.</summary>
    public required float Sunset { get; init; }

    /// <summary>The hour it always is when the player turns the day and night off.</summary>
    public required float HeldHour { get; init; }

    /// <summary>How high the sun climbs at noon, in degrees above the horizon.</summary>
    public required float HighestSun { get; init; }

    /// <summary>How high the moon climbs at midnight, in degrees above the horizon.</summary>
    public required float HighestMoon { get; init; }

    /// <summary>The lowest the light is drawn from, in degrees: lower, its shadows would run for ever.</summary>
    public required float LowestLight { get; init; }

    /// <summary>The share of a village's people indoors in the depth of night, 0..1.</summary>
    public required float AsleepShare { get; init; }

    /// <summary>The strength of the warm light by a shrine in the depth of night.</summary>
    public required float ShrineLight { get; init; }

    /// <summary>The reach of that light, in metres.</summary>
    public required float ShrineLightReach { get; init; }

    /// <summary>The colour of that light and of lit windows.</summary>
    public required RgbColor Lamplight { get; init; }

    /// <summary>The light at each hour named, in order, the first at hour 0.</summary>
    public required IReadOnlyList<DaylightKey> Keys { get; init; }

    /// <summary>Parses and validates the content file.</summary>
    /// <param name="json">Text of <c>day_night.json</c>.</param>
    /// <returns>The validated settings.</returns>
    /// <exception cref="ContentFormatException">The file is malformed or holds out-of-range values.</exception>
    public static DayNightSettings Parse(string json)
    {
        var settings = ContentJson.Parse<DayNightSettings>(json, FileName);
        try
        {
            settings.Validate();
        }
        catch (ArgumentException exception)
        {
            throw new ContentFormatException($"{FileName}: {exception.Message}", exception);
        }

        return settings;
    }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(DaySeconds > 0f) || !(ShrineLight >= 0f) || !(ShrineLightReach > 0f))
        {
            throw new ArgumentException("DaySeconds and ShrineLightReach must be positive, and ShrineLight must not be negative.");
        }

        if (!(Sunrise is > 0f and < 24f) || !(Sunset > Sunrise && Sunset < 24f))
        {
            throw new ArgumentException("Sunrise must lie before Sunset, both inside the day.");
        }

        if (!(StartHour is >= 0f and < 24f) || !(HeldHour is >= 0f and < 24f))
        {
            throw new ArgumentException("StartHour and HeldHour must lie in 0..24.");
        }

        if (!(HighestSun is > 0f and <= 90f) || !(HighestMoon is > 0f and <= 90f) || !(LowestLight is > 0f and <= 45f) || !(AsleepShare is >= 0f and <= 1f))
        {
            throw new ArgumentException("HighestSun and HighestMoon must lie in 0..90, LowestLight in 0..45 and AsleepShare in 0..1.");
        }

        if (Keys.Count < 2 || Keys[0].Hour != 0f)
        {
            throw new ArgumentException("Keys needs at least two entries, the first at hour 0.");
        }

        var last = -1f;
        foreach (var key in Keys)
        {
            if (!(key.Hour > last && key.Hour < 24f) || !(key.Energy >= 0f) || !(key.Ambient >= 0f) || !(key.Night is >= 0f and <= 1f))
            {
                throw new ArgumentException("Keys must be in order of hour within 0..24, with Energy and Ambient not negative and Night in 0..1.");
            }

            last = key.Hour;
        }
    }
}
