using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Game.GreatEvents;
using Pantheon.Simulation.GreatEvents;

namespace Pantheon.Game.Diagnostics;

/// <summary>The F1 debug panel's lines on the great events and the plague (ADR-034). Pure.</summary>
public static class GreatEventReadout
{
    /// <summary>Adds the great events under way, the next eclipse, the villages sick, and how close each god's faith is to reforming.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        var world = snapshot.World;
        var state = world.GreatEvents;
        var sick = world.Settlements.Where(s => s.Plague > 0f).Select(s => GreatEventText.Format("{0} {1:0%}", s.Name, s.Plague)).ToList();
        lines.Add(GreatEventText.Format(
            "Great events: {0}; next eclipse {1}; sick: {2}",
            state.Events.Count == 0 ? "none" : string.Join(", ", state.Events.Select(e => GreatEventText.Describe(e, world))),
            state.NextEclipseTick > 0 ? "in " + GreatEventText.Clock(Math.Max(0L, state.NextEclipseTick - world.Tick)) : "not set",
            sick.Count == 0 ? "none" : string.Join(", ", sick)));
        lines.Add("  faiths: " + string.Join("   ", world.Gods.Select(g =>
        {
            var (doubt, learning, villages) = ReformationModel.Measure(world, g.Id);
            return GreatEventText.Format("{0} doubt {1:0.00} learning {2:0.00} in {3}", g.Name, doubt, learning, GreatEventText.Count(villages, "village"));
        })));
    }

    /// <summary>A village's plague and immunity, and what shields it.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>One line, or <c>null</c> when the plague has never touched it.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settlement);
        if (GreatEventText.Village(settlement) is not { } plague)
        {
            return null;
        }

        var rules = snapshot.DisasterRules?.GreatEvents.Plague;
        return "  plague: " + plague + (rules is null ? string.Empty : GreatEventText.Format(", shielded {0:0%}", PlagueModel.Shield(rules, settlement)));
    }
}
