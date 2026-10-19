using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Game.Divine;
using Pantheon.Game.Domains;
using Pantheon.Simulation.Domains;
using Pantheon.Simulation.HolySites;
using Pantheon.Simulation.Miracles;

namespace Pantheon.Game.Book;

/// <summary>
/// The book's Miracles page (spec §57): every miracle in the book, by kind, with the rank it needs,
/// what it costs the player, its reach and any cooldown, and whether it may be aimed now. Pure.
/// </summary>
public static class MiraclesPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The page.</returns>
    public static BookPage Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var lines = new List<BookLine>();
        if (session.LocalGod is not { } god)
        {
            return new BookPage(BookTab.Miracles, "Miracles", [new("You have no god.", BookTone.Muted)]);
        }

        foreach (var (title, include) in new (string, Func<MiracleDefinition, bool>)[]
        {
            ("Miracles", m => !m.RequiresDomain && m.Scale != MiracleScale.DivineAct),
            ("Signature miracles of the domains", m => m.RequiresDomain),
            ("Divine Acts", m => m.Scale == MiracleScale.DivineAct),
        })
        {
            lines.Add(new(title, BookTone.Heading));
            foreach (var miracle in session.Content.Miracles.Miracles)
            {
                if (include(miracle))
                {
                    lines.Add(Line(session, god, miracle));
                }
            }
        }

        return new BookPage(BookTab.Miracles, "Miracles", lines);
    }

    /// <summary>One miracle's line, and whether it may be aimed now.</summary>
    private static BookLine Line(GameSession session, GodState god, MiracleDefinition miracle)
    {
        var content = session.Content;
        var cost = miracle.PowerCost * DomainModel.CostFactor(content.Domains, god, miracle) * HolySiteModel.CostFactor(content.HolySites, session.World, god.Id);
        var waiting = MiracleCaster.CooldownTicksLeft(session.World, god, miracle.Id) / session.Scheduler.TicksPerSecond;
        var lacks = god.Rank < miracle.MinimumRank ? "needs a " + DivineText.Rank(miracle.MinimumRank)
            : miracle.RequiresDomain && !DomainModel.Holds(god, miracle.Domain) ? "needs " + DomainText.InSentence(miracle.Domain)
            : waiting > 0 ? "ready in " + DivineText.Clock(waiting)
            : god.DivinePower < cost ? "needs more power"
            : null;
        var text = Format("{0} ({1}, {2:0} power, {3:0} m): {4}{5}", miracle.Name, Scale(miracle.Scale), cost, miracle.Radius, miracle.Description, lacks is null ? string.Empty : " [" + lacks + "]");
        return new BookLine(text, lacks is null ? BookTone.Plain : BookTone.Muted, miracle.Id, lacks is null);
    }

    /// <summary>A miracle's scale in words.</summary>
    private static string Scale(MiracleScale scale) => scale switch
    {
        MiracleScale.Minor => "minor",
        MiracleScale.Regional => "regional",
        MiracleScale.Great => "great",
        MiracleScale.DivineAct => "Divine Act",
    };

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
