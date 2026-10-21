using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Game.Angel;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// The player's angel in the debug view (ADR-074): its name, its look and what made it (the kindness
/// and harm in its god's conduct), its marks, who speaks for it and the questions asked, the moments
/// waiting, and its last words with the deeds it urged.
/// </summary>
public static class AngelReadout
{
    /// <summary>The angel's lines.</summary>
    /// <param name="angel">The angel.</param>
    /// <param name="god">Its god, if the player has one.</param>
    /// <param name="model">The model's name, or <c>null</c> when the scribes speak.</param>
    /// <returns>The lines; none without a god.</returns>
    public static IReadOnlyList<string> Lines(AngelDirector angel, GodState? god, string? model)
    {
        ArgumentNullException.ThrowIfNull(angel);
        if (god is null)
        {
            return [];
        }

        var look = angel.Look;
        var (kind, harm) = AngelLooks.Weigh(god.Conduct, angel.Content.Weights);
        var timing = angel.Content.Timing;
        var lines = new List<string>
        {
            Format("Angel: {0}, {1}{2}: lean {3:+0.00;-0.00}, formed {4:0%}, torn {5:0%} (kindness {6:0.0}, harm {7:0.0}); marks {8}",
                angel.Name, angel.Content.Stage(look.Stage).Name, angel.ForcedStage is null ? string.Empty : " (held)", look.Lean, look.Formed, look.Conflict, kind, harm, look.Marks),
            Format("  speaks by {0}{1}; {2}/{3} questions this hour, {4} in all; tokens {5} read, {6} written{7}",
                model ?? "the scribes", angel.Resting ? " (resting after failures)" : angel.Thinking ? " (thinking)" : string.Empty, angel.AskedThisHour, timing.TurnsPerHour, angel.Turns,
                angel.Tokens.Read, angel.Tokens.Written, angel.Problem is { } problem ? "; last problem: " + problem : string.Empty),
        };
        if (angel.Waiting.Count > 0)
        {
            lines.Add("  waiting: " + string.Join(" / ", angel.Waiting.Select(h => h.Moment.ToString())));
        }

        if (angel.Last is { } last)
        {
            lines.Add(Format("  said ({0}, of {1}): {2}", last.FromModel ? "its model" : "the scribes", last.Hook.Moment, last.Text));
            if (last.Suggestions.Count > 0)
            {
                lines.Add("  urged: " + string.Join("; ", last.Suggestions.Select(s => s.Text)));
            }
        }

        return lines;
    }

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
