using System;
using Pantheon.Domain.Weather;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Weather;

/// <summary>Checks <see cref="WeatherRules"/> and lists every problem.</summary>
public static class WeatherRulesValidator
{
    /// <summary>Validates the rules.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more values are invalid.</exception>
    public static void Validate(WeatherRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();

        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireAtLeast(rules.MaxCells, 0, nameof(rules.MaxCells));
        errors.RequireAtLeast(rules.InitialCells, 0, nameof(rules.InitialCells));
        if (rules.InitialCells > rules.MaxCells)
        {
            errors.Add($"{nameof(rules.InitialCells)} must be at most {nameof(rules.MaxCells)}.");
        }

        errors.RequireNonNegative(rules.CellsPerSecond, nameof(rules.CellsPerSecond));
        errors.RequireNonNegative(rules.DrySpellCellFactor, nameof(rules.DrySpellCellFactor));
        errors.RequireNonNegative(rules.WetSpellCellFactor, nameof(rules.WetSpellCellFactor));
        errors.RequireShare(rules.StormShare, nameof(rules.StormShare));
        errors.RequireShare(rules.WetSpellStormShare, nameof(rules.WetSpellStormShare));
        RequireRange(errors, rules.NormalSpellMinSeconds, rules.NormalSpellMaxSeconds, "NormalSpell");
        RequireRange(errors, rules.DrySpellMinSeconds, rules.DrySpellMaxSeconds, "DrySpell");
        RequireRange(errors, rules.WetSpellMinSeconds, rules.WetSpellMaxSeconds, "WetSpell");
        errors.RequireShare(rules.DryAfterNormalChance, nameof(rules.DryAfterNormalChance));
        errors.RequireShare(rules.NormalAfterNormalChance, nameof(rules.NormalAfterNormalChance));
        if (rules.DryAfterNormalChance + rules.NormalAfterNormalChance > 1f)
        {
            errors.Add($"{nameof(rules.DryAfterNormalChance)} and {nameof(rules.NormalAfterNormalChance)} must add up to at most 1.");
        }

        errors.RequireNonNegative(rules.WindTurnDegrees, nameof(rules.WindTurnDegrees));
        errors.RequirePositive(rules.CellSpeed, nameof(rules.CellSpeed));
        errors.RequireShare(rules.SummonedSpeedFactor, nameof(rules.SummonedSpeedFactor));
        errors.RequirePositive(rules.MinRadius, nameof(rules.MinRadius));
        if (rules.MaxRadius < rules.MinRadius)
        {
            errors.Add($"{nameof(rules.MaxRadius)} must be at least {nameof(rules.MinRadius)}.");
        }

        errors.RequireShare(rules.MinHumidity, nameof(rules.MinHumidity));
        errors.RequireShare(rules.ShowerPressure, nameof(rules.ShowerPressure));
        errors.RequireShare(rules.StormPressure, nameof(rules.StormPressure));
        errors.RequireShare(rules.SummonedPressure, nameof(rules.SummonedPressure));
        errors.RequireNonNegative(rules.LandFillingPerSecond, nameof(rules.LandFillingPerSecond));
        errors.RequireShare(rules.SeaHumidityPerSecond, nameof(rules.SeaHumidityPerSecond));
        errors.RequireShare(rules.LandDissipationPerSecond, nameof(rules.LandDissipationPerSecond));
        errors.RequireShare(rules.TemperatureFollowPerSecond, nameof(rules.TemperatureFollowPerSecond));
        if (!(rules.RainHumidity is >= 0f and < 1f))
        {
            errors.Add($"{nameof(rules.RainHumidity)} must be at least 0 and below 1.");
        }

        errors.RequireNonNegative(rules.RainBase, nameof(rules.RainBase));
        errors.RequireNonNegative(rules.RainFromPressure, nameof(rules.RainFromPressure));
        errors.RequireNonNegative(rules.RainDrainPerSecond, nameof(rules.RainDrainPerSecond));
        errors.RequireNonNegative(rules.WindFromPressure, nameof(rules.WindFromPressure));
        errors.RequireNonNegative(rules.LightningFactor, nameof(rules.LightningFactor));
        if (!(rules.StormWind is > 0f and < 1f))
        {
            errors.Add($"{nameof(rules.StormWind)} must be above 0 and below 1.");
        }

        errors.RequireShare(rules.ThunderActivity, nameof(rules.ThunderActivity));
        errors.RequireShare(rules.VisibleRain, nameof(rules.VisibleRain));
        errors.RequireShare(rules.EdgeShare, nameof(rules.EdgeShare));
        errors.RequireNonNegative(rules.RainSoakPerSecond, nameof(rules.RainSoakPerSecond));
        errors.RequireShare(rules.EvaporationPerSecond, nameof(rules.EvaporationPerSecond));
        errors.RequireNonNegative(rules.DrySpellDryingFactor, nameof(rules.DrySpellDryingFactor));
        errors.RequireShare(rules.GroundwaterPerSecond, nameof(rules.GroundwaterPerSecond));
        errors.RequireShare(rules.StormFoodLossPerSecond, nameof(rules.StormFoodLossPerSecond));
        errors.RequireShare(rules.StormConstructionLossPerSecond, nameof(rules.StormConstructionLossPerSecond));
        ClimateRulesValidator.Check(errors, rules);
        errors.ThrowIfAny("weather rules", nameof(rules));
    }

    /// <summary>A length range in seconds: a positive minimum no larger than the maximum.</summary>
    private static void RequireRange(RuleErrors errors, float min, float max, string name)
    {
        errors.RequirePositive(min, name + "MinSeconds");
        if (max < min)
        {
            errors.Add($"{name}MaxSeconds must be at least {name}MinSeconds.");
        }
    }
}
