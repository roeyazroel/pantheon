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

    /// <summary>How much of night this is, 0..1: it brings out the stars and the fireflies and sends the birds to roost.</summary>
    public required float Night { get; init; }

    /// <summary>The share of windows with a lamp in them, 0..1, so they go out as a village falls asleep (ADR-080).</summary>
    public required float Lamps { get; init; }

    /// <summary>How many hearths are burning, 0..1: smoke from the houses at breakfast and supper.</summary>
    public required float Hearths { get; init; }

    /// <summary>How much mist lies on low land, as a multiple of the haze there.</summary>
    public required float Mist { get; init; }

    /// <summary>How much birdsong there is, 0..1.</summary>
    public required float Birds { get; init; }
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

    /// <summary>The hour the first people leave off and walk home (ADR-080).</summary>
    public required float HomeFrom { get; init; }

    /// <summary>The hour the last people leave off.</summary>
    public required float HomeBy { get; init; }

    /// <summary>The hour by which everybody is in.</summary>
    public required float IndoorsBy { get; init; }

    /// <summary>The hour the first people rise.</summary>
    public required float UpFrom { get; init; }

    /// <summary>The hour the last people rise.</summary>
    public required float UpBy { get; init; }

    /// <summary>How fast people walk home, in metres a second.</summary>
    public required float HomeSpeed { get; init; }

    /// <summary>The most times faster they may hurry when the game runs fast.</summary>
    public required float MostHurry { get; init; }

    /// <summary>The distance from a house's centre at which someone reaches its door and goes in, in metres.</summary>
    public required float DoorReach { get; init; }

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

        if (!(HighestSun is > 0f and <= 90f) || !(HighestMoon is > 0f and <= 90f) || !(LowestLight is > 0f and <= 45f))
        {
            throw new ArgumentException("HighestSun and HighestMoon must lie in 0..90, and LowestLight in 0..45.");
        }

        if (!(UpFrom >= 0f && UpFrom <= UpBy && UpBy < HomeFrom && HomeFrom <= HomeBy && HomeBy < IndoorsBy && IndoorsBy < 24f))
        {
            throw new ArgumentException("UpFrom, UpBy, HomeFrom, HomeBy and IndoorsBy must follow one another through the day.");
        }

        if (!(HomeSpeed > 0f) || !(MostHurry >= 1f) || !(DoorReach > 0f))
        {
            throw new ArgumentException("HomeSpeed and DoorReach must be positive, and MostHurry at least 1.");
        }

        if (Keys.Count < 2 || Keys[0].Hour != 0f)
        {
            throw new ArgumentException("Keys needs at least two entries, the first at hour 0.");
        }

        var last = -1f;
        foreach (var key in Keys)
        {
            if (!(key.Hour > last && key.Hour < 24f) || !(key.Energy >= 0f) || !(key.Ambient >= 0f) || !(key.Mist >= 0f))
            {
                throw new ArgumentException("Keys must be in order of hour within 0..24, with Energy, Ambient and Mist not negative.");
            }

            foreach (var share in new[] { key.Night, key.Lamps, key.Hearths, key.Birds })
            {
                if (!(share is >= 0f and <= 1f))
                {
                    throw new ArgumentException("A key's Night, Lamps, Hearths and Birds must lie in 0..1.");
                }
            }

            last = key.Hour;
        }
    }
}
