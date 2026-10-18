using System;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Validation;

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
        var errors = new RuleErrors();

        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireShare(rules.WorkforceShare, nameof(rules.WorkforceShare));
        errors.RequireAtLeast(rules.FarmlandRadius, 0, nameof(rules.FarmlandRadius));
        errors.RequirePositive(rules.FarmersPerRegion, nameof(rules.FarmersPerRegion));
        errors.RequireNonNegative(rules.BlessingYieldBonus, nameof(rules.BlessingYieldBonus));
        errors.RequireShare(rules.WorshipWorkforceShare, nameof(rules.WorshipWorkforceShare));
        errors.RequireNonNegative(rules.FoodPerFarmerPerStep, nameof(rules.FoodPerFarmerPerStep));
        errors.RequireNonNegative(rules.FoodPerPersonPerStep, nameof(rules.FoodPerPersonPerStep));
        errors.RequireNonNegative(rules.FoodStoragePerHousing, nameof(rules.FoodStoragePerHousing));
        errors.RequireNonNegative(rules.SurplusFoodPerPerson, nameof(rules.SurplusFoodPerPerson));
        errors.RequireShare(rules.StarvationHealthLossPerStep, nameof(rules.StarvationHealthLossPerStep));
        errors.RequireShare(rules.HealthRecoveryPerStep, nameof(rules.HealthRecoveryPerStep));
        errors.RequireProbability(rules.BirthChancePerPersonPerStep, nameof(rules.BirthChancePerPersonPerStep));
        errors.RequireProbability(rules.DeathChancePerPersonPerStep, nameof(rules.DeathChancePerPersonPerStep));
        errors.RequireProbability(rules.IllHealthDeathChancePerPersonPerStep, nameof(rules.IllHealthDeathChancePerPersonPerStep));
        errors.RequireAtLeast(rules.HousingPerHouse, 1, nameof(rules.HousingPerHouse));
        if (!(rules.ConstructionStartCrowding is > 0f and <= 1f))
        {
            errors.Add($"{nameof(rules.ConstructionStartCrowding)} must be above 0 and at most 1.");
        }

        errors.RequireShare(rules.MaxBuilderShare, nameof(rules.MaxBuilderShare));
        errors.RequirePositive(rules.BuildersPerSite, nameof(rules.BuildersPerSite));
        errors.RequirePositive(rules.WorkPerHouse, nameof(rules.WorkPerHouse));
        errors.RequireNonNegative(rules.ConstructionFoodReserveSteps, nameof(rules.ConstructionFoodReserveSteps));
        errors.RequireAtLeast(rules.MaxHousing, 0, nameof(rules.MaxHousing));
        errors.ThrowIfAny("settlement rules", nameof(rules));
    }
}
