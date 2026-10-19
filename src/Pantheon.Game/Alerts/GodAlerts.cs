using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about the gods the player meets (ADR-036). Part of <see cref="AlertFeed"/>.</summary>
internal static class GodAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        if (worldEvent is not GodsMet met || localGodId is not { } me || (met.GodId != me && met.OtherGodId != me)
            || world.Gods.Find(g => g.Id == (met.GodId == me ? met.OtherGodId : met.GodId)) is not { } other)
        {
            return null;
        }

        var place = world.Settlements.Find(s => s.Id == met.SettlementId);
        var personality = DivineText.Personality(other.Personality);
        var name = personality.Length > 0 ? other.Name + " the " + personality : other.Name;
        return (string.Format(CultureInfo.InvariantCulture, "met:{0}", other.Id),
            string.Format(CultureInfo.InvariantCulture, "You have met {0}, {1}. You may now deal with it (P).", name, Gods.GoalText.Meeting(met.Way, place?.Name)),
            AlertSeverity.Info,
            place?.Position);
    }
}
