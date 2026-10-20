using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.World;
using Pantheon.Game.Diplomacy;
using Pantheon.Simulation.Diplomacy;

namespace Pantheon.Game.Minds;

/// <summary>
/// How a god stands towards another, in its own words, for its mind (ADR-066, ADR-072): "you distrust
/// Ilyra and see it as a rival", "Ilyra struck your followers in Elar". The diplomacy panel's words are the
/// other way about, said of the god to the player.
/// </summary>
public static class MindWords
{
    /// <summary>How the god feels about the other.</summary>
    /// <param name="relation">The god's view of the other, or <c>null</c> when it has none yet.</param>
    /// <param name="other">The other god's name.</param>
    /// <returns>For example "you distrust Ilyra and see it as a rival".</returns>
    public static string Feeling(GodRelation? relation, string other)
    {
        if (relation is null)
        {
            return "you have formed no view of " + other + " yet";
        }

        var parts = new List<string> { relation.Trust >= 0.65f ? "you trust " + other : relation.Trust <= 0.35f ? "you distrust " + other : "you are unsure of " + other };
        if (relation.Rivalry >= 0.5f)
        {
            parts.Add("see it as a rival");
        }

        if (relation.Fear >= 0.4f)
        {
            parts.Add("fear its power");
        }

        if (relation.Gratitude >= 0.3f)
        {
            parts.Add("are grateful to it");
        }

        if (relation.Suspicion >= 0.5f)
        {
            parts.Add("suspect its motives");
        }

        return string.Join(", ", parts.Take(parts.Count - 1)) + (parts.Count > 1 ? " and " : string.Empty) + parts[^1];
    }

    /// <summary>What the god remembers of the other, its weightiest memories first, good and bad.</summary>
    /// <param name="relation">The god's view of the other, or <c>null</c>.</param>
    /// <param name="world">World, for village names.</param>
    /// <param name="other">The other god's name.</param>
    /// <returns>For example "Ilyra struck your followers in Elar; helped your followers"; empty when it remembers nothing.</returns>
    public static string Remembered(GodRelation? relation, WorldState world, string other)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (relation is null || relation.Memories.Count == 0)
        {
            return string.Empty;
        }

        // The panel's words are said to the player of the god: "you" is the other, "it" the god. Here the
        // god says them of the other, so the two swap: "you let your Creature attack its followers" becomes
        // "Ilyra let its Creature attack your followers".
        var told = DiplomacyText.Explain(relation, world)["remembers that you ".Length..];
        var swapped = Regex.Replace(told, "\\byour\\b", "\u0001");
        swapped = Regex.Replace(swapped, "\\bits\\b", "your");
        swapped = Regex.Replace(swapped, "\\bit\\b", "you");
        return other + " " + swapped.Replace("\u0001", "its", StringComparison.Ordinal);
    }

    /// <summary>The god's feeling and memories of the other in a line.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="otherId">The other god.</param>
    /// <param name="other">The other god's name.</param>
    /// <returns>For example "you distrust Ilyra; Ilyra declared war on you".</returns>
    public static string Stance(WorldState world, int godId, int otherId, string other)
    {
        var view = DiplomacyModel.FindRelation(world, godId, otherId);
        return Remembered(view, world, other) is { Length: > 0 } memories ? Feeling(view, other) + "; " + memories : Feeling(view, other);
    }
}
