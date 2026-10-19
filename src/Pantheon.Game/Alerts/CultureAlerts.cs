using System.Globalization;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Culture;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about culture (spec §7, §8, §11, §33–35, §59, ADR-024): revolts, villages turning from the
/// gods, doctrines the player's villages embrace, and how the people come to see the gods. Part of
/// <see cref="AlertFeed"/>.
/// </summary>
internal static class CultureAlerts
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
            case Rebellion revolt when Find(world, revolt.SettlementId) is { } place && (revolt.GodId == localGodId || AlertFeed.Concerns(place, localGodId)):
                return revolt.GodId == localGodId
                    ? (Key("revolt", place.Id), Format("{0} revolts against you: {1} renounce you and tear down your temple.", place.Name, revolt.People), AlertSeverity.Danger, place.Position)
                    : (Key("revolt", place.Id), Format("{0} revolts against {1}.", place.Name, GodStanding.Find(world, revolt.GodId)?.Name ?? "its god"), AlertSeverity.Info, place.Position);
            case SecularismRose { Stage: SecularStage.Atheist or SecularStage.AntiDivine } rose when Find(world, rose.SettlementId) is { } place && AlertFeed.Concerns(place, localGodId):
                return (Key("secular", place.Id), rose.Stage == SecularStage.AntiDivine
                    ? Format("{0} turns against the gods: its people resist every miracle and draw others away.", place.Name)
                    : Format("{0} no longer believes in the gods: it yields little power and resists miracles.", place.Name), AlertSeverity.Warning, place.Position);
            case DoctrineEmbraced embraced when Find(world, embraced.SettlementId) is { } place && Share(place, localGodId) >= 0.5f:
                return (Key("doctrine" + embraced.Doctrine, place.Id), Format("{0} embraces {1}.", place.Name, CultureText.Name(embraced.Doctrine)), AlertSeverity.Info, place.Position);
            case TraitEarned trait when GodStanding.Find(world, trait.GodId) is { } god:
                return trait.GodId == localGodId
                    ? (Key("trait" + trait.Trait, god.Id), Format("Your people now speak of you as {0}.", CultureText.Name(trait.Trait)), AlertSeverity.Info, null)
                    : (Key("trait" + trait.Trait, god.Id), Format("The world now speaks of {0} as {1}.", god.Name, CultureText.Name(trait.Trait)), AlertSeverity.Info, null);
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
