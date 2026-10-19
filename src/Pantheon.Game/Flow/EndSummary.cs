using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Game.Divine;
using Pantheon.Game.History;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Flow;

/// <summary>The end-of-game screen (spec §53, §115): who won, how the player's god ended up, and the history that led there.</summary>
/// <param name="Won">Whether the player's god is among the winners.</param>
/// <param name="Title">"Victory" or "Defeat".</param>
/// <param name="Subtitle">Who won.</param>
/// <param name="Facts">How the player's god, its Creature and the world ended up.</param>
/// <param name="Highlights">The latest moments of the world's history, those concerning the player first.</param>
public sealed record EndSummary(bool Won, string Title, string Subtitle, IReadOnlyList<string> Facts, IReadOnlyList<string> Highlights);

/// <summary>Builds the end-of-game screen from a session whose world has winners (ADR-020). Pure.</summary>
public static class EndSummaryBuilder
{
    /// <summary>Most history lines shown.</summary>
    public const int HighlightsShown = 6;

    /// <summary>The end screen, or <c>null</c> while nobody has won.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The summary.</returns>
    public static EndSummary? Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        if (world.WinnerGodIds.Count == 0)
        {
            return null;
        }

        var me = session.LocalGodId;
        var won = me is { } id && world.WinnerGodIds.Contains(id);
        var names = DivineText.Names(world, world.WinnerGodIds, me);
        var deed = VictoryText.Deed(VictoryText.KindOf(world));
        var subtitle = won
            ? (world.WinnerGodIds.Count > 1 ? Format("{0} have {1} together.", names, deed) : Format("You have {0}.", deed))
            : Format("{0} {1} {2}.", names, world.WinnerGodIds.Count > 1 ? "have" : "has", deed);
        var rules = session.Content.History;
        var facts = new List<string> { Format("Year {0} of the world.", HistoryText.Year(world.Tick, rules)) };
        if (session.LocalGod is { } god)
        {
            facts.Add(Format(
                "{0} ends a {1}, followed by {2} people: {3:0%} of all belief.",
                god.Name,
                DivineText.Rank(god.Rank),
                GodStanding.CountFollowers(world, god.Id),
                Simulation.Belief.VictorySystem.ShareOf(world, god.Id)));
        }

        if (session.LocalCreature is { } creature)
        {
            facts.Add(Format(
                "{0} reached level {1}, with combat skill {2:0%}{3}.",
                creature.Name,
                creature.Level,
                creature.CombatSkill,
                creature.Scars switch { 0 => " and no scars", 1 => " and 1 scar", _ => Format(" and {0} scars", creature.Scars) }));
        }

        var history = world.History;
        facts.Add(Format("The world remembers {0} moments; {1} concern you.", history.Count, history.Count(r => HistoryText.Concerns(r, world, me))));
        var highlights = history.AsEnumerable().Reverse()
            .OrderByDescending(r => HistoryText.Concerns(r, world, me))
            .Take(HighlightsShown)
            .OrderBy(r => r.Tick)
            .Select(r => HistoryText.Line(r, world, rules))
            .ToList();
        return new EndSummary(won, won ? "Victory" : "Defeat", subtitle, facts, highlights);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
