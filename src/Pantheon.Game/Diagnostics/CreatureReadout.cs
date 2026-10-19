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
            if (snapshot.CreatureRules is { } growth)
            {
                lines.Add("  " + CreatureGrowthText.Attributes(growth, snapshot.DivineRules, creature));
                lines.Add(Format("  paths: {0}   callings: {1}", CreatureGrowthText.Paths(growth, creature), CreatureGrowthText.Callings(creature)));
            }

            lines.Add(Format(
                "  doing: {0} [{1}, {2}, {3} waypoints, {4} ticks left]",
                CreatureText.DescribeActivity(creature, world),
                activity.Action,
                activity.Situation,
                activity.Path.Count,
                activity.TicksLeft));
            lines.Add(Format("  mind: {0}; tree: {1}", Simulation.Creatures.CreatureModes.Describe(world, creature), Simulation.Creatures.CreatureTree.Describe(creature)));
            var face = Presentation.CreatureExpression.FaceOf(creature, world.Tick);
            lines.Add(Format("  face: {0} (joy {1:+0.00;-0.00;0.00}, anger {2:0.00}, fear {3:0.00}, sleep {4:0.00}); nature {5:+0.00;-0.00;0.00}", Presentation.CreatureExpression.MoodOf(face).ToString().ToLowerInvariant(), face.Joy, face.Anger, face.Fear, face.Sleep, creature.Morality));
            lines.Add(Format(
                "  needs: hunger {0:0.00} fatigue {1:0.00} boredom {2:0.00}   personality: {3} ({4})",
                n.Hunger,
                n.Fatigue,
                n.Boredom,
                TemperamentText.Values(p),
                TemperamentText.Describe(creature)));
            if (creature.Id == snapshot.LocalCreatureId && snapshot.CreatureOptions.Count > 0)
            {
                lines.Add("  options: " + string.Join("   ", snapshot.CreatureOptions.Take(OptionsShown).Select(o => DescribeOption(o, world))));
            }

            if (creature.Id == snapshot.LocalCreatureId && snapshot.CreatureResponses.Count > 0)
            {
                var other = CreatureText.Concerning(world, null, snapshot.CreatureResponses[0].CreatureId) ?? "another Creature";
                lines.Add(Format("  meeting {0}: ", other) + string.Join("   ", snapshot.CreatureResponses.Select(o => Format("{0} {1:0.00}", o.Action, o.Utility))));
            }

            AppendBonds(lines, creature, snapshot);
            AppendPreferences(lines, creature);
            AppendMemories(lines, creature, world);
        }
    }

    /// <summary>Its fighting power, skill and scars, and its views of the other Creatures with their latest encounters (ADR-017).</summary>
    private static void AppendBonds(List<string> lines, CreatureState creature, DebugSnapshot snapshot)
    {
        var world = snapshot.World;
        var power = snapshot.CreatureRules is { } rules ? Format("   power {0:0.00}", CreatureBonds.Power(rules, creature)) : string.Empty;
        var views = creature.Relations.Select(view =>
        {
            var name = CreatureText.Concerning(world, null, view.OtherCreatureId) ?? "?";
            var recent = view.Encounters.AsEnumerable().Reverse().Take(MemoriesShown).Select(e => CreatureBondText.Encounter(e.Kind));
            return Format(
                "{0} friend {1:0.00} fear {2:0.00} rival {3:0.00} respect {4:0.00} trust {5:0.00} [{6}]",
                name,
                view.Friendship,
                view.Fear,
                view.Rivalry,
                view.Respect,
                view.Trust,
                string.Join(", ", recent));
        });
        lines.Add(Format("  combat skill {0:0.00}   scars {1}{2}   bonds: ", creature.CombatSkill, creature.Scars, power) + (creature.Relations.Count == 0 ? "none" : string.Join(" | ", views)));
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
                var place = CreatureText.Concerning(world, memory.SettlementId, memory.OtherCreatureId);
                var feedback = memory.Feedback == CreatureFeedback.None ? string.Empty : " (" + memory.Feedback.ToString().ToLowerInvariant() + ")";
                return CreatureText.Doing(memory.Action, place) + feedback;
            });
        lines.Add("  remembers: " + string.Join(", ", memories));
    }

    /// <summary>One option with its utility.</summary>
    private static string DescribeOption(CreatureOption option, WorldState world)
    {
        var place = CreatureText.Concerning(world, option.SettlementId, option.CreatureId);
        return Format("{0} {1:0.00}", CreatureText.Doing(option.Action, place), option.Utility);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(Invariant, format, args);
}
