using System;
using System.Numerics;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>A part of the day, for the words shown.</summary>
public enum DayPart
{
    /// <summary>The sky pales before the sun is well up.</summary>
    Dawn,

    /// <summary>Morning.</summary>
    Morning,

    /// <summary>Around noon.</summary>
    Noon,

    /// <summary>Afternoon.</summary>
    Afternoon,

    /// <summary>The sun goes down.</summary>
    Dusk,

    /// <summary>Night.</summary>
    Night,
}

/// <summary>The light at an hour of the day (ADR-079).</summary>
/// <param name="Hour">The hour, 0 up to 24.</param>
/// <param name="ToLight">The direction from the land to the sun or moon, a unit vector.</param>
/// <param name="Light">The colour of its light.</param>
/// <param name="Energy">The strength of its light.</param>
/// <param name="Ambient">The strength of the light from the sky.</param>
/// <param name="SkyTop">The colour of the sky overhead.</param>
/// <param name="SkyHorizon">The colour of the sky at the horizon.</param>
/// <param name="Haze">The colour of the haze.</param>
/// <param name="Night">How much of night this is, 0..1.</param>
/// <param name="Lamps">The share of windows with a lamp in them, 0..1.</param>
/// <param name="Hearths">How many hearths are burning, 0..1.</param>
/// <param name="Mist">How much mist lies on low land, as a multiple of the haze there.</param>
/// <param name="Birds">How much birdsong there is, 0..1.</param>
public readonly record struct Daylight(float Hour, Vector3 ToLight, RgbColor Light, float Energy, float Ambient, RgbColor SkyTop, RgbColor SkyHorizon, RgbColor Haze, float Night, float Lamps, float Hearths, float Mist, float Birds);

/// <summary>
/// The day's light as a function of the world's clock (ADR-079), so every player of a shared world
/// sees the same hour, a save brings it back, and nothing about it needs saving. One light plays the
/// sun by day and the moon by night, changing at the horizon, where it is dark. Presentation only: the
/// day changes how the world looks and never what it does. Pure.
/// </summary>
public static class DayNight
{
    /// <summary>The hours in a day.</summary>
    public const float Hours = 24f;

    /// <summary>The hour at a tick.</summary>
    /// <param name="settings">Day and night settings.</param>
    /// <param name="tick">The world's tick.</param>
    /// <param name="shift">Hours added, for the debug key.</param>
    /// <returns>The hour, 0 up to 24.</returns>
    public static float HourAt(DayNightSettings settings, long tick, float shift = 0f)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var ticksInDay = Math.Max(1L, (long)MathF.Round(settings.DaySeconds * SimulationTime.TicksPerSecond));
        var through = (tick % ticksInDay) / (double)ticksInDay;
        return Wrap(settings.StartHour + shift + (float)(through * Hours));
    }

    /// <summary>The light at an hour.</summary>
    /// <param name="settings">Day and night settings.</param>
    /// <param name="hour">The hour.</param>
    /// <returns>The light.</returns>
    public static Daylight At(DayNightSettings settings, float hour)
    {
        ArgumentNullException.ThrowIfNull(settings);
        hour = Wrap(hour);
        var keys = settings.Keys;
        var next = 0;
        while (next < keys.Count && keys[next].Hour <= hour)
        {
            next++;
        }

        var before = keys[next - 1];
        var after = next < keys.Count ? keys[next] : keys[0];
        var span = (next < keys.Count ? after.Hour : Hours) - before.Hour;
        var t = span > 0f ? (hour - before.Hour) / span : 0f;
        return new Daylight(
            hour,
            LightFrom(settings, hour),
            RgbColor.Lerp(before.Light, after.Light, t),
            Lerp(before.Energy, after.Energy, t),
            Lerp(before.Ambient, after.Ambient, t),
            RgbColor.Lerp(before.SkyTop, after.SkyTop, t),
            RgbColor.Lerp(before.SkyHorizon, after.SkyHorizon, t),
            RgbColor.Lerp(before.Haze, after.Haze, t),
            Lerp(before.Night, after.Night, t),
            Lerp(before.Lamps, after.Lamps, t),
            Lerp(before.Hearths, after.Hearths, t),
            Lerp(before.Mist, after.Mist, t),
            Lerp(before.Birds, after.Birds, t));
    }

    /// <summary>Which part of the day an hour is.</summary>
    /// <param name="settings">Day and night settings.</param>
    /// <param name="hour">The hour.</param>
    /// <returns>The part.</returns>
    public static DayPart PartOf(DayNightSettings settings, float hour)
    {
        ArgumentNullException.ThrowIfNull(settings);
        hour = Wrap(hour);
        var noon = (settings.Sunrise + settings.Sunset) / 2f;
        return hour < settings.Sunrise - 1f || hour >= settings.Sunset + 1f ? DayPart.Night
            : hour < settings.Sunrise + 1f ? DayPart.Dawn
            : hour >= settings.Sunset - 1f ? DayPart.Dusk
            : hour < noon - 1f ? DayPart.Morning
            : hour < noon + 1f ? DayPart.Noon
            : DayPart.Afternoon;
    }

    /// <summary>A part of the day in words.</summary>
    /// <param name="part">The part.</param>
    /// <returns>Its name.</returns>
    public static string WordOf(DayPart part) => part switch
    {
        DayPart.Dawn => "Dawn",
        DayPart.Morning => "Morning",
        DayPart.Noon => "Noon",
        DayPart.Afternoon => "Afternoon",
        DayPart.Dusk => "Dusk",
        DayPart.Night => "Night",
    };

    /// <summary>The way to the sun by day, or the moon by night: rising in the east and setting in the west, across the south of the sky.</summary>
    private static Vector3 LightFrom(DayNightSettings settings, float hour)
    {
        var day = hour >= settings.Sunrise && hour < settings.Sunset;
        var nightLength = Hours - (settings.Sunset - settings.Sunrise);
        var through = day
            ? (hour - settings.Sunrise) / (settings.Sunset - settings.Sunrise)
            : Wrap(hour - settings.Sunset) / nightLength;
        var arc = through * MathF.PI;
        var highest = (day ? settings.HighestSun : settings.HighestMoon) * MathF.PI / 180f;
        var up = MathF.Max(MathF.Sin(arc) * MathF.Sin(highest), MathF.Sin(settings.LowestLight * MathF.PI / 180f));
        return Vector3.Normalize(new Vector3(MathF.Cos(arc), up, MathF.Sin(arc) * MathF.Cos(highest)));
    }

    private static float Wrap(float hour) => ((hour % Hours) + Hours) % Hours;

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
