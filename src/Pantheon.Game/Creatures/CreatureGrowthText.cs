using System;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Game.Creatures;

/// <summary>Words for a Creature's paths and attributes (spec §23–24, ADR-030), for its card and the debug panel. Pure and culture-invariant.</summary>
public static class CreatureGrowthText
{
    /// <summary>The paths a Creature walks, and what the next calling level brings.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example "Healer, Guardian - at level 9 it takes up a third path".</returns>
    public static string Paths(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        var walked = creature.Specialisations.Count == 0 ? "No path yet" : string.Join(", ", creature.Specialisations.Select(CreatureText.Path));
        var next = rules.Callings.Levels.Where(level => level > creature.Level).Order().Cast<int?>().FirstOrDefault();
        return next is { } level
            ? Format("{0} - at level {1} it becomes a {2}", walked, level, CreatureText.Path(CreatureCalling.Next(creature)))
            : walked;
    }

    /// <summary>The Creature's attributes (spec §23).</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules, for divine affinity.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example "strength 1.6   speed 8.0   intelligence 1.10   resilience 1.05   affinity 1.20".</returns>
    public static string Attributes(CreatureRules rules, DivineRules divine, CreatureState creature) => Format(
        "strength {0:0.0}   speed {1:0.0}   intelligence {2:0.00}   resilience {3:0.00}   affinity {4:0.00}",
        CreatureModel.Strength(rules, creature),
        CreatureModel.Speed(rules, creature),
        CreatureAttributes.Intelligence(rules, creature),
        CreatureAttributes.Resilience(rules, creature),
        CreatureAttributes.Affinity(divine, creature));

    /// <summary>How far its deeds have called it to each path, most first; the paths it has no calling to are left out.</summary>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example "Healer 12.0, Guardian 3.5"; "none yet" without any.</returns>
    public static string Callings(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var callings = Enum.GetValues<CreatureSpecialisation>()
            .Select(path => (Path: path, Calling: CreatureCalling.Get(creature.Callings, path)))
            .Where(c => c.Calling > 0f)
            .OrderByDescending(c => c.Calling)
            .ThenBy(c => c.Path)
            .Select(c => Format("{0} {1:0.0}", CreatureText.Path(c.Path), c.Calling))
            .ToList();
        return callings.Count == 0 ? "none yet" : string.Join(", ", callings);
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
