using System;
using Pantheon.Domain.Water;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Water;

/// <summary>Validates data-driven <see cref="WaterRules"/>.</summary>
public static class WaterRulesValidator
{
    /// <summary>Throws when any rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(WaterRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireNonNegative(rules.BaseLevel, nameof(rules.BaseLevel));
        errors.RequireShare(rules.SoilThreshold, nameof(rules.SoilThreshold));
        errors.RequireNonNegative(rules.SoilLevelGain, nameof(rules.SoilLevelGain));
        errors.RequireNonNegative(rules.RainLevelGain, nameof(rules.RainLevelGain));
        errors.RequireShare(rules.LevelResponsePerSecond, nameof(rules.LevelResponsePerSecond));
        errors.RequirePositive(rules.FloodLevel, nameof(rules.FloodLevel));
        if (!(rules.MaxFloodLevel > rules.FloodLevel))
        {
            errors.Add($"{nameof(rules.MaxFloodLevel)} must be above {nameof(rules.FloodLevel)}.");
        }

        errors.RequireNonNegative(rules.FloodBankHeight, nameof(rules.FloodBankHeight));
        errors.RequireShare(rules.FloodRecedePerSecond, nameof(rules.FloodRecedePerSecond));
        errors.RequireShare(rules.FloodHousingLossPerSecond, nameof(rules.FloodHousingLossPerSecond));
        errors.RequireShare(rules.FloodDeathsPerSecond, nameof(rules.FloodDeathsPerSecond));
        errors.RequireShare(rules.FloodFoodLossPerSecond, nameof(rules.FloodFoodLossPerSecond));
        errors.RequireNonNegative(rules.FloodDreadPerSecond, nameof(rules.FloodDreadPerSecond));
        errors.RequireShare(rules.SiltBlessing, nameof(rules.SiltBlessing));
        errors.RequireShare(rules.IrrigationPerSecond, nameof(rules.IrrigationPerSecond));
        errors.ThrowIfAny("water rules", nameof(rules));
    }
}
