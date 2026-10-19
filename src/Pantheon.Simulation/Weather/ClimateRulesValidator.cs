using System;
using Pantheon.Domain.Weather;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Weather;

/// <summary>Checks the seasons and the extremes of <see cref="WeatherRules"/> (ADR-028) into a weather check's errors.</summary>
internal static class ClimateRulesValidator
{
    /// <summary>Adds every problem with the seasons and the extremes.</summary>
    /// <param name="errors">The weather check's errors.</param>
    /// <param name="rules">The rules.</param>
    public static void Check(RuleErrors errors, WeatherRules rules)
    {
        var seasons = rules.Seasons;
        errors.RequirePositive(seasons.SeasonSeconds, "Seasons.SeasonSeconds");
        Check(errors, seasons.Steady, "Seasons.Steady");
        Check(errors, seasons.Spring, "Seasons.Spring");
        Check(errors, seasons.Summer, "Seasons.Summer");
        Check(errors, seasons.Autumn, "Seasons.Autumn");
        Check(errors, seasons.Winter, "Seasons.Winter");

        var extremes = rules.Extremes;
        errors.RequireShare(extremes.SnowTemperature, "Extremes.SnowTemperature");
        errors.RequireShare(extremes.MeltTemperature, "Extremes.MeltTemperature");
        if (extremes.MeltTemperature < extremes.SnowTemperature)
        {
            errors.Add("Extremes.MeltTemperature must be at least Extremes.SnowTemperature.");
        }

        errors.RequireNonNegative(extremes.SnowPerSecond, "Extremes.SnowPerSecond");
        errors.RequireNonNegative(extremes.MeltPerSecond, "Extremes.MeltPerSecond");
        errors.RequireNonNegative(extremes.MeltWater, "Extremes.MeltWater");
        errors.RequireNonNegative(extremes.FogHumidity, "Extremes.FogHumidity");
        errors.RequireShare(extremes.FogPressure, "Extremes.FogPressure");
        errors.RequireShare(extremes.FogTemperature, "Extremes.FogTemperature");
        errors.RequireShare(extremes.HurricanePressure, "Extremes.HurricanePressure");
        errors.RequirePositive(extremes.HurricaneWind, "Extremes.HurricaneWind");
        if (extremes.HurricaneWind < 1f && extremes.HurricaneWind <= rules.StormWind)
        {
            errors.Add("Extremes.HurricaneWind must be above StormWind.");
        }

        errors.RequireNonNegative(extremes.HurricaneLandFilling, "Extremes.HurricaneLandFilling");
        errors.RequireNonNegative(extremes.HurricaneSeaTemperature, "Extremes.HurricaneSeaTemperature");
        errors.RequireShare(extremes.HurricaneHouseLossPerSecond, "Extremes.HurricaneHouseLossPerSecond");
        Check(errors, extremes.HeatWave, "Extremes.HeatWave");
        Check(errors, extremes.ColdWave, "Extremes.ColdWave");
        errors.RequirePositive(extremes.WaveMinSeconds, "Extremes.WaveMinSeconds");
        if (extremes.WaveMaxSeconds < extremes.WaveMinSeconds)
        {
            errors.Add("Extremes.WaveMaxSeconds must be at least Extremes.WaveMinSeconds.");
        }
    }

    /// <summary>Adds every problem with one set of effects.</summary>
    private static void Check(RuleErrors errors, ClimateEffects effects, string name)
    {
        ArgumentNullException.ThrowIfNull(effects);
        errors.RequireSignedShare(effects.TemperatureShift, name + ".TemperatureShift");
        errors.RequireNonNegative(effects.RainFactor, name + ".RainFactor");
        errors.RequireNonNegative(effects.YieldFactor, name + ".YieldFactor");
        errors.RequireNonNegative(effects.FireFactor, name + ".FireFactor");
        errors.RequireNonNegative(effects.DryingFactor, name + ".DryingFactor");
        errors.RequireShare(effects.SpoilagePerSecond, name + ".SpoilagePerSecond");
        errors.RequireShare(effects.HealthLossPerSecond, name + ".HealthLossPerSecond");
        errors.RequireNonNegative(effects.MigrationFactor, name + ".MigrationFactor");
        errors.RequireShare(effects.HeatWaveChance, name + ".HeatWaveChance");
        errors.RequireShare(effects.ColdWaveChance, name + ".ColdWaveChance");
        errors.RequireShare(effects.HurricaneShare, name + ".HurricaneShare");
        if (effects.HeatWaveChance + effects.ColdWaveChance > 1f)
        {
            errors.Add($"{name}.HeatWaveChance and {name}.ColdWaveChance must add up to at most 1.");
        }
    }
}
