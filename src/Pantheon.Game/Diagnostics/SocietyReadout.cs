using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;

namespace Pantheon.Game.Diagnostics;

/// <summary>The debug overlay's lines about a settlement's society (ADR-023).</summary>
public static class SocietyReadout
{
    /// <summary>The society of a settlement: its values, priority, work, technology and culture.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Three lines.</returns>
    public static IReadOnlyList<string> Describe(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var society = settlement.Society;
        var buildings = society.Buildings.Count == 0 ? "none" : string.Join(",", society.Buildings.Select(b => b.GodId is { } god ? Format("temple({0})", god) : b.Kind.ToString()));
        return
        [
            Format(
                "  society: safety {0:0.00} happiness {1:0.00} stability {2:0.00} wealth {3:0.0} education {4:0.00}   priority {5}{6}   project {7} {8:0}   buildings {9}",
                society.Safety, society.Happiness, society.Stability, society.Wealth, society.Education, society.Priority,
                society.PriorityGodId is { } setter ? Format(" (god {0})", setter) : string.Empty, society.Project?.ToString() ?? "none", society.ProjectProgress, buildings),
            "  technology: " + string.Join(" ", Enum.GetValues<TechnologyField>().Select(f => Format("{0} {1:0.00}", f, society.Technology.Get(f)))),
            Culture.CultureReadout.Debug(settlement),
        ];
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
