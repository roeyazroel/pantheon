using System;
using System.Collections.Generic;
using Pantheon.Domain.Settlements;

namespace Pantheon.Simulation.Settlements;

/// <summary>Validates data-driven <see cref="SettlementRules"/>.</summary>
public static class SettlementRulesValidator
{
    /// <summary>Throws when any rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(SettlementRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new List<string>();

        if (!(rules.WorkforceShare is >= 0f and <= 1f))
        {
            errors.Add("WorkforceShare must lie between 0 and 1.");
        }

        RequireNonNegative(rules.FoodPerWorkerPerTick, nameof(rules.FoodPerWorkerPerTick), errors);
        RequireNonNegative(rules.FoodPerPersonPerTick, nameof(rules.FoodPerPersonPerTick), errors);
        RequireNonNegative(rules.FoodStoragePerHousing, nameof(rules.FoodStoragePerHousing), errors);
        RequireNonNegative(rules.SurplusFoodPerPerson, nameof(rules.SurplusFoodPerPerson), errors);
        RequireProbability(rules.BirthChancePerPersonPerTick, nameof(rules.BirthChancePerPersonPerTick), errors);
        RequireProbability(rules.DeathChancePerPersonPerTick, nameof(rules.DeathChancePerPersonPerTick), errors);
        RequireProbability(rules.StarvationDeathChancePerPersonPerTick, nameof(rules.StarvationDeathChancePerPersonPerTick), errors);

        if (errors.Count > 0)
        {
            throw new ArgumentException("Invalid settlement rules:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors), nameof(rules));
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> is a finite, non-negative number.</summary>
    private static void RequireNonNegative(float value, string name, List<string> errors)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            errors.Add($"{name} must be a finite, non-negative number.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> lies in 0..1.</summary>
    private static void RequireProbability(double value, string name, List<string> errors)
    {
        if (!(value is >= 0.0 and <= 1.0))
        {
            errors.Add($"{name} must lie between 0 and 1.");
        }
    }
}
