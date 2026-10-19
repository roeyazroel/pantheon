using System;
using Pantheon.Domain.History;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.History;

/// <summary>Validates data-driven <see cref="HistoryRules"/>.</summary>
public static class HistoryRulesValidator
{
    /// <summary>Throws when a rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(HistoryRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequirePositive(rules.SecondsPerYear, nameof(rules.SecondsPerYear));
        errors.RequireNonNegative(rules.RepeatSeconds, nameof(rules.RepeatSeconds));
        errors.RequireAtLeast(rules.Capacity, 1, nameof(rules.Capacity));
        errors.RequireAtLeast(rules.EarthquakeDeaths, 1, nameof(rules.EarthquakeDeaths));
        errors.RequireAtLeast(rules.HousesLost, 1, nameof(rules.HousesLost));
        errors.RequireAtLeast(rules.ExodusPeople, 1, nameof(rules.ExodusPeople));
        errors.RequireAtLeast(rules.ConversionPopulation, 0, nameof(rules.ConversionPopulation));
        if (rules.LegendLevels is null)
        {
            errors.Add("LegendLevels is required.");
        }
        else
        {
            foreach (var level in rules.LegendLevels)
            {
                errors.RequireAtLeast(level, 2, "LegendLevels");
            }
        }

        errors.ThrowIfAny("history rules", nameof(rules));
    }
}
