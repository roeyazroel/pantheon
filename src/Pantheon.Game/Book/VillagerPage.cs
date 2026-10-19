using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Villagers;
using Pantheon.Game.Villagers;
using Pantheon.Simulation.Villagers;

namespace Pantheon.Game.Book;

/// <summary>
/// The book's Villager page (spec §80, ADR-043): the villager the player follows: who they are, what
/// they do and why, and the life they remember, each memory with the age they were. Pure.
/// </summary>
public static class VillagerPage
{
    /// <summary>Builds the page.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The page.</returns>
    public static BookPage Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var world = session.World;
        if (VillagerCommands.Of(world, session.LocalGodId) is not { } villager)
        {
            return new BookPage(BookTab.Villager, "Your villager",
            [
                new("You follow no villager.", BookTone.Muted),
                new("Point at someone in a village and press / to follow them. They become one of the world's individuals, and this page tells their life."),
            ]);
        }

        var rules = session.Content.SettlementRules.Villagers;
        var lines = new List<BookLine>
        {
            new(VillagerText.Title(rules, villager, world), BookTone.Heading),
            new(Capital(VillagerText.Doing(villager, world)) + "."),
            new(Capital(VillagerText.Faith(villager, world)) + "."),
            new(Format("Hunger {0:0%}, weariness {1:0%}, health {2:0%}.", villager.Hunger, villager.Fatigue, villager.Health), villager.Health < 0.5f || villager.Hunger >= 0.9f ? BookTone.Warning : BookTone.Plain),
            new("What they want", BookTone.Heading),
        };
        lines.AddRange(VillagerMind.Options(rules, world, villager).Select(o => new BookLine(Format("To {0}: {1:0.00} ({2})", VillagerText.Verb(o.Activity), o.Score, o.Reason))));
        if (VillagerModel.Wary(villager))
        {
            lines.Add(new("Danger they have known makes them flee it sooner.", BookTone.Muted));
        }

        lines.Add(new("Their life", BookTone.Heading));
        lines.AddRange(villager.Memories.AsEnumerable().Reverse().Select(memory => new BookLine(
            Format("At {0:0}, {1}.", Math.Floor(VillagerModel.Age(rules, villager, memory.Tick)), VillagerText.Memory(memory, villager, world, id => session.Content.Miracles.Find(id)?.Name ?? id)),
            memory.Deed is VillagerDeed.Fled or VillagerDeed.Hungered or VillagerDeed.FellIll ? BookTone.Warning : BookTone.Plain)));
        return new BookPage(BookTab.Villager, "Your villager", lines);
    }

    /// <summary>A phrase with its first letter in capitals.</summary>
    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
