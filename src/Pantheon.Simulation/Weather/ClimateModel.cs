using System;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Weather;

/// <summary>
/// The seasons and the heat and cold waves as pure functions (spec §16–17, ADR-028): which season it
/// is, and what the season and the spell together do to temperature, rain, harvests, fire, stores,
/// health and the wish to leave. The season follows from the tick, so it needs no state of its own
/// beyond when the year began.
/// </summary>
public static class ClimateModel
{
    /// <summary>Ticks a season lasts.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <returns>At least one tick.</returns>
    public static long SeasonTicks(WeatherRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Math.Max(1L, (long)MathF.Round(rules.Seasons.SeasonSeconds * SimulationTime.TicksPerSecond));
    }

    /// <summary>The season at a tick, or <c>null</c> in a world without seasons.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="tick">The tick; the world's now when <c>null</c>.</param>
    /// <returns>The season.</returns>
    public static Season? SeasonOf(WeatherRules rules, WorldState world, long? tick = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Seasons is { } seasons ? (Season)(int)(((Index(rules, seasons, tick ?? world.Tick) % 4) + 4) % 4) : null;
    }

    /// <summary>The year at a tick, counting from 1, in a world with seasons; 0 without.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>The year.</returns>
    public static int YearOf(WeatherRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Seasons is { } seasons ? (int)Math.Floor(Index(rules, seasons, world.Tick) / 4.0) + 1 : 0;
    }

    /// <summary>Ticks left in the season, or 0 in a world without seasons.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>Ticks.</returns>
    public static long TicksLeft(WeatherRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Seasons is not { } seasons)
        {
            return 0L;
        }

        var length = SeasonTicks(rules);
        var into = (world.Tick - seasons.OriginTick) % length;
        return length - (into < 0 ? into + length : into);
    }

    /// <summary>The year's origin that makes a season begin at a tick.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="season">The season that begins.</param>
    /// <param name="tick">When it begins.</param>
    /// <returns>The origin tick.</returns>
    public static long OriginFor(WeatherRules rules, Season season, long tick) => tick - ((int)season * SeasonTicks(rules));

    /// <summary>What a season does.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="season">The season, or <c>null</c> for a world without seasons.</param>
    /// <returns>Its effects.</returns>
    public static ClimateEffects OfSeason(WeatherRules rules, Season? season)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var seasons = rules.Seasons;
        return season is not { } now ? seasons.Steady : now switch
        {
            Season.Spring => seasons.Spring,
            Season.Summer => seasons.Summer,
            Season.Autumn => seasons.Autumn,
            Season.Winter => seasons.Winter,
        };
    }

    /// <summary>What a spell does beyond the ordinary: a heat or a cold wave, or nothing.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>Its effects.</returns>
    public static ClimateEffects OfSpell(WeatherRules rules, WeatherSpell spell)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return spell switch
        {
            WeatherSpell.HeatWave => rules.Extremes.HeatWave,
            WeatherSpell.ColdWave => rules.Extremes.ColdWave,
            WeatherSpell.Normal or WeatherSpell.Dry or WeatherSpell.Wet => ClimateEffects.None,
        };
    }

    /// <summary>What the season and the spell do together now: shifts and losses add, factors multiply.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <returns>The combined effects.</returns>
    public static ClimateEffects Now(WeatherRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Combine(OfSeason(rules, SeasonOf(rules, world)), OfSpell(rules, world.Weather.Spell));
    }

    /// <summary>Two effects together.</summary>
    /// <param name="a">One.</param>
    /// <param name="b">The other.</param>
    /// <returns>Shifts and losses added, factors multiplied, chances of the first.</returns>
    public static ClimateEffects Combine(ClimateEffects a, ClimateEffects b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a with
        {
            TemperatureShift = a.TemperatureShift + b.TemperatureShift,
            RainFactor = a.RainFactor * b.RainFactor,
            YieldFactor = a.YieldFactor * b.YieldFactor,
            FireFactor = a.FireFactor * b.FireFactor,
            DryingFactor = a.DryingFactor * b.DryingFactor,
            SpoilagePerSecond = a.SpoilagePerSecond + b.SpoilagePerSecond,
            HealthLossPerSecond = a.HealthLossPerSecond + b.HealthLossPerSecond,
            MigrationFactor = a.MigrationFactor * b.MigrationFactor,
        };
    }

    /// <summary>
    /// The spell after an ordinary one: in a season (or year-round) that allows it, a heat or a cold
    /// wave by its chance; otherwise <see cref="WeatherSystem.NextSpell"/>'s dry, normal or wet.
    /// </summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="season">The season's effects.</param>
    /// <param name="spell">The spell that ends.</param>
    /// <param name="waveRoll">A uniform number in [0, 1) for a wave.</param>
    /// <param name="roll">A uniform number in [0, 1) for the rest.</param>
    /// <returns>The next spell.</returns>
    public static WeatherSpell NextSpell(WeatherRules rules, ClimateEffects season, WeatherSpell spell, double waveRoll, double roll)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (spell == WeatherSpell.Normal)
        {
            if (waveRoll < season.HeatWaveChance)
            {
                return WeatherSpell.HeatWave;
            }

            if (waveRoll < season.HeatWaveChance + season.ColdWaveChance)
            {
                return WeatherSpell.ColdWave;
            }
        }

        return WeatherSystem.NextSpell(rules, spell, roll);
    }

    /// <summary>A season's index since the year's origin (spring of the first year is 0, the winter before it -1).</summary>
    private static long Index(WeatherRules rules, SeasonState seasons, long tick) =>
        (long)Math.Floor((tick - seasons.OriginTick) / (double)SeasonTicks(rules));
}
