using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Notables;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Notables;

namespace Pantheon.Game.Diagnostics;

/// <summary>The prophets' and elders' lines of the F1 debug panel (spec §80, ADR-032). Pure.</summary>
public static class NotableReadout
{
    /// <summary>Most prophets listed.</summary>
    public const int ListedProphets = 6;

    /// <summary>Adds the count of notables and every prophet's walk, last deed and years left.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        var world = snapshot.World;
        var prophets = world.Notables.Where(n => n.Kind == NotableKind.Prophet).ToList();
        var elders = world.Notables.Count(n => n.Kind == NotableKind.Elder);
        if (prophets.Count == 0 && elders == 0)
        {
            return;
        }

        lines.Add(Format(
            "Notables: {0} ({1}), {2}",
            Count(prophets.Count, "prophet"),
            prophets.Count == 0 ? "none walk" : string.Join(", ", world.Gods.Where(g => prophets.Exists(p => p.GodId == g.Id)).Select(g => g.Name + " " + prophets.Count(p => p.GodId == g.Id))),
            Count(elders, "elder")));
        foreach (var prophet in prophets.Take(ListedProphets))
        {
            lines.Add(Line(prophet, world));
        }
    }

    /// <summary>A village's elder and the prophets in it.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>One line, or <c>null</c> when it has neither.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settlement);
        var world = snapshot.World;
        var elder = world.Notables.Find(n => n.Kind == NotableKind.Elder && n.SettlementId == settlement.Id);
        var prophets = world.Notables.Where(n => n.Kind == NotableKind.Prophet && n.SettlementId == settlement.Id && n.TargetSettlementId is null).ToList();
        if (elder is null && prophets.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (elder is not null)
        {
            parts.Add(Format("elder {0}, of {1}, {2} left", elder.Name, NotableText.God(world, elder.GodId), Left(elder, world)));
        }

        if (prophets.Count > 0)
        {
            parts.Add("preaching: " + string.Join(", ", prophets.Select(p => Format("{0} of {1}", p.Name, NotableText.God(world, p.GodId)))));
        }

        return "  notables: " + string.Join("; ", parts);
    }

    /// <summary>One prophet's line.</summary>
    private static string Line(NotableState prophet, WorldState world) =>
        Format(
            "  #{0} {1}: {2}, {3} left{4}",
            prophet.Id,
            NotableText.Title(prophet, world),
            NotableText.Doing(prophet, world),
            Left(prophet, world),
            prophet.Memories.Count > 0 ? "; " + NotableText.Memory(prophet.Memories[^1], world) : string.Empty);

    /// <summary>The game time a notable has left, as minutes and seconds.</summary>
    private static string Left(NotableState notable, WorldState world)
    {
        var seconds = Math.Max(0L, notable.DiesAtTick - world.Tick) / SimulationTime.TicksPerSecond;
        return Format("{0}:{1:00}", seconds / 60, seconds % 60);
    }

    /// <summary>A count with its noun, singular for one.</summary>
    private static string Count(int count, string noun) => Format("{0} {1}{2}", count, noun, count == 1 ? string.Empty : "s");

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
