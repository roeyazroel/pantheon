using Pantheon.Domain.Notables;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Notables;

/// <summary>Checks <see cref="NotableRules"/> (ADR-032) into the divine check's errors.</summary>
internal static class NotableRulesValidator
{
    /// <summary>Adds every problem with the prophets' and elders' rules.</summary>
    /// <param name="errors">The divine check's errors.</param>
    /// <param name="rules">The rules.</param>
    public static void Check(RuleErrors errors, NotableRules rules)
    {
        if (System.Linq.Enumerable.Any(rules.Names, string.IsNullOrWhiteSpace))
        {
            errors.Add("Notables.Names must not be blank.");
        }

        errors.RequireAtLeast(rules.MaxProphetsPerGod, 0, "Notables.MaxProphetsPerGod");
        errors.RequireAtLeast(rules.ElderPopulation, 1, "Notables.ElderPopulation");
        errors.RequireAtLeast(rules.MemoryCapacity, 1, "Notables.MemoryCapacity");
        foreach (var (value, name) in new[]
        {
            (rules.Devotion, "Notables.Devotion"),
            (rules.AnointAdherence, "Notables.AnointAdherence"),
            (rules.PreachConversion, "Notables.PreachConversion"),
            (rules.PreachFaith, "Notables.PreachFaith"),
            (rules.ProphetInfluence, "Notables.ProphetInfluence"),
            (rules.ElderDevotionPerSecond, "Notables.ElderDevotionPerSecond"),
            (rules.ElderTurnResentment, "Notables.ElderTurnResentment"),
            (rules.TurnDoubt, "Notables.TurnDoubt"),
            (rules.Mortality, "Notables.Mortality"),
        })
        {
            errors.RequireShare(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (rules.ProphetsPerHour, "Notables.ProphetsPerHour"),
            (rules.ProphetSpeed, "Notables.ProphetSpeed"),
            (rules.ProphetRange, "Notables.ProphetRange"),
            (rules.ProphetReach, "Notables.ProphetReach"),
            (rules.MartyrRadius, "Notables.MartyrRadius"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        foreach (var (shortest, longest, name) in new[] { (rules.ProphetMinSeconds, rules.ProphetMaxSeconds, "Notables.Prophet"), (rules.ElderMinSeconds, rules.ElderMaxSeconds, "Notables.Elder") })
        {
            errors.RequirePositive(shortest, name + "MinSeconds");
            if (longest < shortest)
            {
                errors.Add($"{name}MaxSeconds must be at least {name}MinSeconds.");
            }
        }

        errors.RequirePositive(rules.PreachSeconds, "Notables.PreachSeconds");
        errors.RequireSignedShare(rules.Martyrdom.Faith, "Notables.Martyrdom.Faith");
        errors.RequireSignedShare(rules.Martyrdom.Love, "Notables.Martyrdom.Love");
        errors.RequireSignedShare(rules.Martyrdom.Fear, "Notables.Martyrdom.Fear");
        errors.RequireSignedShare(rules.Martyrdom.Doubt, "Notables.Martyrdom.Doubt");
    }
}
