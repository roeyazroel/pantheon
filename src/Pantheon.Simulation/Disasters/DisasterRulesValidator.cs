using System;
using Pantheon.Domain.Disasters;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Disasters;

/// <summary>Checks <see cref="DisasterRules"/> and lists every problem.</summary>
public static class DisasterRulesValidator
{
    /// <summary>Validates the rules.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more values are invalid.</exception>
    public static void Validate(DisasterRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();

        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireNonNegative(rules.LightningIgnitionPerSecond, nameof(rules.LightningIgnitionPerSecond));
        errors.RequireShare(rules.HearthFirePerSecond, nameof(rules.HearthFirePerSecond));
        errors.RequireNonNegative(rules.FuelHeat, nameof(rules.FuelHeat));
        errors.RequireShare(rules.IgnitionIntensity, nameof(rules.IgnitionIntensity));
        errors.RequireShare(rules.MinFlammability, nameof(rules.MinFlammability));
        if (!(rules.WetSoil is > 0f and <= 1f))
        {
            errors.Add($"{nameof(rules.WetSoil)} must be above 0 and at most 1.");
        }

        errors.RequireShare(rules.FireGrowthPerSecond, nameof(rules.FireGrowthPerSecond));
        errors.RequireShare(rules.BurnPerSecond, nameof(rules.BurnPerSecond));
        errors.RequireNonNegative(rules.SpreadPerSecond, nameof(rules.SpreadPerSecond));
        errors.RequireNonNegative(rules.WindSpread, nameof(rules.WindSpread));
        errors.RequireNonNegative(rules.RainQuenchPerSecond, nameof(rules.RainQuenchPerSecond));
        errors.RequireNonNegative(rules.FirefightingPerSecond, nameof(rules.FirefightingPerSecond));
        errors.RequireShare(rules.ScorchRecoveryPerSecond, nameof(rules.ScorchRecoveryPerSecond));
        errors.RequireShare(rules.FireHousingLossPerSecond, nameof(rules.FireHousingLossPerSecond));
        errors.RequireShare(rules.FireDeathsPerSecond, nameof(rules.FireDeathsPerSecond));
        errors.RequireShare(rules.FireFoodLossPerSecond, nameof(rules.FireFoodLossPerSecond));
        errors.RequireShare(rules.FireDreadPerSecond, nameof(rules.FireDreadPerSecond));
        errors.RequireNonNegative(rules.EarthquakesPerHour, nameof(rules.EarthquakesPerHour));
        if (!(rules.HarmlessMagnitude < rules.FullMagnitude) || !(rules.MinMagnitude <= rules.MaxMagnitude))
        {
            errors.Add($"{nameof(rules.HarmlessMagnitude)} must be below {nameof(rules.FullMagnitude)}, and {nameof(rules.MinMagnitude)} at most {nameof(rules.MaxMagnitude)}.");
        }

        errors.RequirePositive(rules.RadiusPerMagnitude, nameof(rules.RadiusPerMagnitude));
        errors.RequirePositive(rules.ShakingSeconds, nameof(rules.ShakingSeconds));
        errors.RequireShare(rules.QuakeHousingLoss, nameof(rules.QuakeHousingLoss));
        errors.RequireShare(rules.QuakeDeaths, nameof(rules.QuakeDeaths));
        errors.RequireShare(rules.QuakeFoodLoss, nameof(rules.QuakeFoodLoss));
        errors.RequireShare(rules.QuakeFireChance, nameof(rules.QuakeFireChance));
        errors.RequireShare(rules.HouseQuakeResistance, nameof(rules.HouseQuakeResistance));
        errors.RequireShare(rules.HouseFireResistance, nameof(rules.HouseFireResistance));
        errors.RequireShare(rules.ShieldedDamage, nameof(rules.ShieldedDamage));
        HazardRulesValidator.Check(errors, rules);
        GreatEvents.GreatEventRulesValidator.Check(errors, rules.GreatEvents);
        errors.ThrowIfAny("disaster rules", nameof(rules));
    }
}
