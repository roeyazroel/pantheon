using System;
using Pantheon.Domain.Ecology;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Ecology;

/// <summary>Checks <see cref="EcologyRules"/> and lists every problem.</summary>
public static class EcologyRulesValidator
{
    /// <summary>Validates the rules.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more values are invalid.</exception>
    public static void Validate(EcologyRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireShare(rules.WildlifeGrowthPerSecond, nameof(rules.WildlifeGrowthPerSecond));
        errors.RequireNonNegative(rules.HuntPerHundredPerSecond, nameof(rules.HuntPerHundredPerSecond));
        errors.RequireNonNegative(rules.FoodPerWildlife, nameof(rules.FoodPerWildlife));
        errors.RequireShare(rules.FireWildlifeLossPerSecond, nameof(rules.FireWildlifeLossPerSecond));
        errors.RequireShare(rules.FireDamagePerSecond, nameof(rules.FireDamagePerSecond));
        errors.RequireShare(rules.FloodDamagePerSecond, nameof(rules.FloodDamagePerSecond));
        errors.RequireShare(rules.QuakeDamage, nameof(rules.QuakeDamage));
        errors.RequireShare(rules.HarmDamage, nameof(rules.HarmDamage));
        errors.RequireShare(rules.HealPerSecond, nameof(rules.HealPerSecond));
        errors.RequireShare(rules.ScarThreshold, nameof(rules.ScarThreshold));
        errors.RequireShare(rules.ScarPerSecond, nameof(rules.ScarPerSecond));
        errors.RequireNonNegative(rules.NatureHealFactor, nameof(rules.NatureHealFactor));
        errors.RequireShare(rules.NatureScarHealPerSecond, nameof(rules.NatureScarHealPerSecond));
        errors.RequireShare(rules.ScarNotice, nameof(rules.ScarNotice));
        errors.RequireAtLeast(rules.PollutionFreePeople, 0, nameof(rules.PollutionFreePeople));
        errors.RequireShare(rules.PollutionPerThousandPerSecond, nameof(rules.PollutionPerThousandPerSecond));
        errors.RequireShare(rules.PollutionClearPerSecond, nameof(rules.PollutionClearPerSecond));
        errors.ThrowIfAny("ecology rules", nameof(rules));
    }
}
