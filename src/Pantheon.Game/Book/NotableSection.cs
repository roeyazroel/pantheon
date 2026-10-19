using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;
using Pantheon.Game.Notables;

namespace Pantheon.Game.Book;

/// <summary>The book's lines on prophets and elders (spec §80, ADR-032), for the World and Religion pages. Pure.</summary>
public static class NotableSection
{
    /// <summary>Every prophet and how many villages each god's elders keep, for the World page.</summary>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god, whose prophets are marked.</param>
    /// <returns>The lines.</returns>
    public static IEnumerable<BookLine> World(WorldState world, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var prophets = world.Notables.Where(n => n.Kind == NotableKind.Prophet).OrderBy(n => n.GodId).ThenBy(n => n.Id).ToList();
        if (prophets.Count == 0)
        {
            yield return new BookLine("No prophet walks the island.", BookTone.Muted);
        }

        foreach (var prophet in prophets)
        {
            yield return new BookLine(Format("{0}, {1}", NotableText.Title(prophet, world), NotableText.Doing(prophet, world)), prophet.GodId == localGodId ? BookTone.Good : BookTone.Plain);
        }

        var elders = world.Notables.Where(n => n.Kind == NotableKind.Elder).ToList();
        if (elders.Count > 0)
        {
            yield return new BookLine("Elders keep " + string.Join(", ", world.Gods.Where(g => elders.Exists(e => e.GodId == g.Id))
                .Select(g => Format("{0} village{1} to {2}", elders.Count(e => e.GodId == g.Id), elders.Count(e => e.GodId == g.Id) == 1 ? string.Empty : "s", g.Name))) + ".");
        }
    }

    /// <summary>The player's own prophets with what they remember, and their elders, for the Religion page.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The player's god.</param>
    /// <returns>The lines.</returns>
    public static IEnumerable<BookLine> Yours(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        var prophets = world.Notables.Where(n => n.Kind == NotableKind.Prophet && n.GodId == godId).OrderBy(n => n.Id).ToList();
        if (prophets.Count == 0)
        {
            yield return new BookLine("No prophet walks in your name. A village devout to you may call one, or you may anoint one.", BookTone.Muted);
        }

        foreach (var prophet in prophets)
        {
            yield return new BookLine(Format("{0}, {1}", NotableText.Title(prophet, world), NotableText.Doing(prophet, world)), BookTone.Good);
            foreach (var memory in prophet.Memories.AsEnumerable().Reverse())
            {
                yield return new BookLine("  " + NotableText.Memory(memory, world), BookTone.Muted);
            }
        }

        var elders = world.Notables.Where(n => n.Kind == NotableKind.Elder && n.GodId == godId).Select(n => NotableText.Village(world, n.SettlementId) + " (" + n.Name + ")").ToList();
        yield return elders.Count == 0
            ? new BookLine("No elder keeps a village to you.", BookTone.Muted)
            : new BookLine("Your elders keep " + string.Join(", ", elders) + ".");
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
