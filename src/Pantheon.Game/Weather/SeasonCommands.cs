using System;
using System.Globalization;
using Pantheon.Domain.Weather;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Weather;

/// <summary>Debug commands over the seasons (ADR-028): the Y hotkey and <c>--season</c>. Only the host changes its world.</summary>
public static class SeasonCommands
{
    /// <summary>Begins the next season now.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <returns>Status line.</returns>
    public static string NextSeason(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ClimateModel.SeasonOf(session.Content.Weather, session.World) is { } season
            ? SetSeason(session, (Season)(((int)season + 1) % 4))
            : "This world has no seasons";
    }

    /// <summary>Begins a season now, giving a world without seasons its year.</summary>
    /// <param name="session">Session whose world is changed.</param>
    /// <param name="season">The season.</param>
    /// <returns>Status line.</returns>
    public static string SetSeason(GameSession session, Season season)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (SessionCommands.HostOnly(session) is { } refusal)
        {
            return refusal;
        }

        var world = session.World;
        var origin = ClimateModel.OriginFor(session.Content.Weather, season, world.Tick);
        if (world.Seasons is { } seasons)
        {
            seasons.OriginTick = origin;
        }
        else
        {
            world.Seasons = new SeasonState { OriginTick = origin };
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} begins: {1}", SeasonText.Name(season), SeasonText.Effect(season));
    }
}
