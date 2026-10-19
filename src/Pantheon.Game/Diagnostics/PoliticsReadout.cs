using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.World;

namespace Pantheon.Game.Diagnostics;

/// <summary>The debug panel's lines about treaties, favours, promises and the Divine Council (ADR-025): everything, secrets included.</summary>
internal static class PoliticsReadout
{
    /// <summary>Adds the politics lines.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        var world = snapshot.World;
        var diplomacy = world.Diplomacy;
        var treaties = diplomacy.Treaties.Select(t => Format(
            "#{0} {1} {2}-{3}{4}{5}{6} {7}",
            t.Id, t.Kind, t.FirstGodId, t.SecondGodId, t.TargetGodId is { } target ? Format(" vs {0}", target) : string.Empty,
            t.PayerGodId is { } payer ? Format(" paid by {0} {1:0}/min", payer, t.Amount) : string.Empty, t.Secret ? " SECRET" : string.Empty,
            t.EndsTick is { } end ? Format("{0:0}s", (end - world.Tick) / (float)SimulationTime.TicksPerSecond) : "open"));
        lines.Add("Politics: treaties " + (diplomacy.Treaties.Count == 0 ? "none" : string.Join("; ", treaties)));
        var favours = diplomacy.Favours.Select(f => Format("{0} owes {1} x{2:0}", f.DebtorGodId, f.CreditorGodId, f.Weight));
        var promises = diplomacy.Obligations.Select(o => Format("{0}->{1} {2} {3:0}s", o.DebtorGodId, o.CreditorGodId, o.Kind, (o.DueTick - world.Tick) / (float)SimulationTime.TicksPerSecond));
        lines.Add("  favours " + Join(favours) + "   promises " + Join(promises));
        var council = diplomacy.Council;
        var session = council.Session is { } open
            ? Format("{0} target {1} by {2} ({3}) closes {4:0}s votes {5}", open.Motion.Kind, open.Motion.TargetGodId?.ToString(CultureInfo.InvariantCulture) ?? "-", open.Motion.ProposerGodId?.ToString(CultureInfo.InvariantCulture) ?? "event",
                open.Trigger, (open.ClosesTick - world.Tick) / (float)SimulationTime.TicksPerSecond, Join(open.Votes.Select(v => Format("{0}:{1}", v.GodId, v.Choice))))
            : "not sitting";
        lines.Add(Format(
            "  council {0}   decrees {1}   tabled {2}   next {3:0}s",
            session,
            Join(council.Decrees.Select(d => Format("{0}({1}) {2:0}s", d.Motion.Kind, d.Motion.TargetGodId?.ToString(CultureInfo.InvariantCulture) ?? d.Motion.SiteId?.ToString(CultureInfo.InvariantCulture) ?? d.Motion.Practice?.ToString() ?? d.Motion.SettlementId?.ToString(CultureInfo.InvariantCulture) ?? "-", (d.EndsTick - world.Tick) / (float)SimulationTime.TicksPerSecond))),
            Join(council.Tabled.Select(m => Format("{0} by {1}", m.Kind, m.ProposerGodId))),
            (council.NextSessionTick - world.Tick) / (float)SimulationTime.TicksPerSecond));
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
