using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Resources;
using Pantheon.Simulation.Resources;
using Pantheon.Simulation.Roads;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Diagnostics;

/// <summary>The F1 debug panel's lines on wood, stone and roads (ADR-033). Pure.</summary>
public static class ResourceReadout
{
    /// <summary>Adds how much of the island's forest stands, and how many roads are built, being built and cut.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data; nothing is added where nobody gathers.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        var rules = snapshot.SettlementRules;
        if (!rules.Resources.Enabled && !rules.Roads.Enabled)
        {
            return;
        }

        var world = snapshot.World;
        var forest = world.Regions.Cells.Sum(r => r.Forest);
        var built = world.Roads.Count(r => r.Built >= r.Length);
        lines.Add(Format(
            "Resources: {0:0%} of the forest standing, {1} roads built, {2} being built, {3} cut",
            forest > 0f ? world.Regions.Cells.Sum(r => r.Timber) / forest : 0f,
            built,
            world.Roads.Count - built,
            world.Roads.Count(r => r.Built >= r.Length && !RoadModel.IsOpen(rules.Roads, r))));
    }

    /// <summary>A village's stores, gatherers, the land it gathers from and its roads.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>One line, or <c>null</c> where nobody gathers.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settlement);
        var rules = snapshot.SettlementRules;
        if (!rules.Resources.Enabled)
        {
            return null;
        }

        var labour = SettlementEconomy.AllocateLabour(rules, settlement);
        var (_, timber, stone) = ResourceModel.Survey(rules.Resources, snapshot.World.Regions, settlement);
        return Format(
            "  resources: {0}; land around: timber {1:0.00}, stone {2:0.00}; {3}",
            ResourceText.Stores(rules, settlement, labour.Gatherers).ToLowerInvariant(),
            timber,
            stone,
            ResourceText.Roads(rules.Roads, snapshot.World, settlement).ToLowerInvariant());
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
