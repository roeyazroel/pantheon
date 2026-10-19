using System;
using Pantheon.Domain.Migration;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Migration;

/// <summary>Checks <see cref="MigrationRules"/> and lists every problem.</summary>
public static class MigrationRulesValidator
{
    /// <summary>Validates the rules.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more values are invalid.</exception>
    public static void Validate(MigrationRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();

        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireNonNegative(rules.FamineWeight, nameof(rules.FamineWeight));
        errors.RequireNonNegative(rules.HomelessWeight, nameof(rules.HomelessWeight));
        errors.RequireNonNegative(rules.DangerWeight, nameof(rules.DangerWeight));
        errors.RequireNonNegative(rules.WarWeight, nameof(rules.WarWeight));
        errors.RequireShare(rules.WarPresence, nameof(rules.WarPresence));
        errors.RequireNonNegative(rules.FearWeight, nameof(rules.FearWeight));
        errors.RequireNonNegative(rules.DoubtWeight, nameof(rules.DoubtWeight));
        errors.RequireShare(rules.LoveLoyalty, nameof(rules.LoveLoyalty));
        if (!(rules.PressureThreshold is >= 0f and < 1f))
        {
            errors.Add($"{nameof(rules.PressureThreshold)} must be at least 0 and below 1.");
        }

        errors.RequireShare(rules.EmigrationPerSecond, nameof(rules.EmigrationPerSecond));
        errors.RequireShare(rules.UrgeFadePerSecond, nameof(rules.UrgeFadePerSecond));
        errors.RequireAtLeast(rules.MinGroupSize, 1, nameof(rules.MinGroupSize));
        errors.RequireShare(rules.GroupShare, nameof(rules.GroupShare));
        errors.RequireShare(rules.MaxGroupShare, nameof(rules.MaxGroupShare));
        errors.RequireAtLeast(rules.MinRemaining, 0, nameof(rules.MinRemaining));
        errors.RequireAtLeast(rules.MaxGroups, 0, nameof(rules.MaxGroups));
        errors.RequirePositive(rules.MaxDistance, nameof(rules.MaxDistance));
        errors.RequireShare(rules.BetterBy, nameof(rules.BetterBy));
        errors.RequireNonNegative(rules.RoomWeight, nameof(rules.RoomWeight));
        errors.RequireNonNegative(rules.FoodWeight, nameof(rules.FoodWeight));
        errors.RequireNonNegative(rules.SafetyWeight, nameof(rules.SafetyWeight));
        errors.RequireNonNegative(rules.FaithWeight, nameof(rules.FaithWeight));
        errors.RequireNonNegative(rules.DistanceWeight, nameof(rules.DistanceWeight));
        errors.RequirePositive(rules.Speed, nameof(rules.Speed));
        errors.RequireNonNegative(rules.FoodTakenPerPerson, nameof(rules.FoodTakenPerPerson));
        errors.RequireNonNegative(rules.FoodPerPersonPerSecond, nameof(rules.FoodPerPersonPerSecond));
        errors.RequireShare(rules.HungerHealthLossPerSecond, nameof(rules.HungerHealthLossPerSecond));
        errors.RequireShare(rules.HealthRecoveryPerSecond, nameof(rules.HealthRecoveryPerSecond));
        errors.RequireShare(rules.DeathsPerSecond, nameof(rules.DeathsPerSecond));
        errors.RequireShare(rules.AbandonShare, nameof(rules.AbandonShare));
        errors.RequireShare(rules.WelcomeLove, nameof(rules.WelcomeLove));
        errors.RequireShare(rules.ConversionDemand, nameof(rules.ConversionDemand));
        errors.RequireShare(rules.ConversionFear, nameof(rules.ConversionFear));
        errors.RequireShare(rules.ConversionDoubt, nameof(rules.ConversionDoubt));
        errors.RequireShare(rules.TurnedAwayLoss, nameof(rules.TurnedAwayLoss));
        errors.RequireShare(rules.OverwhelmedHomeless, nameof(rules.OverwhelmedHomeless));
        errors.ThrowIfAny("migration rules", nameof(rules));
    }
}
