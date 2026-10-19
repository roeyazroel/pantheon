using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Game.Society;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about settlements' society (spec §29, §36, §59, ADR-023): a rival setting the course of
/// one of the player's villages, buildings and temples raised, villages founded, and the player's
/// villages mastering a field. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class SocietyAlerts
{
    // A rival's priority is news where the player is followed by at least this share.
    private const float Stake = 0.3f;

    // Technology is news from this level on, where the player holds the majority.
    private const int NotableLevel = 2;

    /// <summary>The alert for an event, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        switch (worldEvent)
        {
            case PriorityChanged changed when changed.GodId != localGodId && Find(world, changed.SettlementId) is { } place && Share(place, localGodId) >= Stake
                && GodStanding.Find(world, changed.GodId) is { } rival:
                return (Key("priority", place.Id), Format("{0} asks {1} to {2}.", rival.Name, place.Name, SocietyText.Name(changed.Priority).ToLowerInvariant()), AlertSeverity.Info, place.Position);
            case BuildingCompleted { Kind: BuildingKind.Temple } temple when Find(world, temple.SettlementId) is { } place && AlertFeed.Concerns(place, localGodId):
                return temple.GodId == localGodId
                    ? (Key("temple", place.Id), Format("{0} raises a temple to you.", place.Name), AlertSeverity.Good, place.Position)
                    : (Key("temple", place.Id), Format("{0} raises a temple to {1}.", place.Name, GodStanding.Find(world, temple.GodId ?? 0)?.Name ?? "another god"), AlertSeverity.Warning, place.Position);
            case BuildingCompleted built when Find(world, built.SettlementId) is { } place && Share(place, localGodId) >= 0.5f:
                return (Key("built" + built.Kind, place.Id), Format("{0} has built {1}.", place.Name, SocietyText.Name(built.Kind)), AlertSeverity.Info, place.Position);
            case SettlementFounded founded when Find(world, founded.SettlementId) is { } village && Find(world, founded.FromSettlementId) is { } home && AlertFeed.Concerns(home, localGodId):
                return (Key("founded", village.Id), Format("Settlers from {0} found {1}.", home.Name, village.Name), AlertSeverity.Good, village.Position);
            case TechnologyAdvanced advanced when advanced.Level >= NotableLevel && Find(world, advanced.SettlementId) is { } place && Share(place, localGodId) >= 0.5f:
                return (Key("tech" + advanced.Field, place.Id), Format("{0} masters {1} (level {2}).", place.Name, SocietyText.Name(advanced.Field), advanced.Level), AlertSeverity.Info, place.Position);
            default:
                return null;
        }
    }

    /// <summary>The share of a settlement following the player.</summary>
    private static float Share(SettlementState settlement, int? godId) => godId is { } god ? BeliefModel.Find(settlement, god)?.Adherence ?? 0f : 0f;

    /// <summary>The settlement with an id, if it exists.</summary>
    private static SettlementState? Find(WorldState world, int id) => world.Settlements.Find(s => s.Id == id);

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
