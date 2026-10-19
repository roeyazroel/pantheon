using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Migration;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Migration;

namespace Pantheon.Game.Diagnostics;

/// <summary>The migration lines of the F1 debug panel (spec §106 "migration routes", ADR-016). Pure.</summary>
public static class MigrationReadout
{
    /// <summary>Most groups listed.</summary>
    public const int ListedGroups = 4;

    /// <summary>Adds the groups on the road and every god's refugee policy.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data; nothing is added without migration rules.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.MigrationRules is null)
        {
            return;
        }

        var world = snapshot.World;
        var groups = world.Migration.Groups;
        lines.Add(Format(
            "Migration: {0} groups, {1} people on the road; refugees: {2}",
            groups.Count,
            groups.Sum(g => g.Population),
            string.Join(", ", world.Gods.Select(g => g.Name + " " + MigrationText.Policy(g.RefugeePolicy).ToLowerInvariant() + Reason(snapshot, g.Id)))));
        foreach (var group in groups.Take(ListedGroups))
        {
            lines.Add(Format(
                "  #{0} {1}: food {2:0}, health {3:0.00}, {4} waypoints left{5}",
                group.Id,
                MigrationText.Group(group, world),
                group.Food,
                group.Health,
                group.Path.Count,
                MigrationModel.MainGod(group.Beliefs) is { } god ? ", mostly following " + (GodStanding.Find(world, god)?.Name ?? "?") : ", following no god"));
        }
    }

    /// <summary>A settlement's pressure to leave and the people ready to go.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>One line, or <c>null</c> without migration rules or a measured pressure.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settlement);
        if (snapshot.MigrationRules is null || snapshot.MigrationPressure is not { } pressure)
        {
            return null;
        }

        return Format(
            "  migration: pressure {0} (leaving above {1:0.00}), {2:0.0} want to leave, {3} on their way here",
            MigrationText.Pressure(pressure),
            snapshot.MigrationRules.PressureThreshold,
            settlement.Emigration,
            snapshot.World.Migration.Groups.Where(g => g.DestinationSettlementId == settlement.Id).Sum(g => g.Population));
    }

    /// <summary>Why an AI god has its refugee policy, if the snapshot says.</summary>
    private static string Reason(DebugSnapshot snapshot, int godId) =>
        snapshot.RefugeeChoices.FirstOrDefault(c => c.GodId == godId) is { Reason: { } reason } ? " (" + reason + ")" : string.Empty;

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
