using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.HolySites;

namespace Pantheon.Game.Domains;

/// <summary>One domain in the Domains panel.</summary>
/// <param name="Domain">The domain.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">What it stands for.</param>
/// <param name="Boon">What its boon does.</param>
/// <param name="Signature">Its signature miracle, with the rank it needs.</param>
/// <param name="Held">Whether the player's god holds it.</param>
/// <param name="CanChoose">Whether the player's god may take it up now.</param>
public sealed record DomainEntry(GodDomain Domain, string Name, string Description, string Boon, string Signature, bool Held, bool CanChoose);

/// <summary>One holy site in the Domains panel.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Description">What keeping it gives.</param>
/// <param name="State">Who keeps it and who is taking it.</param>
/// <param name="Yours">Whether the player's god keeps it.</param>
/// <param name="Position">Where it stands.</param>
public sealed record HolySiteEntry(string Name, string Description, string State, bool Yours, GroundPosition Position);

/// <summary>The Domains panel (spec §57's Domains panel, ADR-021).</summary>
/// <param name="Slots">How many domains the god holds and may hold.</param>
/// <param name="Domains">Every domain, the god's first.</param>
/// <param name="Sites">The world's holy sites.</param>
public sealed record DomainPanelModel(string Slots, IReadOnlyList<DomainEntry> Domains, IReadOnlyList<HolySiteEntry> Sites)
{
    /// <summary>The Divine Acts (ADR-030), dearest of all miracles.</summary>
    public IReadOnlyList<ActEntry> Acts { get; init; } = [];
}

/// <summary>A Divine Act in the Domains panel (spec §14, ADR-030).</summary>
/// <param name="MiracleId">The act's miracle.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">What it does.</param>
/// <param name="Requirement">The rank it needs, its cost and any cooldown.</param>
/// <param name="CanChoose">Whether the god may cast it now.</param>
public sealed record ActEntry(string MiracleId, string Name, string Description, string Requirement, bool CanChoose);

/// <summary>Builds the Domains panel from the session. Pure.</summary>
public static class DomainReadout
{
    /// <summary>The panel.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The model; empty lists without a god.</returns>
    public static DomainPanelModel Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var content = session.Content;
        var world = session.World;
        if (session.LocalGod is not { } god)
        {
            return new DomainPanelModel("You have no god", [], []);
        }

        var rules = content.Domains;
        var held = new List<DomainEntry>();
        var others = new List<DomainEntry>();
        var free = DomainModel.FreeSlots(rules, god) > 0;
        foreach (var definition in rules.Domains)
        {
            var holds = god.Domains.Contains(definition.Domain);
            var entry = new DomainEntry(definition.Domain, DomainText.Name(definition.Domain), definition.Description, definition.Boon, Signature(content, definition.Domain), holds, free && !holds);
            (holds ? held : others).Add(entry);
        }

        var sites = world.HolySites.ConvertAll(site => new HolySiteEntry(
            site.Name,
            HolySiteModel.Find(content.HolySites, site.Kind)?.Description ?? string.Empty,
            DomainText.SiteState(world, site, content.HolySites.ClaimSeconds),
            site.KeeperGodId == god.Id,
            site.Position));
        return new DomainPanelModel(DescribeSlots(rules, god), [.. held, .. others], sites) { Acts = Acts(session, god) };
    }

    /// <summary>The Divine Acts and whether the god may cast each now.</summary>
    private static List<ActEntry> Acts(GameSession session, GodState god)
    {
        var acts = new List<ActEntry>();
        var ticksPerSecond = session.Scheduler.TicksPerSecond;
        foreach (var act in session.Content.Miracles.Miracles)
        {
            if (act.Scale != Domain.Miracles.MiracleScale.DivineAct)
            {
                continue;
            }

            var cost = act.PowerCost * DomainModel.CostFactor(session.Content.Domains, god, act);
            var waiting = Simulation.Miracles.MiracleCaster.CooldownTicksLeft(session.World, god, act.Id) / ticksPerSecond;
            var requirement = Format("{0}, {1:0} power{2}", DivineText.Rank(act.MinimumRank), cost, waiting > 0 ? Format(", ready in {0}", DivineText.Clock(waiting)) : string.Empty);
            acts.Add(new ActEntry(act.Id, act.Name, act.Description, requirement, god.Rank >= act.MinimumRank && god.DivinePower >= cost && waiting <= 0));
        }

        return acts;
    }

    /// <summary>How many domains the god holds, may hold, and may hold at its next rank that offers more.</summary>
    private static string DescribeSlots(DomainRules rules, GodState god)
    {
        var slots = DomainModel.SlotsFor(rules, god.Rank);
        var text = Format("{0} holds {1} of {2} domain{3}.", god.Name, god.Domains.Count, slots, slots == 1 ? string.Empty : "s");
        foreach (var slot in rules.Slots)
        {
            if (slot.Rank > god.Rank && slot.Domains > slots)
            {
                return text + Format(" A {0} may hold {1}.", DivineText.Rank(slot.Rank), slot.Domains);
            }
        }

        return text;
    }

    /// <summary>The domain's signature miracles, with the rank each needs.</summary>
    private static string Signature(GameContent content, GodDomain domain)
    {
        var names = new List<string>();
        foreach (var miracle in content.Miracles.Miracles)
        {
            if (miracle.RequiresDomain && miracle.Domain == domain)
            {
                names.Add(Format("{0} ({1}, {2:0} power)", miracle.Name, DivineText.Rank(miracle.MinimumRank), miracle.PowerCost * content.Domains.DomainCostFactor));
            }
        }

        return names.Count == 0 ? "No signature miracle" : "Signature: " + string.Join(", ", names);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
