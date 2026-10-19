using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Domains;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.HolySites;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// The debug view of domains, holy sites and schisms (spec §106, ADR-021): every god's domains,
/// every site with its keeper, its claimant and who leads its influence, and a settlement's Old
/// Faith and sanctuary.
/// </summary>
internal static class FaithReadout
{
    /// <summary>Adds the gods' domains and the holy sites.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        var world = snapshot.World;
        var domains = new List<string>();
        foreach (var god in world.Gods)
        {
            domains.Add(god.Name + ": " + DomainText.List(god));
        }

        if (domains.Count > 0)
        {
            lines.Add("Domains  " + string.Join("  ", domains));
        }

        if (snapshot.HolySiteRules is not { } rules || world.HolySites.Count == 0)
        {
            return;
        }

        var fields = world.Gods.ConvertAll(god => InfluenceField.Build(snapshot.DivineRules, world, god.Id));
        foreach (var site in world.HolySites)
        {
            var (leader, influence, lead) = HolySiteSystem.Leader(world, site, fields);
            var leading = leader is { } id ? world.Gods.Find(g => g.Id == id)?.Name ?? "?" : "nobody";
            lines.Add(Format(
                "Site {0}: {1}; leads {2} {3:0%} (+{4:0%}; needs {5:0%}, +{6:0%})",
                site.Name,
                DomainText.SiteState(world, site, rules.ClaimSeconds),
                leading,
                influence,
                lead,
                rules.ClaimInfluence,
                rules.ClaimLead));
        }
    }

    /// <summary>A settlement's Old Faith and sanctuary, if it has either.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>One line, or <c>null</c>.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        var world = snapshot.World;
        var parts = new List<string>();
        if (settlement.OldFaith is { Share: > 0f } old)
        {
            parts.Add(Format(
                "Old Faith of {0}: {1:0%} of the people ({2:0} returned, {3:0} lost)",
                world.Gods.Find(g => g.Id == old.GodId)?.Name ?? "?",
                old.Share,
                old.Returned * settlement.Population,
                old.Faded * settlement.Population));
        }

        if (settlement.SanctuaryGodId is { } sheltered && Schisms.IsSanctuary(settlement, sheltered, world.Tick))
        {
            parts.Add(Format("Sanctuary of {0} for {1:0} s", world.Gods.Find(g => g.Id == sheltered)?.Name ?? "?", (settlement.SanctuaryUntilTick - world.Tick) / snapshot.TicksPerSecond));
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
