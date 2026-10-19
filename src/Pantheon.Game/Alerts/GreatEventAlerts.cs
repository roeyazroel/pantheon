using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Game.GreatEvents;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about the great events, which concern everyone, and the plague reaching the player's villages (spec §59, ADR-034). Part of <see cref="AlertFeed"/>.</summary>
internal static class GreatEventAlerts
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
            case GreatEventBegan began:
                var name = GreatEventText.Capitalised(began.Name);
                var reach = world.GreatEvents.Events.Find(e => e.Id == began.EventId)?.Peak ?? 0;
                var (text, severity) = began.Kind switch
                {
                    GreatEventKind.Plague => (GreatEventText.Format("{0} spreads: {1} are sick. Heal them, or it will travel the roads.", name, GreatEventText.Count(reach, "village")), AlertSeverity.Danger),
                    GreatEventKind.Eclipse => (GreatEventText.Format("{0}: the sun goes dark. The people are afraid, and a miracle worked now will awe them.", name), AlertSeverity.Warning),
                    GreatEventKind.Reformation when began.GodId == localGodId => (GreatEventText.Format("Your faith reforms itself in {0}: doubt gives way to faith, but {1} leave you.", began.Name, reach), AlertSeverity.Good),
                    GreatEventKind.Reformation => (GreatEventText.Format("The faith of {0} reforms itself in {1}.", GreatEventText.God(world, began.GodId), began.Name), AlertSeverity.Info),
                    GreatEventKind.WorldWar => (GreatEventText.Format("{0}: {1} are at war at once, and fear grows everywhere.", name, GreatEventText.Count(reach, "god")), AlertSeverity.Danger),
                    GreatEventKind.GreatDrought => (GreatEventText.Format("{0}: the whole island is parched.", name), AlertSeverity.Warning),
                    GreatEventKind.GreatMigration => (GreatEventText.Format("{0}: {1} people are on the road.", name, reach), AlertSeverity.Info),
                };
                return (GreatEventText.Format("great:{0}", began.EventId), text, severity, null);
            case GreatEventEnded ended:
                return (GreatEventText.Format("great-end:{0}", ended.EventId), ended.Kind == GreatEventKind.Eclipse
                    ? "The sun returns."
                    : GreatEventText.Format("{0} is over.", GreatEventText.Capitalised(ended.Name)), AlertSeverity.Info, null);
            case PlagueStruck struck when world.Settlements.Find(s => s.Id == struck.SettlementId) is { } village && AlertFeed.Concerns(village, localGodId):
                var from = struck.FromSettlementId is { } source && world.Settlements.Find(s => s.Id == source) is { } carrier ? " from " + carrier.Name : string.Empty;
                return (GreatEventText.Format("plague:{0}:{1}", village.Id, struck.Tick), GreatEventText.Format("The plague reaches {0}{1}: heal its people before it spreads.", village.Name, from), AlertSeverity.Warning, village.Position);
            default:
                return null;
        }
    }
}
