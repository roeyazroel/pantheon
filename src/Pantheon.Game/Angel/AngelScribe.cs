using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Game.Angel;

/// <summary>
/// The angel's words when no model speaks for it (ADR-074): a line for the moment in its voice, chosen
/// from the seed and the tick, with its names filled in, and the deeds its nature urges: a kind heart's
/// kind deeds, a dark heart's harsh ones, and a torn angel's one of each. Pure.
/// </summary>
public static class AngelScribe
{
    /// <summary>The angel's line for a moment.</summary>
    /// <param name="content">The angel's content.</param>
    /// <param name="look">How it looks now: its voice.</param>
    /// <param name="hook">The moment.</param>
    /// <param name="world">The world, for names and the seed.</param>
    /// <param name="god">Its god.</param>
    /// <param name="name">The angel's name.</param>
    /// <returns>The line.</returns>
    public static string Line(AngelContent content, AngelLook look, AngelHook hook, WorldState world, GodState god, string name)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(hook);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        var lines = content.LinesFor(hook.Moment, look.Tone);
        var line = lines[(int)(SeedMixer.Derive(world.Seed, "angel-line", (hook.Tick * 31) + (int)hook.Moment) % (ulong)lines.Count)];
        var village = world.Settlements.Find(s => s.Id == hook.SettlementId)?.Name
            ?? Simulation.Gods.GodPlanner.Home(world, god.Id)?.Name ?? "your village";
        return line
            .Replace("{name}", name, StringComparison.Ordinal)
            .Replace("{god}", god.Name, StringComparison.Ordinal)
            .Replace("{village}", village, StringComparison.Ordinal)
            .Replace("{other}", world.Gods.Find(g => g.Id == hook.GodId)?.Name ?? "a rival", StringComparison.Ordinal)
            .Replace("{creature}", world.Creatures.Find(c => c.Id == god.CreatureId)?.Name ?? "your Creature", StringComparison.Ordinal)
            .Replace("{visitor}", world.Creatures.Find(c => c.Id == hook.CreatureId)?.Name ?? "the stranger", StringComparison.Ordinal)
            .Replace("{detail}", hook.Detail.Length > 0 ? hook.Detail : "it", StringComparison.Ordinal);
    }

    /// <summary>The deeds the angel's nature urges, of those it may: at most two.</summary>
    /// <param name="choices">The deeds it may urge, the most pressing first.</param>
    /// <param name="look">How it looks now.</param>
    /// <returns>The deeds urged.</returns>
    public static List<AngelSuggestion> Pick(IReadOnlyList<AngelSuggestion> choices, AngelLook look)
    {
        ArgumentNullException.ThrowIfNull(choices);
        AngelSuggestion? First(Func<AngelSuggestion, bool> wanted) => choices.FirstOrDefault(wanted);
        AngelSuggestion?[] picked = look.Tone switch
        {
            AngelTone.Light => [First(c => c.Kindness > 0) ?? First(c => c.Kindness == 0)],
            AngelTone.Dark => [First(c => c.Kindness < 0) ?? First(c => c.Kindness == 0)],
            AngelTone.Torn => [First(c => c.Kindness > 0), First(c => c.Kindness < 0)],
            AngelTone.Silver => [First(c => c.Kindness >= 0)],
        };
        return picked.OfType<AngelSuggestion>().Take(AngelPrompt.MaxUrged).ToList();
    }
}
