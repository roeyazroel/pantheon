using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;

namespace Pantheon.Game.Alerts;

/// <summary>A god's words to the player, unasked (ADR-072): a warning, a threat, an offer or thanks. Part of <see cref="AlertFeed"/>.</summary>
internal static class MindAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when it is not for the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        if (worldEvent is not GodSpoke spoke || localGodId is not { } me || spoke.ToGodId != me || world.Gods.Find(g => g.Id == spoke.GodId) is not { } god)
        {
            return null;
        }

        // Each of a god's words is its own alert; the key keeps a repeat of the same words in a tick from showing twice.
        return (string.Format(CultureInfo.InvariantCulture, "spoke:{0}:{1}:{2}", god.Id, spoke.Tick, spoke.Text),
            string.Format(CultureInfo.InvariantCulture, "{0}: “{1}” (P, Speak to answer)", god.Name, spoke.Text.Trim('“', '”', '"')),
            AlertSeverity.Info,
            null);
    }
}
