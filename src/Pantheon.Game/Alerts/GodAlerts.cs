using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about the gods the player meets (ADR-036), and how they answer Creatures in their lands (ADR-070). Part of <see cref="AlertFeed"/>.</summary>
internal static class GodAlerts
{
    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        if (worldEvent is CreatureWatched watched)
        {
            return Watched(watched, world, localGodId);
        }

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

    /// <summary>
    /// A god's answer to a Creature in its lands (ADR-070), when the player's Creature is the visitor or
    /// the player's village the place: what the god decided, and what it sent.
    /// </summary>
    private static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Watched(CreatureWatched watched, WorldState world, int? localGodId)
    {
        if (localGodId is not { } me || (watched.GodId != me && watched.VisitorGodId != me)
            || world.Gods.Find(g => g.Id == watched.GodId) is not { } god
            || world.Creatures.Find(c => c.Id == watched.CreatureId) is not { } visitor
            || world.Settlements.Find(s => s.Id == watched.SettlementId) is not { } village)
        {
            return null;
        }

        var key = string.Format(CultureInfo.InvariantCulture, "watch:{0}:{1}", god.Id, visitor.Id);
        var guard = world.Creatures.Find(c => c.Id == watched.GuardId)?.Name;
        if (watched.GodId == me)
        {
            var owner = world.Gods.Find(g => g.Id == watched.VisitorGodId)?.Name ?? "another god";
            return (key, Format("{0}, {1}'s Creature, has come into {2}.", visitor.Name, owner, village.Name), AlertSeverity.Warning, village.Position);
        }

        var (text, severity) = watched.Answer switch
        {
            WatchAnswer.Welcome => (guard is null
                ? Format("{0} welcomes {1} to {2}.", god.Name, visitor.Name, village.Name)
                : Format("{0} welcomes {1} to {2}: {3} comes to greet it.", god.Name, visitor.Name, village.Name, guard), AlertSeverity.Good),
            WatchAnswer.Watch => (guard is null
                ? Format("{0} is wary of {1} in {2}, and holds the trespass against you.", god.Name, visitor.Name, village.Name)
                : Format("{0} is wary of {1} in {2}: {3} comes to watch it, and {0} holds the trespass against you.", god.Name, visitor.Name, village.Name, guard), AlertSeverity.Warning),
            WatchAnswer.Repel => (guard is null
                ? Format("{0} is angered by {1} in {2}, and holds the trespass against you.", god.Name, visitor.Name, village.Name)
                : Format("{0} drives {1} out of {2}: it sets {3} on it!", god.Name, visitor.Name, village.Name, guard), AlertSeverity.Danger),
            WatchAnswer.Unanswered => (Format("{0} has come into {1}, {2}'s village.", visitor.Name, village.Name, god.Name), AlertSeverity.Info),
        };
        return (key, text, severity, village.Position);
    }

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
