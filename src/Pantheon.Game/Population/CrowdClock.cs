using System;

namespace Pantheon.Game.Population;

/// <summary>
/// The time the village crowds live by (spec §80, ADR-049): it runs with the game, never faster than
/// <see cref="CitizenSettings.PaceCap"/> times real time, and stops while the game is paused. So
/// people walk at a human pace however fast the world is run. Presentation only: never saved, and
/// the simulation never reads it.
/// </summary>
public sealed class CrowdClock
{
    /// <summary>Seconds of crowd time so far.</summary>
    public double Seconds { get; private set; }

    /// <summary>Moves the clock on by a frame.</summary>
    /// <param name="realSeconds">The frame's real time.</param>
    /// <param name="timeScale">How fast the game runs (1 is real time).</param>
    /// <param name="paused">Whether the game is paused.</param>
    /// <param name="paceCap">The most crowd seconds a real second may hold.</param>
    /// <returns>The clock's new time.</returns>
    public double Advance(double realSeconds, double timeScale, bool paused, float paceCap)
    {
        if (!paused && realSeconds > 0.0 && timeScale > 0.0)
        {
            Seconds += realSeconds * Math.Min(timeScale, paceCap);
        }

        return Seconds;
    }
}
