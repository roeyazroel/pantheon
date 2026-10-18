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

        RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks), errors);
        RequireShare(rules.WorkforceShare, nameof(rules.WorkforceShare), errors);
        RequireAtLeast(rules.FarmlandRadius, 0, nameof(rules.FarmlandRadius), errors);
        RequirePositive(rules.FarmersPerRegion, nameof(rules.FarmersPerRegion), errors);
        RequireNonNegative(rules.FoodPerFarmerPerStep, nameof(rules.FoodPerFarmerPerStep), errors);
        RequireNonNegative(rules.FoodPerPersonPerStep, nameof(rules.FoodPerPersonPerStep), errors);
        RequireNonNegative(rules.FoodStoragePerHousing, nameof(rules.FoodStoragePerHousing), errors);
        RequireNonNegative(rules.SurplusFoodPerPerson, nameof(rules.SurplusFoodPerPerson), errors);
        RequireShare(rules.StarvationHealthLossPerStep, nameof(rules.StarvationHealthLossPerStep), errors);
        RequireShare(rules.HealthRecoveryPerStep, nameof(rules.HealthRecoveryPerStep), errors);
        RequireProbability(rules.BirthChancePerPersonPerStep, nameof(rules.BirthChancePerPersonPerStep), errors);
        RequireProbability(rules.DeathChancePerPersonPerStep, nameof(rules.DeathChancePerPersonPerStep), errors);
        RequireProbability(rules.IllHealthDeathChancePerPersonPerStep, nameof(rules.IllHealthDeathChancePerPersonPerStep), errors);
        RequireAtLeast(rules.HousingPerHouse, 1, nameof(rules.HousingPerHouse), errors);
        if (!(rules.ConstructionStartCrowding is > 0f and <= 1f))
        {
            errors.Add($"{nameof(rules.ConstructionStartCrowding)} must be above 0 and at most 1.");
        }

        RequireShare(rules.MaxBuilderShare, nameof(rules.MaxBuilderShare), errors);
        RequirePositive(rules.BuildersPerSite, nameof(rules.BuildersPerSite), errors);
        RequirePositive(rules.WorkPerHouse, nameof(rules.WorkPerHouse), errors);
        RequireNonNegative(rules.ConstructionFoodReserveSteps, nameof(rules.ConstructionFoodReserveSteps), errors);
        RequireAtLeast(rules.MaxHousing, 0, nameof(rules.MaxHousing), errors);

        if (errors.Count > 0)
        {
            throw new ArgumentException("Invalid settlement rules:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors), nameof(rules));
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> is at least <paramref name="minimum"/>.</summary>
    private static void RequireAtLeast(int value, int minimum, string name, List<string> errors)
    {
        if (value < minimum)
        {
            errors.Add($"{name} must be at least {minimum}.");
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

    /// <summary>Records an error unless <paramref name="value"/> is a finite number above zero.</summary>
    private static void RequirePositive(float value, string name, List<string> errors)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            errors.Add($"{name} must be a finite number above 0.");
        }
    }

    /// <summary>Records an error unless <paramref name="value"/> lies in 0..1.</summary>
    private static void RequireShare(float value, string name, List<string> errors)
    {
        if (!(value is >= 0f and <= 1f))
        {
            errors.Add($"{name} must lie between 0 and 1.");
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
