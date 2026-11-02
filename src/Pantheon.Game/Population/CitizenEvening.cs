using System;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Population;

/// <summary>Where a figure stands in the turn of the day.</summary>
public enum EveningPhase
{
    /// <summary>Up and about its routine.</summary>
    Abroad,

    /// <summary>Its day is done: it walks home.</summary>
    Homeward,

    /// <summary>The night has come: it is in, whether it has walked there or not.</summary>
    Indoors,
}

/// <summary>
/// When people go in and come out (ADR-080): each has its own hour to leave off and its own hour to
/// rise, so a village empties and fills over a while, not at once. Presentation only. Pure.
/// </summary>
public static class CitizenEvening
{
    /// <summary>Where a figure stands at an hour.</summary>
    /// <param name="day">How the day passes.</param>
    /// <param name="hour">The hour, 0 up to 24.</param>
    /// <param name="share">The figure's own place among its neighbours, 0..1: lower leaves off and rises sooner.</param>
    /// <returns>Its phase.</returns>
    public static EveningPhase Of(DayNightSettings day, float hour, float share)
    {
        ArgumentNullException.ThrowIfNull(day);
        var leaves = day.HomeFrom + (share * (day.HomeBy - day.HomeFrom));
        var rises = day.UpFrom + (share * (day.UpBy - day.UpFrom));
        return hour >= rises && hour < leaves ? EveningPhase.Abroad
            : hour >= leaves && hour < day.IndoorsBy ? EveningPhase.Homeward
            : EveningPhase.Indoors;
    }

    /// <summary>
    /// How many times faster than a walk people hurry home, so that they get there by nightfall even
    /// while the game runs fast and the evening passes in a moment.
    /// </summary>
    /// <param name="day">How the day passes.</param>
    /// <param name="hoursPassed">Hours of the day passed this frame.</param>
    /// <param name="crowdSeconds">The crowds' seconds passed this frame.</param>
    /// <returns>The factor, at least 1.</returns>
    public static float Hurry(DayNightSettings day, float hoursPassed, float crowdSeconds)
    {
        ArgumentNullException.ThrowIfNull(day);
        if (!(crowdSeconds > 0f) || !(hoursPassed > 0f))
        {
            return 1f;
        }

        var usual = DayNight.Hours / day.DaySeconds;
        return Math.Clamp(hoursPassed / crowdSeconds / usual, 1f, day.MostHurry);
    }
}
