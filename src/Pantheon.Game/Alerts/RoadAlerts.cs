using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about roads among the player's villages (spec §59, ADR-033). Part of <see cref="AlertFeed"/>.</summary>
internal static class RoadAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when neither village concerns the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        var (roadId, fromId, toId) = worldEvent switch
        {
            RoadBuilt built => (built.RoadId, built.FromSettlementId, built.ToSettlementId),
            RoadChanged changed => (changed.RoadId, changed.FromSettlementId, changed.ToSettlementId),
            _ => (0, 0, 0),
        };
        var from = world.Settlements.Find(s => s.Id == fromId);
        var to = world.Settlements.Find(s => s.Id == toId);
        if (from is null || to is null || !(AlertFeed.Concerns(from, localGodId) || AlertFeed.Concerns(to, localGodId)))
        {
            return null;
        }

        return worldEvent switch
        {
            RoadBuilt => (Format("road:{0}", roadId), Format("A road now joins {0} and {1}: walkers go faster, and trade and food travel it.", from.Name, to.Name), AlertSeverity.Good, from.Position),
            RoadChanged { Open: false } => (Format("road-cut:{0}", roadId), Format("The road between {0} and {1} is cut: until it is mended no trade or food passes.", from.Name, to.Name), AlertSeverity.Warning, from.Position),
            RoadChanged => (Format("road-open:{0}", roadId), Format("The road between {0} and {1} is mended and open again.", from.Name, to.Name), AlertSeverity.Info, from.Position),
            _ => null,
        };
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
