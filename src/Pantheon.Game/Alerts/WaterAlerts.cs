using System.Globalization;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>Alerts about floods (spec §19, §59, ADR-022): a river over its banks or a rival's wave reaching the player's people. Part of <see cref="AlertFeed"/>.</summary>
internal static class WaterAlerts
{
    /// <summary>The alert for a flood, or <c>null</c> when it reaches none of the player's people or the player sent it.</summary>
    /// <param name="flood">The flood.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? DescribeFlood(FloodStarted flood, WorldState world, int? localGodId)
    {
        if (flood.GodId == localGodId && flood.GodId is not null)
        {
            return null;
        }

        var drowned = Flooded(world, localGodId);
        var key = string.Create(CultureInfo.InvariantCulture, $"flood:{flood.RiverId ?? 0}:{flood.GodId ?? 0}");
        if (flood.RiverId is { } riverId && world.Water.Rivers.Find(r => r.Id == riverId) is { } river)
        {
            return drowned is null
                ? null
                : (key, Format("The {0} has burst its banks and floods {1}.", river.Name, drowned.Name), AlertSeverity.Danger, drowned.Position);
        }

        if (flood.GodId is { } godId && GodStanding.Find(world, godId) is { } god && drowned is not null)
        {
            return (key, Format("{0} sends the sea over {1}.", god.Name, drowned.Name), AlertSeverity.Danger, drowned.Position);
        }

        return null;
    }

    /// <summary>The player's most flooded settlement, if any lies under water.</summary>
    private static SettlementState? Flooded(WorldState world, int? localGodId)
    {
        SettlementState? worst = null;
        var deepest = 0f;
        foreach (var settlement in world.Settlements)
        {
            var flood = world.Regions.Cells[settlement.RegionId].Flood;
            if (flood > deepest && settlement.Population > 0 && AlertFeed.Concerns(settlement, localGodId))
            {
                deepest = flood;
                worst = settlement;
            }
        }

        return worst;
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
