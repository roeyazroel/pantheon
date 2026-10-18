using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// The Creature debug view (spec §106): current behaviour, personality, needs, learned weights
/// and memories, plus the options it is weighing now.
/// </summary>
internal static class CreatureReadout
{
    private const int OptionsShown = 4;
    private const int MemoriesShown = 5;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Adds the lines of every Creature.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        var world = snapshot.World;
        foreach (var creature in world.Creatures)
        {
            var next = snapshot.CreatureRules is { } rules && creature.Level < CreatureModel.MaxLevel && creature.Level - 1 < rules.LevelExperience.Count
                ? Format("/{0:0}", rules.LevelExperience[creature.Level - 1])
                : string.Empty;
            var p = creature.Personality;
            var n = creature.Needs;
            var activity = creature.Activity;
            lines.Add(Format(
                "Creature {0} ({1}) Lv {2} XP {3:0}{4}   health {5:0%}   loyalty {6:0.00}   morality {7:+0.00;-0.00;0.00}   at ({8:0}, {9:0})",
                creature.Name,
                creature.Species,
                creature.Level,
                creature.Experience,
                next,
                creature.Health,
                creature.Loyalty,
                creature.Morality,
                creature.Position.X,
                creature.Position.Z));
            lines.Add(Format(
                "  doing: {0} [{1}, {2}, {3} waypoints, {4} ticks left]",
                CreatureText.DescribeActivity(creature, world),
                activity.Action,
                activity.Situation,
                activity.Path.Count,
                activity.TicksLeft));
            lines.Add(Format(
                "  needs: hunger {0:0.00} fatigue {1:0.00} boredom {2:0.00}   personality: kind {3:0.00} aggressive {4:0.00} lazy {5:0.00} curious {6:0.00}",
                n.Hunger,
                n.Fatigue,
                n.Boredom,
                p.Kindness,
                p.Aggression,
                p.Laziness,
                p.Curiosity));
            if (creature.Id == snapshot.LocalCreatureId && snapshot.CreatureOptions.Count > 0)
            {
                lines.Add("  options: " + string.Join("   ", snapshot.CreatureOptions.Take(OptionsShown).Select(o => DescribeOption(o, world))));
            }

            AppendPreferences(lines, creature);
            AppendMemories(lines, creature, world);
        }
    }

    /// <summary>The learned preferences, grouped by situation.</summary>
    private static void AppendPreferences(List<string> lines, CreatureState creature)
    {
        var groups = creature.Preferences
            .GroupBy(preference => preference.Situation)
            .Select(group => Format("{0}: {1}", group.Key, string.Join(" ", group.Select(preference => Format("{0} {1:0.00}", preference.Action, preference.Weight)))));
        lines.Add("  learned: " + string.Join(" | ", groups));
    }

    /// <summary>The latest memories, newest first, with the god's feedback.</summary>
    private static void AppendMemories(List<string> lines, CreatureState creature, WorldState world)
    {
        if (creature.Memories.Count == 0)
        {
            return;
        }

        var memories = creature.Memories
            .AsEnumerable()
            .Reverse()
            .Take(MemoriesShown)
            .Select(memory =>
            {
                var place = memory.SettlementId is { } id ? world.Settlements.Find(s => s.Id == id)?.Name : null;
                var feedback = memory.Feedback == CreatureFeedback.None ? string.Empty : " (" + memory.Feedback.ToString().ToLowerInvariant() + ")";
                return CreatureText.Doing(memory.Action, place) + feedback;
            });
        lines.Add("  remembers: " + string.Join(", ", memories));
    }

    /// <summary>One option with its utility.</summary>
    private static string DescribeOption(CreatureOption option, WorldState world)
    {
        var place = option.SettlementId is { } id ? world.Settlements.Find(s => s.Id == id)?.Name : null;
        return Format("{0} {1:0.00}", CreatureText.Doing(option.Action, place), option.Utility);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(Invariant, format, args);
}
