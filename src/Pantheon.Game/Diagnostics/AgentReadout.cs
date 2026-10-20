using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game.Minds.Agents;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// The agents in the debug view (ADR-072): who thinks for the gods and Creatures, the questions asked this
/// hour and the tokens spent, and for each mind what woke it last, why it acted, what came of it, what it
/// said, and a god's journal.
/// </summary>
public static class AgentReadout
{
    // The longest a journal is shown, in characters.
    private const int JournalShown = 220;

    /// <summary>The agents' lines.</summary>
    /// <param name="agents">The agents.</param>
    /// <param name="world">World state, for names and journals.</param>
    /// <param name="model">The model's name, or <c>null</c> when the scribes speak.</param>
    /// <param name="limit">The most questions an hour.</param>
    /// <returns>The lines.</returns>
    public static IReadOnlyList<string> Lines(AgentDirector agents, WorldState world, string? model, int limit)
    {
        ArgumentNullException.ThrowIfNull(agents);
        ArgumentNullException.ThrowIfNull(world);
        var lines = new List<string>
        {
            Format("Minds: {0}{1}; {2}/{3} questions this hour; tokens {4} read, {5} written{6}", model ?? "the scribes", agents.Resting ? " (resting after failures)" : string.Empty,
                agents.AskedThisHour, limit, agents.Tokens.Read, agents.Tokens.Written, agents.Problem is { } problem ? "; last problem: " + problem : string.Empty),
        };
        foreach (var mind in agents.Minds.Where(m => m.Turns > 0 || m.Thinking || m.LastSaid.Length > 0).OrderBy(m => m.Mind.Kind).ThenBy(m => m.Mind.Id))
        {
            var name = mind.Mind.Kind == MindKind.God ? world.Gods.Find(g => g.Id == mind.Mind.Id)?.Name : world.Creatures.Find(c => c.Id == mind.Mind.Id)?.Name;
            var state = mind.Thinking ? "thinking" : mind.Waiting > 0 ? Format("{0} waiting", mind.Waiting) : "idle";
            lines.Add(Format("  {0} ({1}): {2}, {3} turns{4}{5}", name ?? "?", mind.Mind.Kind, state, mind.Turns,
                mind.LastHooks.Count > 0 ? "; woke for: " + string.Join(" / ", mind.LastHooks.Take(2)) : string.Empty,
                mind.LastProblem is { } failed ? "; unanswered: " + failed : string.Empty));
            if (mind.LastReason.Length > 0 || mind.LastResults.Count > 0)
            {
                lines.Add(Format("    {0}{1}", mind.LastReason.Length > 0 ? "why: " + mind.LastReason + "  " : string.Empty, mind.LastResults.Count > 0 ? "did: " + string.Join("; ", mind.LastResults) : string.Empty));
            }

            if (mind.LastSaid.Length > 0)
            {
                lines.Add("    said: " + mind.LastSaid);
            }

            if (mind.Mind.Kind == MindKind.God && world.Gods.Find(g => g.Id == mind.Mind.Id)?.Journal is { Length: > 0 } journal)
            {
                lines.Add("    keeps in mind: " + (journal.Length > JournalShown ? journal[..JournalShown] + "…" : journal));
            }
        }

        return lines;
    }

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
