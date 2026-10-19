using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Weather;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about the seasons and the land's life (spec §17, §18, §59, ADR-028): a new season, and a
/// village of the player's whose land is scarred. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class NatureAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        switch (worldEvent)
        {
            case SeasonChanged season:
                return (Format("season:{0}", (int)season.Season), Format("{0} of year {1}: {2}.", SeasonText.Name(season.Season), season.Year, SeasonText.Effect(season.Season)),
                    season.Season == Season.Winter ? AlertSeverity.Warning : AlertSeverity.Info, null);
            case LandScarred scarred when world.Settlements.Find(s => s.Id == scarred.SettlementId) is { } place && AlertFeed.Concerns(place, localGodId):
                return (Format("scar:{0}", place.Id), Format("The land around {0} is scarred: its harvests and game will not recover without a god of nature.", place.Name), AlertSeverity.Warning, place.Position);
            default:
                return null;
        }
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
