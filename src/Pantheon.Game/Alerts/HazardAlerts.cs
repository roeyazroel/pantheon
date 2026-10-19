using System.Globalization;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Disasters;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about the hazards of ADR-029 (spec §19, §59): waves, landslides, the volcano, tornadoes,
/// falling stars, blight and locusts. The damage they do is told as any disaster's. Part of
/// <see cref="AlertFeed"/>.
/// </summary>
internal static class HazardAlerts
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
            case TsunamiStruck wave when DisasterText.Nearest(world, wave.Shore) is { } place:
                return (Format("tsunami:{0}", place.Id), Format("A great wave comes ashore by {0}: the low land floods.", place.Name), AlertFeed.Concerns(place, localGodId) ? AlertSeverity.Danger : AlertSeverity.Info, wave.Shore);
            case LandslideStruck slide when Buried(world, slide) is { } place && AlertFeed.Concerns(place, localGodId):
                return (Format("landslide:{0}", place.Id), Format("A landslide buries the land by {0}: the way is blocked until the rubble is cleared.", place.Name), AlertSeverity.Danger, place.Position);
            case VolcanoChanged volcano:
                var name = DisasterText.VolcanoName(world, volcano.RegionId);
                var crater = world.Regions.GetRegionCenter(volcano.RegionId);
                return volcano.Phase switch
                {
                    VolcanoPhase.Rumbling => (Format("volcano:{0}", (int)volcano.Phase), Format("{0} rumbles: the volcano is waking.", name), AlertSeverity.Warning, crater),
                    VolcanoPhase.Erupting => (Format("volcano:{0}", (int)volcano.Phase), Format("{0} erupts: lava runs down its slope and ash falls around it.", name), AlertSeverity.Danger, crater),
                    VolcanoPhase.Dormant => (Format("volcano:{0}", (int)volcano.Phase), Format("{0} falls quiet.", name), AlertSeverity.Good, crater),
                };
            case TornadoTouchedDown tornado when DisasterText.Nearest(world, tornado.Position) is { } place && AlertFeed.Concerns(place, localGodId):
                return (Format("tornado:{0}", tornado.TornadoId), Format("A tornado touches down near {0}.", place.Name), AlertSeverity.Danger, tornado.Position);
            case MeteorStruck meteor when DisasterText.Nearest(world, meteor.Position) is { } place:
                return (Format("meteor:{0}", place.Id), Format("A falling star strikes near {0}!", place.Name), AlertSeverity.Danger, meteor.Position);
            case BlightChanged blight when world.Settlements.Find(s => s.Id == blight.SettlementId) is { } place && AlertFeed.Concerns(place, localGodId):
                return blight.Started
                    ? (Format("blight:{0}", place.Id), Format("Blight in the fields of {0}: its harvest withers. Bless Crops would cure it.", place.Name), AlertSeverity.Warning, place.Position)
                    : (Format("blighted:{0}", place.Id), Format("The blight has left the fields of {0}.", place.Name), AlertSeverity.Good, place.Position);
            case SwarmArrived swarm when world.Settlements.Find(s => s.Id == swarm.SettlementId) is { } place && AlertFeed.Concerns(place, localGodId):
                return (Format("locusts:{0}", place.Id), Format("Locusts descend on {0}: they are eating its stores. Rain would drown them.", place.Name), AlertSeverity.Danger, place.Position);
            default:
                return null;
        }
    }

    /// <summary>The village in the land a landslide buried, or in the slope that slid.</summary>
    private static Domain.Settlements.SettlementState? Buried(WorldState world, LandslideStruck slide) =>
        world.Settlements.Find(s => s.RegionId == slide.ToRegionId) ?? world.Settlements.Find(s => s.RegionId == slide.RegionId);

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
