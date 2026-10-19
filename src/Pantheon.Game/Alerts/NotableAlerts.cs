using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;
using Pantheon.Game.Notables;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about prophets and elders (spec §59, ADR-032): the player's own, and rivals' among the player's people. Part of <see cref="AlertFeed"/>.</summary>
internal static class NotableAlerts
{
    // Fewer converts than this are not worth an alert.
    private const int NotedConverts = 3;

    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        switch (worldEvent)
        {
            case NotableRose { Kind: NotableKind.Prophet } rose when Village(world, rose.SettlementId) is { } place && (rose.GodId == localGodId || AlertFeed.Concerns(place, localGodId)):
                var name = world.Notables.Find(n => n.Id == rose.NotableId)?.Name ?? "a prophet";
                return rose.GodId == localGodId
                    ? (Format("prophet:{0}", rose.NotableId), Format("{0}, a prophet of yours, rises in {1}{2}: they will walk and preach your name.", name, place.Name, rose.Anointed ? " at your anointing" : string.Empty), AlertSeverity.Good, place.Position)
                    : (Format("prophet:{0}", rose.NotableId), Format("{0}, a prophet of {1}, rises in {2}.", name, NotableText.God(world, rose.GodId), place.Name), AlertSeverity.Warning, place.Position);
            case NotablePreached preached when preached.Converted >= NotedConverts && preached.GodId != localGodId && Village(world, preached.SettlementId) is { } village && AlertFeed.Concerns(village, localGodId):
                return (Format("preached:{0}:{1}", preached.NotableId, village.Id), Format("A prophet of {0} preaches in {1}: {2} turn to {0}.", NotableText.God(world, preached.GodId), village.Name, preached.Converted), AlertSeverity.Warning, village.Position);
            case NotableDied { Martyred: true } martyr when martyr.GodId == localGodId || (Village(world, martyr.SettlementId) is { } struck && AlertFeed.Concerns(struck, localGodId)):
                return (Format("martyr:{0}", martyr.NotableId), martyr.GodId == localGodId
                    ? Format("Your prophet {0} dies a martyr at {1}: the faithful hold to you the harder.", martyr.Name, NotableText.Village(world, martyr.SettlementId))
                    : Format("{0}, a prophet of {1}, dies a martyr at {2}.", martyr.Name, NotableText.God(world, martyr.GodId), NotableText.Village(world, martyr.SettlementId)), martyr.GodId == localGodId ? AlertSeverity.Warning : AlertSeverity.Info, Village(world, martyr.SettlementId)?.Position);
            // An elder who follows the village is told already by the village turning (FollowingChanged).
            case ElderTurned { Resented: true } turned when turned.FromGodId == localGodId || turned.ToGodId == localGodId:
                var elder = world.Notables.Find(n => n.Id == turned.NotableId)?.Name ?? "The elder";
                var home = NotableText.Village(world, turned.SettlementId);
                return (Format("turned:{0}", turned.NotableId), turned.FromGodId == localGodId
                    ? Format("{0}, elder of {1}, turns from you to {2}: the village resents you.", elder, home, NotableText.God(world, turned.ToGodId))
                    : Format("{0}, elder of {1}, turns from {2} to you.", elder, home, NotableText.God(world, turned.FromGodId)), turned.FromGodId == localGodId ? AlertSeverity.Danger : AlertSeverity.Good, Village(world, turned.SettlementId)?.Position);
            default:
                return null;
        }
    }

    /// <summary>The settlement with an id, if it is still there.</summary>
    private static Domain.Settlements.SettlementState? Village(WorldState world, int id) => world.Settlements.Find(s => s.Id == id);

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
