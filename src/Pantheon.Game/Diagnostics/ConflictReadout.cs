using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Simulation.Conflict;

namespace Pantheon.Game.Diagnostics;

/// <summary>The debug panel's lines about wars, holy wars and what each god knows of the others (ADR-026).</summary>
internal static class ConflictReadout
{
    /// <summary>Adds the conflict lines.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="rules">Conflict rules, for intelligence.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot, Domain.Conflict.ConflictRules? rules)
    {
        var world = snapshot.World;
        var wars = world.Diplomacy.Wars.Select(w => Format("{0}>{1} {2}{3} {4:0.0}-{5:0.0}", w.AggressorGodId, w.DefenderGodId, w.Goal, w.SiteId is { } site ? Format("({0})", site) : string.Empty, w.AggressorScore, w.DefenderScore));
        var holy = world.Settlements.Where(s => s.Society.HolyWar is not null)
            .Select(s => Format("{0}>{1} god {2} {3} killed {4} converted {5}", s.Id, s.Society.HolyWar!.DefenderSettlementId, s.Society.HolyWar.GodId, s.Society.HolyWar.Stance, s.Society.HolyWar.Killed, s.Society.HolyWar.Converted));
        lines.Add("Conflict: wars " + Join(wars) + "   holy wars " + Join(holy));
        if (rules is null)
        {
            return;
        }

        var intel = world.Gods.SelectMany(a => world.Gods.Where(b => b.Id != a.Id).Select(b => Format("{0}>{1} {2:0.00}", a.Id, b.Id, IntelModel.Level(rules, world, a.Id, b.Id))));
        lines.Add("  intel " + Join(intel));
    }

    /// <summary>Items joined, or "none".</summary>
    private static string Join(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count == 0 ? "none" : string.Join(", ", list);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
