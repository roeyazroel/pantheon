using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Weather;

/// <summary>
/// The rules of weather and soil water as pure functions (spec §16, ADR-013). <see cref="WeatherSystem"/>
/// applies them once per weather step; readouts, overlays and the Rain miracle use them too.
/// </summary>
public static class WeatherModel
{
    /// <summary>Simulated seconds one weather step covers.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <returns>Seconds.</returns>
    public static float StepSeconds(WeatherRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
    }

    /// <summary>
    /// How hard a cell rains: nothing below <see cref="WeatherRules.RainHumidity"/>, then rising with
    /// the water above it and with the depth of the low.
    /// </summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="humidity">The cell's humidity (0..1).</param>
    /// <param name="pressure">The cell's pressure (0..1).</param>
    /// <returns>Rain (0..1).</returns>
    public static float ComputePrecipitation(WeatherRules rules, float humidity, float pressure)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var wetness = SimMath.Clamp01((humidity - rules.RainHumidity) / (1f - rules.RainHumidity));
        return SimMath.Clamp01(wetness * (rules.RainBase + (rules.RainFromPressure * SimMath.Clamp01(pressure))));
    }

    /// <summary>Wind beneath a cell: deeper lows blow harder.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="pressure">The cell's pressure (0..1).</param>
    /// <returns>Wind (0..1).</returns>
    public static float ComputeWind(WeatherRules rules, float pressure)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return SimMath.Clamp01(rules.WindFromPressure * SimMath.Clamp01(pressure));
    }

    /// <summary>Lightning in a cell: deep, wet and warm lows make thunderstorms.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="pressure">The cell's pressure (0..1).</param>
    /// <param name="precipitation">Its rain (0..1).</param>
    /// <param name="temperature">Its warmth (0..1).</param>
    /// <returns>Lightning activity (0..1).</returns>
    public static float ComputeLightning(WeatherRules rules, float pressure, float precipitation, float temperature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return SimMath.Clamp01(rules.LightningFactor * SimMath.Clamp01(pressure) * SimMath.Clamp01(precipitation) * SimMath.Clamp01(temperature));
    }

    /// <summary>Recomputes a cell's rain, wind and lightning from its pressure, humidity and temperature.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="cell">The cell to update.</param>
    public static void Refresh(WeatherRules rules, WeatherCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        cell.Precipitation = ComputePrecipitation(rules, cell.Humidity, cell.Pressure);
        cell.Wind = ComputeWind(rules, cell.Pressure);
        cell.ElectricalActivity = ComputeLightning(rules, cell.Pressure, cell.Precipitation, cell.Temperature);
    }

    /// <summary>What the weather beneath a cell is like: a thunderstorm, a windstorm, rain or clear.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="cell">The cell.</param>
    /// <returns>Its kind.</returns>
    public static WeatherKind Classify(WeatherRules rules, WeatherCell cell)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(cell);
        return cell.ElectricalActivity >= rules.ThunderActivity ? WeatherKind.Thunderstorm
            : cell.Wind >= rules.StormWind ? WeatherKind.Windstorm
            : cell.Precipitation >= rules.VisibleRain ? WeatherKind.Rain
            : WeatherKind.Clear;
    }

    /// <summary>How fully a cell covers a point: 1 well inside, fading to 0 over its edge.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="point">The point.</param>
    /// <returns>Coverage (0..1).</returns>
    public static float Coverage(WeatherRules rules, WeatherCell cell, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(cell);
        var dx = point.X - cell.X;
        var dz = point.Z - cell.Z;
        var distance = MathF.Sqrt((dx * dx) + (dz * dz));
        var edge = Math.Max(1f, rules.EdgeShare * cell.Radius);
        return SimMath.Clamp01((cell.Radius - distance) / edge);
    }

    /// <summary>The weather at a point: the rain of every cell over it, the strongest wind and lightning, and the cell that brings most of it.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="weather">The weather.</param>
    /// <param name="point">The point.</param>
    /// <returns>The weather there.</returns>
    public static PointWeather At(WeatherRules rules, WeatherState weather, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(weather);
        var rain = 0f;
        var wind = 0f;
        var lightning = 0f;
        int? strongest = null;
        var strongestShare = 0f;
        foreach (var cell in weather.Cells)
        {
            var coverage = Coverage(rules, cell, point);
            if (coverage <= 0f)
            {
                continue;
            }

            rain += cell.Precipitation * coverage;
            wind = Math.Max(wind, cell.Wind * coverage);
            lightning = Math.Max(lightning, cell.ElectricalActivity * coverage);
            var share = Math.Max(cell.Precipitation, cell.Wind) * coverage;
            if (strongest is null || share > strongestShare)
            {
                strongest = cell.Id;
                strongestShare = share;
            }
        }

        return new PointWeather(SimMath.Clamp01(rain), wind, lightning, strongest);
    }

    /// <summary>
    /// Water in a region's soil after some seconds: rain soaks in where there is room, warmth dries it
    /// (faster in a dry spell), and springs refill it in proportion to the region's climate moisture.
    /// </summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="region">The region.</param>
    /// <param name="rain">Rain over it (0..1).</param>
    /// <param name="spell">The spell; the soil dries faster in a dry one.</param>
    /// <param name="seconds">Simulated seconds.</param>
    /// <returns>Soil moisture (0..1).</returns>
    public static float NextSoilMoisture(WeatherRules rules, RegionState region, float rain, WeatherSpell spell, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(region);
        var soil = SimMath.Clamp01(region.SoilMoisture);
        var room = 1f - soil;
        var soak = rules.RainSoakPerSecond * SimMath.Clamp01(rain) * room;
        var heat = spell == WeatherSpell.Dry ? rules.DrySpellDryingFactor : 1f;
        var drying = rules.EvaporationPerSecond * heat * (0.5f + SimMath.Clamp01(region.Temperature)) * soil;
        var springs = rules.GroundwaterPerSecond * SimMath.Clamp01(region.Moisture) * room;
        return SimMath.Clamp01(soil + ((soak - drying + springs) * seconds));
    }

    /// <summary>How long a spell lasts at least and at most, in simulated seconds.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>Shortest and longest length.</returns>
    public static (float Min, float Max) SpellSeconds(WeatherRules rules, WeatherSpell spell)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return spell switch
        {
            WeatherSpell.Normal => (rules.NormalSpellMinSeconds, rules.NormalSpellMaxSeconds),
            WeatherSpell.Dry => (rules.DrySpellMinSeconds, rules.DrySpellMaxSeconds),
            WeatherSpell.Wet => (rules.WetSpellMinSeconds, rules.WetSpellMaxSeconds),
        };
    }

    /// <summary>Factor on the arrival of new cells in a spell.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>The factor.</returns>
    public static float CellFactor(WeatherRules rules, WeatherSpell spell)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return spell switch
        {
            WeatherSpell.Normal => 1f,
            WeatherSpell.Dry => rules.DrySpellCellFactor,
            WeatherSpell.Wet => rules.WetSpellCellFactor,
        };
    }

    /// <summary>Share of new cells that are storms in a spell.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="spell">The spell.</param>
    /// <returns>The share (0..1).</returns>
    public static float StormShare(WeatherRules rules, WeatherSpell spell)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return spell switch
        {
            WeatherSpell.Normal or WeatherSpell.Dry => rules.StormShare,
            WeatherSpell.Wet => rules.WetSpellStormShare,
        };
    }

    /// <summary>Unit vector of the prevailing wind.</summary>
    /// <param name="weather">The weather.</param>
    /// <returns>The direction the wind blows towards.</returns>
    public static (float X, float Z) WindVector(WeatherState weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        return (MathF.Cos(weather.WindDirection), MathF.Sin(weather.WindDirection));
    }

    /// <summary>Whether a point lies over the sea, or off the map, where cells take up water.</summary>
    /// <param name="regions">Region grid.</param>
    /// <param name="x">East-west position in metres.</param>
    /// <param name="z">North-south position in metres.</param>
    /// <returns><c>true</c> over the sea or beyond the grid.</returns>
    public static bool IsOverSea(RegionGrid regions, float x, float z)
    {
        ArgumentNullException.ThrowIfNull(regions);
        return !regions.TryGetRegionIdAt(x, z, out var id) || regions.Cells[id].Biome == Biome.Sea;
    }

    /// <summary>
    /// Calls a rain cell over a point for a god (the Rain miracle): a gentle low of the given radius
    /// and humidity, drifting slowly with the prevailing wind so it rains where it was called.
    /// </summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="target">Where the rain falls.</param>
    /// <param name="radius">Radius in metres.</param>
    /// <param name="humidity">Water it carries (0..1).</param>
    /// <param name="godId">The god who called it.</param>
    /// <returns>The new cell, already added to the weather.</returns>
    public static WeatherCell Summon(WeatherRules rules, WorldState world, GroundPosition target, float radius, float humidity, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var weather = world.Weather;
        var (windX, windZ) = WindVector(weather);
        var speed = rules.CellSpeed * rules.SummonedSpeedFactor;
        var cell = new WeatherCell
        {
            Id = weather.NextCellId++,
            X = target.X,
            Z = target.Z,
            VelocityX = windX * speed,
            VelocityZ = windZ * speed,
            Radius = radius,
            Pressure = rules.SummonedPressure,
            Temperature = GroundTemperature(world.Regions, target.X, target.Z, 0.5f),
            Humidity = SimMath.Clamp01(humidity),
            Precipitation = 0f,
            Wind = 0f,
            ElectricalActivity = 0f,
            SummonedByGodId = godId,
        };
        Refresh(rules, cell);
        weather.Cells.Add(cell);
        return cell;
    }

    /// <summary>Temperature of the region under a point, or a fallback beyond the grid.</summary>
    /// <param name="regions">Region grid.</param>
    /// <param name="x">East-west position in metres.</param>
    /// <param name="z">North-south position in metres.</param>
    /// <param name="fallback">Temperature beyond the grid.</param>
    /// <returns>Temperature (0..1).</returns>
    public static float GroundTemperature(RegionGrid regions, float x, float z, float fallback)
    {
        ArgumentNullException.ThrowIfNull(regions);
        return regions.TryGetRegionIdAt(x, z, out var id) ? SimMath.Clamp01(regions.Cells[id].Temperature) : fallback;
    }
}
