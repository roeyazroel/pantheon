using Pantheon.Domain.Ecology;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;

namespace Pantheon.Tests.Support;

/// <summary>Seasons, extremes and ecology with short years and plain numbers, for the tests of ADR-028.</summary>
internal static class NatureWorlds
{
    /// <summary><see cref="WeatherWorlds.Rules"/> with ten-second seasons and every extreme.</summary>
    public static WeatherRules Rules { get; } = WeatherWorlds.Rules with
    {
        Seasons = new SeasonRules
        {
            SeasonSeconds = 10f,
            Steady = ClimateEffects.None,
            Spring = ClimateEffects.None,
            Summer = new ClimateEffects { TemperatureShift = 0.2f, YieldFactor = 1.5f, FireFactor = 2f, DryingFactor = 2f, SpoilagePerSecond = 0.01f, HeatWaveChance = 1f },
            Autumn = new ClimateEffects { YieldFactor = 1.2f, HurricaneShare = 1f },
            Winter = new ClimateEffects { TemperatureShift = -0.4f, YieldFactor = 0.25f, FireFactor = 0.2f, HealthLossPerSecond = 0.01f, MigrationFactor = 0.25f, ColdWaveChance = 1f },
        },
        Extremes = new ExtremeRules
        {
            SnowTemperature = 0.3f,
            MeltTemperature = 0.4f,
            SnowPerSecond = 0.1f,
            MeltPerSecond = 0.1f,
            MeltWater = 1f,
            FogHumidity = 0.8f,
            FogPressure = 0.2f,
            FogTemperature = 0.5f,
            HurricanePressure = 0.95f,
            HurricaneWind = 0.9f,
            HurricaneLandFilling = 0.25f,
            HurricaneSeaTemperature = 0.5f,
            HurricaneHouseLossPerSecond = 0.05f,
            HeatWave = new ClimateEffects { TemperatureShift = 0.2f, YieldFactor = 0.5f, FireFactor = 3f, HealthLossPerSecond = 0.02f },
            ColdWave = new ClimateEffects { TemperatureShift = -0.3f, YieldFactor = 0.4f, HealthLossPerSecond = 0.02f },
            WaveMinSeconds = 10f,
            WaveMaxSeconds = 20f,
        },
    };

    /// <summary>An ecology that heals, scars, grows animals and pollutes at rates easy to follow.</summary>
    public static EcologyRules Ecology { get; } = new()
    {
        StepIntervalTicks = 10,
        WildlifeGrowthPerSecond = 0.1f,
        HuntPerHundredPerSecond = 0.1f,
        FoodPerWildlife = 10f,
        FireWildlifeLossPerSecond = 0.5f,
        FireDamagePerSecond = 0.1f,
        FloodDamagePerSecond = 0.05f,
        QuakeDamage = 0.5f,
        HarmDamage = 0.2f,
        HealPerSecond = 0.1f,
        ScarThreshold = 0.5f,
        ScarPerSecond = 0.1f,
        NatureHealFactor = 3f,
        NatureScarHealPerSecond = 0.05f,
        ScarNotice = 0.1f,
        PollutionFreePeople = 100,
        PollutionPerThousandPerSecond = 0.1f,
        PollutionClearPerSecond = 0.01f,
    };

    /// <summary>Gives a world seasons, with a season beginning now.</summary>
    /// <param name="world">The world.</param>
    /// <param name="season">The season.</param>
    public static void Begin(WorldState world, Season season) =>
        world.Seasons = new SeasonState { OriginTick = ClimateModel.OriginFor(Rules, season, world.Tick) };
}
