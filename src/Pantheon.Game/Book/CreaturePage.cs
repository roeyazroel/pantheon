using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Game.Creatures;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Book;

/// <summary>
/// The book's Creature page (spec §57): the player's Creature in full: its growth, paths and
/// callings, temperament, what it has learned, what it remembers and how it regards the others. Pure.
/// </summary>
public static class CreaturePage
{
    // Most memories shown, newest first.
    private const int ShownMemories = 8;

    /// <summary>Builds the page.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The page.</returns>
    public static BookPage Build(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.LocalCreature is not { } creature)
        {
            return new BookPage(BookTab.Creature, "Your Creature", [new("You have no Creature.", BookTone.Muted)]);
        }

        var rules = session.Content.Creatures;
        var world = session.World;
        var next = creature.Level < CreatureModel.MaxLevel && creature.Level - 1 < rules.LevelExperience.Count
            ? Format(" of {0:0} for level {1}", rules.LevelExperience[creature.Level - 1], creature.Level + 1)
            : string.Empty;
        var lines = new List<BookLine>
        {
            new(Format("{0} the {1}", creature.Name, creature.Species), BookTone.Heading),
            new(Format("Level {0}, {1:0} experience{2}. {3}.", creature.Level, creature.Experience, next, CreatureText.DescribeActivity(creature, world))),
            new(CreatureGrowthText.Attributes(rules, session.Content.DivineRules, creature)),
            new(Format("Health {0:0%}, loyalty {1:0%}, morality {2:+0.00;-0.00;0.00}, combat skill {3:0%}, {4} scars.", creature.Health, creature.Loyalty, creature.Morality, creature.CombatSkill, creature.Scars)),
            new(Format(
                "Kindness {0:0.00}, aggression {1:0.00}, laziness {2:0.00}, curiosity {3:0.00}.",
                creature.Personality.Kindness,
                creature.Personality.Aggression,
                creature.Personality.Laziness,
                creature.Personality.Curiosity)),
            new("Paths", BookTone.Heading),
            new(CreatureGrowthText.Paths(rules, creature), BookTone.Good),
        };
        lines.AddRange(creature.Specialisations.Select(path => new BookLine(Format("{0}: it {1}.", CreatureText.Path(path), CreatureText.PathEffect(path)))));
        lines.Add(new("Called by its deeds towards: " + CreatureGrowthText.Callings(creature), BookTone.Muted));
        lines.Add(new("What it has learned", BookTone.Heading));
        foreach (var group in creature.Preferences.GroupBy(p => p.Situation).OrderBy(g => g.Key))
        {
            var best = group.OrderByDescending(p => p.Weight).ThenBy(p => p.Action).First();
            lines.Add(new(Format("{0}: {1} ({2:0.00})", Situation(group.Key), CreatureText.Doing(best.Action, null), best.Weight)));
        }

        lines.Add(new("What it remembers", BookTone.Heading));
        lines.AddRange(creature.Memories.AsEnumerable().Reverse().Take(ShownMemories).Select(memory => new BookLine(
            CreatureText.Doing(memory.Action, CreatureText.Concerning(world, memory.SettlementId, memory.OtherCreatureId)) + Feedback(memory.Feedback),
            memory.Feedback == CreatureFeedback.Punished ? BookTone.Warning : BookTone.Plain)));
        lines.Add(new("Other Creatures", BookTone.Heading));
        lines.AddRange(creature.Relations.Count == 0
            ? [new BookLine("It has met no other Creature.", BookTone.Muted)]
            : creature.Relations.Select(view => new BookLine(CreatureBondText.Describe(rules.Bonds, world, view))));
        return new BookPage(BookTab.Creature, "Your Creature", lines);
    }

    /// <summary>A situation in words.</summary>
    private static string Situation(CreatureSituation situation) => situation switch
    {
        CreatureSituation.Idle => "With nothing to do",
        CreatureSituation.Hungry => "Hungry",
        CreatureSituation.Tired => "Tired",
        CreatureSituation.Bored => "Bored",
        CreatureSituation.VillageInNeed => "By a village in need",
        CreatureSituation.VillageBuilding => "By a village building",
        CreatureSituation.VillageThriving => "By a thriving village",
        CreatureSituation.Commanded => "When sent",
        CreatureSituation.MeetsCreature => "Meeting a Creature",
        CreatureSituation.MeetsEnemyCreature => "Meeting an enemy's Creature",
    };

    /// <summary>What its god thought of a deed.</summary>
    private static string Feedback(CreatureFeedback feedback) => feedback switch
    {
        CreatureFeedback.None => string.Empty,
        CreatureFeedback.Rewarded => " - rewarded",
        CreatureFeedback.Punished => " - punished",
    };

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
