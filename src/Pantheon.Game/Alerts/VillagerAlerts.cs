using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Villagers;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about the villager the player follows (spec §59, ADR-043): their flight, their turning and their death. Part of <see cref="AlertFeed"/>.</summary>
internal static class VillagerAlerts
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
            case VillagerFled fled when fled.GodId == localGodId:
                return (Format("villager-fled:{0}", fled.VillagerId), Format("{0} flees {1} in {2}{3}.", fled.Name, VillagerText.Danger(fled.Cause), VillagerText.Village(world, fled.SettlementId),
                    fled.RefugeId is { } refuge ? " for " + VillagerText.Village(world, refuge) : string.Empty), AlertSeverity.Warning, Place(world, fled.SettlementId));
            case VillagerTurned turned when turned.GodId == localGodId:
                var to = VillagerText.God(world, turned.ToGodId);
                return (Format("villager-turned:{0}", turned.VillagerId), turned.FromGodId is { } from
                    ? Format("{0} turns from {1} to {2}.", turned.Name, VillagerText.God(world, from), to)
                    : Format("{0} comes to believe in {1}.", turned.Name, to), turned.ToGodId == localGodId ? AlertSeverity.Good : AlertSeverity.Info, null);
            case VillagerDied died when died.GodId == localGodId:
                return (Format("villager-died:{0}", died.VillagerId), Format("{0} of {1} dies {2}, aged {3}. Press / on someone else to follow them.", died.Name, VillagerText.Village(world, died.SettlementId), VillagerText.Fate(died.Fate), died.Age),
                    AlertSeverity.Warning, Place(world, died.SettlementId));
            default:
                return null;
        }
    }

    /// <summary>Where a village is, if it is still there.</summary>
    private static GroundPosition? Place(WorldState world, int settlementId) => world.Settlements.Find(s => s.Id == settlementId)?.Position;

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
