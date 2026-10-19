using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Disasters;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about the powers of ADR-030 (spec §14, §24, §55, §59): what gods' hands do to the player's
/// people, the paths Creatures take up, and the Divine Acts, which every god hears of. Part of
/// <see cref="AlertFeed"/>.
/// </summary>
internal static class PowerAlerts
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
            case VillagersLifted lifted when lifted.GodId != localGodId && Village(world, lifted.SettlementId) is { } place && AlertFeed.Concerns(place, localGodId):
                return (Format("lifted:{0}", place.Id), Format("{0}'s hand lifts {1} people out of {2}.", God(world, lifted.GodId), lifted.People, place.Name), lifted.FromDanger ? AlertSeverity.Info : AlertSeverity.Warning, place.Position);
            case VillagersSetDown { Rescued: true } rescue when rescue.GodId == localGodId && Village(world, rescue.ToSettlementId) is { } safe:
                return (Format("rescued:{0}", safe.Id), Format("You carry {0} people to safety in {1}: they and all who saw it love you for it.", rescue.People, safe.Name), AlertSeverity.Good, safe.Position);
            case VillagersSetDown { Drowned: true } drowned when Village(world, drowned.FromSettlementId) is { } home && (drowned.GodId == localGodId || AlertFeed.Concerns(home, localGodId)):
                return (Format("drowned:{0}", home.Id), Format("{0} people of {1} drown in the sea, dropped by {2}.", drowned.People, home.Name, drowned.GodId == localGodId ? "your hand" : God(world, drowned.GodId)), AlertSeverity.Danger, home.Position);
            case HandStruck { SettlementId: { } id } struck when Village(world, id) is { } hit && (struck.GodId == localGodId || AlertFeed.Concerns(hit, localGodId)):
                return (Format("struck:{0}", hit.Id), Format("{0} {1} {2}: {3} houses fall, {4} dead.", struck.GodId == localGodId ? "You hurl" : God(world, struck.GodId) + " hurls", Thing(struck.Kind), Into(hit.Name), struck.HousesLost, struck.Deaths), AlertSeverity.Danger, struck.Position);
            case CreatureSpecialised calling when world.Creatures.Find(c => c.Id == calling.CreatureId) is { } creature:
                var mine = calling.GodId == localGodId;
                var path = CreatureText.Path(calling.Specialisation);
                return (Format("calling:{0}:{1}", creature.Id, (int)calling.Specialisation), mine
                    ? Format("{0} becomes a {1}: it {2}.", creature.Name, path, CreatureText.PathEffect(calling.Specialisation))
                    : Format("{0}'s {1} becomes a {2}.", God(world, calling.GodId), creature.Name, path), mine ? AlertSeverity.Good : AlertSeverity.Info, creature.Position);
            case DivineActDone act:
                var near = DisasterText.Nearest(world, act.Position)?.Name ?? "the wilds";
                var who = act.GodId == localGodId ? "You" : God(world, act.GodId);
                return (Format("act:{0}:{1}", act.GodId, (int)act.Act), act.Act switch
                {
                    DivineActKind.Hallowing => Format("{0} {1} the land around {2}: its blessing will never fade.", who, act.GodId == localGodId ? "hallow" : "hallows", near),
                    DivineActKind.Awakening => Format("{0} {1} {2}: the volcano rumbles.", who, act.GodId == localGodId ? "wake" : "wakes", world.Disasters.Volcano is { } volcano ? DisasterText.VolcanoName(world, volcano.RegionId) : "the mountain"),
                    DivineActKind.Stillness => Format("{0} {1} the world: every quake, fire, flood and storm falls quiet.", who, act.GodId == localGodId ? "still" : "stills"),
                    DivineActKind.Raising => Format("{0} {1} an island from the sea off {2}: new land to settle.", who, act.GodId == localGodId ? "raise" : "raises", near),
                    DivineActKind.Splitting => Format("{0} {1} the mountain above {2}: a gorge opens through it.", who, act.GodId == localGodId ? "split" : "splits", near),
                    DivineActKind.Diverting => Format("{0} {1} {2} towards {3}: its old course runs dry below the dam.", who, act.GodId == localGodId ? "turn" : "turns", act.Name ?? "a river", near),
                }, act.Act == DivineActKind.Awakening ? AlertSeverity.Warning : AlertSeverity.Good, act.Position);
            default:
                return null;
        }
    }

    /// <summary>The settlement with an id, if it is still there.</summary>
    private static Domain.Settlements.SettlementState? Village(WorldState world, int? id) => world.Settlements.Find(s => s.Id == id);

    /// <summary>A god's name.</summary>
    private static string God(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "A god";

    /// <summary>A thrown thing.</summary>
    private static string Thing(HeldKind kind) => kind switch
    {
        HeldKind.Rock => "a rock",
        HeldKind.Tree => "a tree",
        HeldKind.Villagers => "people",
        HeldKind.Creature => "a Creature",
        HeldKind.Offering => "an offering",
    };

    /// <summary>Where a thrown thing lands.</summary>
    private static string Into(string place) => "into " + place;

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
