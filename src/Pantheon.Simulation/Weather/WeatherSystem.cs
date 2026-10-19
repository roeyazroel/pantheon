using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Weather;

/// <summary>
/// One weather step (spec §16, ADR-013): the spell runs its course, cells take up water over the sea,
/// rain it out over land and drift with the wind, new cells blow in from upwind, the soil of every
/// land region gets wetter or drier, and storm winds ruin stores and building work. The season and a
/// heat or cold wave warm or cool the land, change how often weather comes, let storms grow into
/// hurricanes over warm sea, and turn rain to snow that lies until it melts (ADR-028). It raises
/// <see cref="WeatherSpellChanged"/>, <see cref="StormStruck"/> and <see cref="DroughtChanged"/>.
/// <see cref="IntervalSystem"/> runs it once every <see cref="WeatherRules.StepIntervalTicks"/> ticks.
/// </summary>
/// <remarks>
/// Its random numbers come from a stream derived from the world seed and the tick, not from the
/// shared stream, so weather never shifts the settlements' births and deaths and needs no state of
/// its own in the save.
/// </remarks>
public sealed class WeatherSystem : ISimulationSystem
{
    /// <summary>Purpose name of the weather's random streams.</summary>
    public const string RandomStream = "weather";

    // Cells this dry have rained themselves out and vanish.
    private const float SpentHumidity = 0.05f;

    private readonly WeatherRules _rules;
    private readonly SettlementRules _settlementRules;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="settlementRules">Settlement rules, for the farmland and the drought.</param>
    public WeatherSystem(WeatherRules rules, SettlementRules settlementRules)
    {
        WeatherRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _settlementRules = settlementRules;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        var seconds = WeatherModel.StepSeconds(_rules);

        var windBefore = SettlementWinds(world);
        var droughtBefore = Droughts(world);
        UpdateSpell(world, random, context.Events);
        var climate = ClimateModel.Now(_rules, world);
        var season = ClimateModel.OfSeason(_rules, ClimateModel.SeasonOf(_rules, world));
        UpdateCells(world, seconds, climate.TemperatureShift);
        SpawnCell(world, random, seconds, climate, season, context.Events);
        Rain(world, seconds, climate);
        Storms(world, windBefore, seconds, context.Events);
        ReportDroughts(world, droughtBefore, context.Events);
    }

    /// <summary>Picks the spell after one ends: a dry, wet or another normal spell after a normal one, a normal spell after any other.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="spell">The spell that ends.</param>
    /// <param name="roll">A uniform number in [0, 1).</param>
    /// <returns>The next spell.</returns>
    public static WeatherSpell NextSpell(WeatherRules rules, WeatherSpell spell, double roll)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (spell != WeatherSpell.Normal)
        {
            return WeatherSpell.Normal;
        }

        return roll < rules.DryAfterNormalChance ? WeatherSpell.Dry
            : roll < rules.DryAfterNormalChance + rules.NormalAfterNormalChance ? WeatherSpell.Normal
            : WeatherSpell.Wet;
    }

    /// <summary>Schedules the first spell's end, and starts the next spell when one ends; the prevailing wind turns with it.</summary>
    private void UpdateSpell(WorldState world, SimulationRandom random, List<WorldEvent> events)
    {
        var weather = world.Weather;
        if (weather.SpellEndsTick is not { } ends)
        {
            weather.SpellEndsTick = world.Tick + SpellTicks(weather.Spell, random);
            var start = ClimateModel.OfSeason(_rules, ClimateModel.SeasonOf(_rules, world));
            var shift = ClimateModel.Now(_rules, world).TemperatureShift;
            for (var i = weather.Cells.Count; i < _rules.InitialCells; i++)
            {
                var cell = CreateCell(world, random, scattered: true);
                Strengthen(cell, random, shift, start);
                weather.Cells.Add(cell);
            }

            return;
        }

        if (world.Tick < ends)
        {
            return;
        }

        var previous = weather.Spell;
        var season = ClimateModel.OfSeason(_rules, ClimateModel.SeasonOf(_rules, world));
        weather.Spell = ClimateModel.NextSpell(_rules, season, previous, random.NextDouble(), random.NextDouble());
        weather.SpellEndsTick = world.Tick + SpellTicks(weather.Spell, random);
        var turn = ((random.NextFloat() * 2f) - 1f) * _rules.WindTurnDegrees * (MathF.PI / 180f);
        weather.WindDirection = NormalizeAngle(weather.WindDirection + turn);
        events.Add(new WeatherSpellChanged { Tick = world.Tick, Previous = previous, Spell = weather.Spell, EndsAtTick = weather.SpellEndsTick.Value });
    }

    /// <summary>A random length for a spell, in ticks.</summary>
    private long SpellTicks(WeatherSpell spell, SimulationRandom random)
    {
        var (min, max) = WeatherModel.SpellSeconds(_rules, spell);
        var seconds = min + (random.NextFloat() * (max - min));
        return Math.Max(1L, (long)MathF.Round(seconds * SimulationTime.TicksPerSecond));
    }

    /// <summary>
    /// Every cell rains, takes up water over the sea, fills and thins over land, takes on the ground's
    /// warmth and moves; cells that have thinned away or left the world vanish.
    /// </summary>
    private void UpdateCells(WorldState world, float seconds, float shift)
    {
        var regions = world.Regions;
        var cells = world.Weather.Cells;
        for (var i = cells.Count - 1; i >= 0; i--)
        {
            var cell = cells[i];
            WeatherModel.Refresh(_rules, cell);
            cell.Humidity = Math.Max(0f, cell.Humidity - (_rules.RainDrainPerSecond * cell.Precipitation * seconds));
            if (WeatherModel.IsOverSea(regions, cell.X, cell.Z))
            {
                cell.Humidity = SimMath.Clamp01(cell.Humidity + (_rules.SeaHumidityPerSecond * (1f - cell.Humidity) * seconds));
            }
            else
            {
                var filling = cell.Wind >= _rules.Extremes.HurricaneWind ? _rules.Extremes.HurricaneLandFilling : 1f;
                cell.Pressure = Math.Max(0f, cell.Pressure - (_rules.LandFillingPerSecond * filling * seconds));
                cell.Humidity = Math.Max(0f, cell.Humidity - (_rules.LandDissipationPerSecond * seconds));
            }

            var ground = WeatherModel.GroundTemperature(regions, cell.X, cell.Z, cell.Temperature - shift, shift);
            cell.Temperature = SimMath.Clamp01(cell.Temperature + ((ground - cell.Temperature) * Math.Min(1f, _rules.TemperatureFollowPerSecond * seconds)));
            cell.X += cell.VelocityX * seconds;
            cell.Z += cell.VelocityZ * seconds;
            WeatherModel.Refresh(_rules, cell);
            if (cell.Humidity <= SpentHumidity || HasLeftWorld(regions, cell))
            {
                cells.RemoveAt(i);
            }
        }
    }

    /// <summary>Whether a cell lies wholly off the map and is moving away from it.</summary>
    private static bool HasLeftWorld(RegionGrid regions, WeatherCell cell)
    {
        var width = regions.Columns * regions.RegionSize;
        var depth = regions.Rows * regions.RegionSize;
        var outside = cell.X < -cell.Radius || cell.Z < -cell.Radius || cell.X > width + cell.Radius || cell.Z > depth + cell.Radius;
        var leaving = ((cell.X - (0.5f * width)) * cell.VelocityX) + ((cell.Z - (0.5f * depth)) * cell.VelocityZ) > 0f;
        return outside && leaving;
    }

    /// <summary>
    /// Perhaps lets a new cell blow in at the upwind edge of the map: more often in a wet spell or a
    /// rainy season, rarely in a dry one.
    /// </summary>
    private void SpawnCell(WorldState world, SimulationRandom random, float seconds, ClimateEffects climate, ClimateEffects season, List<WorldEvent> events)
    {
        var weather = world.Weather;
        var chance = _rules.CellsPerSecond * WeatherModel.CellFactor(_rules, weather.Spell) * climate.RainFactor * seconds;
        if (weather.Cells.Count < _rules.MaxCells && random.NextDouble() < chance)
        {
            var cell = CreateCell(world, random, scattered: false);
            Strengthen(cell, random, climate.TemperatureShift, season);
            weather.Cells.Add(cell);
            if (cell.Wind >= _rules.Extremes.HurricaneWind)
            {
                // A hurricane is news before it makes landfall (spec §59, ADR-041).
                events.Add(new HurricaneApproaching { Tick = world.Tick, CellId = cell.Id, X = cell.X, Z = cell.Z });
            }
        }
    }

    /// <summary>
    /// A storm born over sea warm enough grows into a hurricane by the season's share; any other
    /// storm stays below a hurricane's winds (ADR-028).
    /// </summary>
    private void Strengthen(WeatherCell cell, SimulationRandom random, float shift, ClimateEffects season)
    {
        var extremes = _rules.Extremes;
        if (extremes.HurricaneWind >= 1f || cell.Pressure < _rules.StormPressure)
        {
            return;
        }

        if (cell.Temperature + shift >= extremes.HurricaneSeaTemperature && random.NextDouble() < season.HurricaneShare)
        {
            cell.Pressure = Math.Max(cell.Pressure, extremes.HurricanePressure + (random.NextFloat() * (1f - extremes.HurricanePressure)));
            cell.Humidity = 1f;
        }
        else
        {
            cell.Pressure = Math.Min(cell.Pressure, (extremes.HurricaneWind / Math.Max(_rules.WindFromPressure, 1e-3f)) - 0.01f);
        }

        WeatherModel.Refresh(_rules, cell);
    }

    /// <summary>
    /// A new cell moving with the prevailing wind: at the upwind edge of the map, or anywhere over it
    /// when the weather starts. It is a storm by the spell's share.
    /// </summary>
    private WeatherCell CreateCell(WorldState world, SimulationRandom random, bool scattered)
    {
        var weather = world.Weather;
        var regions = world.Regions;
        var half = 0.5f * regions.Columns * regions.RegionSize;
        var (windX, windZ) = WeatherModel.WindVector(weather);
        var radius = _rules.MinRadius + (random.NextFloat() * (_rules.MaxRadius - _rules.MinRadius));
        var across = ((random.NextFloat() * 2f) - 1f) * half;
        var upwind = scattered
            ? ((random.NextFloat() * 2f) - 1f) * half
            : (half / Math.Max(Math.Abs(windX), Math.Abs(windZ))) + radius;
        var x = half - (windX * upwind) - (windZ * across);
        var z = half - (windZ * upwind) + (windX * across);
        var speed = _rules.CellSpeed * (0.8f + (0.4f * random.NextFloat()));
        var drift = _rules.CellSpeed * 0.15f * ((random.NextFloat() * 2f) - 1f);
        var storm = random.NextDouble() < WeatherModel.StormShare(_rules, weather.Spell);
        var pressure = storm
            ? _rules.StormPressure + (random.NextFloat() * (1f - _rules.StormPressure))
            : random.NextFloat() * _rules.ShowerPressure;
        var cell = new WeatherCell
        {
            Id = weather.NextCellId++,
            X = x,
            Z = z,
            VelocityX = (windX * speed) - (windZ * drift),
            VelocityZ = (windZ * speed) + (windX * drift),
            Radius = radius,
            Pressure = pressure,
            Temperature = WeatherModel.GroundTemperature(regions, Math.Clamp(x, 0f, 2f * half), Math.Clamp(z, 0f, 2f * half), 0.5f),
            Humidity = _rules.MinHumidity + (random.NextFloat() * (1f - _rules.MinHumidity)),
            Precipitation = 0f,
            Wind = 0f,
            ElectricalActivity = 0f,
            SummonedByGodId = null,
        };
        WeatherModel.Refresh(_rules, cell);
        return cell;
    }

    /// <summary>
    /// Wets or dries the soil of every land region by the rain over its centre; on land colder than
    /// the snow line the rain lies as snow instead, and snow on warmer land melts into the soil (ADR-028).
    /// </summary>
    private void Rain(WorldState world, float seconds, ClimateEffects climate)
    {
        var regions = world.Regions;
        var extremes = _rules.Extremes;
        foreach (var region in regions.Cells)
        {
            if (region.Biome == Biome.Sea)
            {
                continue;
            }

            var rain = WeatherModel.At(_rules, world.Weather, regions.GetRegionCenter(region.Id)).Rain;
            var warmth = region.Temperature + climate.TemperatureShift;
            if (warmth < extremes.SnowTemperature)
            {
                region.Snow = SimMath.Clamp01(region.Snow + (extremes.SnowPerSecond * rain * seconds));
                rain = 0f;
            }
            else if (region.Snow > 0f && warmth > extremes.MeltTemperature)
            {
                var melt = Math.Min(region.Snow, extremes.MeltPerSecond * (warmth - extremes.MeltTemperature) * seconds);
                region.Snow -= melt;
                region.SoilMoisture = SimMath.Clamp01(region.SoilMoisture + (melt * extremes.MeltWater));
            }

            region.SoilMoisture = WeatherModel.NextSoilMoisture(_rules, region, rain, world.Weather.Spell, seconds, climate);
        }
    }

    /// <summary>Storm winds ruin part of the stores and the building work of every settlement they reach, and are reported as they arrive and again if they grow to a hurricane's.</summary>
    private void Storms(WorldState world, float[] windBefore, float seconds, List<WorldEvent> events)
    {
        for (var i = 0; i < world.Settlements.Count; i++)
        {
            var settlement = world.Settlements[i];
            var weather = WeatherModel.At(_rules, world.Weather, settlement.Position);
            if (weather.Wind < _rules.StormWind)
            {
                continue;
            }

            var severity = (weather.Wind - _rules.StormWind) / (1f - _rules.StormWind);
            settlement.Food *= Math.Max(0f, 1f - (_rules.StormFoodLossPerSecond * severity * seconds));
            settlement.ConstructionProgress *= Math.Max(0f, 1f - (_rules.StormConstructionLossPerSecond * severity * seconds));
            var hurricane = weather.Wind >= _rules.Extremes.HurricaneWind;
            if ((windBefore[i] < _rules.StormWind || (hurricane && windBefore[i] < _rules.Extremes.HurricaneWind)) && weather.CellId is { } cellId)
            {
                events.Add(new StormStruck { Tick = world.Tick, SettlementId = settlement.Id, CellId = cellId, Wind = weather.Wind, Hurricane = hurricane });
            }
        }
    }

    /// <summary>Reports the settlements whose farmland fell into drought or recovered in this step.</summary>
    private void ReportDroughts(WorldState world, float[] droughtBefore, List<WorldEvent> events)
    {
        var after = Droughts(world);
        for (var i = 0; i < after.Length; i++)
        {
            var started = droughtBefore[i] < _settlementRules.DroughtThreshold && after[i] >= _settlementRules.DroughtThreshold;
            var ended = droughtBefore[i] >= _settlementRules.DroughtThreshold && after[i] < _settlementRules.DroughtThreshold;
            if (started || ended)
            {
                events.Add(new DroughtChanged { Tick = world.Tick, SettlementId = world.Settlements[i].Id, Started = started, Drought = after[i] });
            }
        }
    }

    /// <summary>The wind at every settlement, in list order.</summary>
    private float[] SettlementWinds(WorldState world)
    {
        var winds = new float[world.Settlements.Count];
        for (var i = 0; i < winds.Length; i++)
        {
            winds[i] = WeatherModel.At(_rules, world.Weather, world.Settlements[i].Position).Wind;
        }

        return winds;
    }

    /// <summary>The drought of every settlement, in list order.</summary>
    private float[] Droughts(WorldState world)
    {
        var droughts = new float[world.Settlements.Count];
        for (var i = 0; i < droughts.Length; i++)
        {
            droughts[i] = SettlementEconomy.DroughtOf(_settlementRules, world.Regions, world.Settlements[i]);
        }

        return droughts;
    }

    /// <summary>An angle in [0, 2π).</summary>
    private static float NormalizeAngle(float angle)
    {
        var full = 2f * MathF.PI;
        var wrapped = angle % full;
        return wrapped < 0f ? wrapped + full : wrapped;
    }
}
