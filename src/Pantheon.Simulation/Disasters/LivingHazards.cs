using System;
using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// The hazards that move or spread (ADR-029), part of <see cref="HazardSystem"/>: tornadoes that
/// thunderstorms spin down onto warm land, locust swarms rising from hot, dry land and drifting
/// with the wind, and blight spreading through warm, wet fields.
/// </summary>
internal sealed class LivingHazards
{
    private readonly HazardRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly WeatherRules _weather;
    private readonly DisasterDamage _damage;

    /// <summary>Creates the step.</summary>
    public LivingHazards(DisasterRules rules, SettlementRules settlementRules, WeatherRules weather, DisasterDamage damage)
    {
        _rules = rules.Hazards;
        _settlementRules = settlementRules;
        _weather = weather;
        _damage = damage;
    }

    /// <summary>Runs one step of tornadoes, locusts and blight.</summary>
    public void Step(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var warmth = ClimateModel.Now(_weather, world).TemperatureShift;
        Tornadoes(world, random, seconds, warmth, events);
        Swarms(world, random, seconds, warmth, events);
        Blight(world, random, seconds, warmth, events);
    }

    /// <summary>Thunderstorms over warm land spin tornadoes down; each runs its path, tearing down what it crosses, and lifts.</summary>
    private void Tornadoes(WorldState world, SimulationRandom random, float seconds, float warmth, List<WorldEvent> events)
    {
        var rules = _rules.Tornado;
        if (rules.PerStormPerSecond > 0f)
        {
            foreach (var cell in world.Weather.Cells)
            {
                var point = new GroundPosition(cell.X, cell.Z);
                if (WeatherModel.Classify(_weather, cell) == WeatherKind.Thunderstorm && !WeatherModel.IsOverSea(world.Regions, cell.X, cell.Z)
                    && WeatherModel.GroundTemperature(world.Regions, cell.X, cell.Z, 0f, warmth) >= rules.MinTemperature
                    && random.NextDouble() < rules.PerStormPerSecond * seconds)
                {
                    var heading = MathF.Atan2(cell.VelocityZ, cell.VelocityX) + ((random.NextFloat() - 0.5f) * 0.8f);
                    var life = rules.MinSeconds + (random.NextFloat() * (rules.MaxSeconds - rules.MinSeconds));
                    var tornado = HazardModel.SpawnTornado(rules, world, point, heading, life);
                    events.Add(new TornadoTouchedDown { Tick = world.Tick, TornadoId = tornado.Id, Position = point });
                }
            }
        }

        var tornadoes = world.Disasters.Tornadoes;
        for (var i = tornadoes.Count - 1; i >= 0; i--)
        {
            var tornado = tornadoes[i];
            (tornado.X, tornado.Z) = (tornado.X + (tornado.VelocityX * seconds), tornado.Z + (tornado.VelocityZ * seconds));
            if (world.Tick >= tornado.EndsAtTick || WeatherModel.IsOverSea(world.Regions, tornado.X, tornado.Z))
            {
                tornadoes.RemoveAt(i);
                continue;
            }

            var at = new GroundPosition(tornado.X, tornado.Z);
            if (world.Regions.TryGetRegionIdAt(at.X, at.Z, out var regionId))
            {
                var region = world.Regions.Cells[regionId];
                region.Damage = SimMath.Clamp01(region.Damage + (rules.DamagePerSecond * seconds));
            }

            foreach (var settlement in world.Settlements)
            {
                var near = HazardModel.Falloff(settlement.Position, at, rules.Radius) > 0f ? 1f : 0f;
                _damage.Strike(world, settlement, DisasterKind.Tornado, rules.HousingLossPerSecond * seconds, rules.DeathsPerSecond * seconds, near, random, events);
            }
        }
    }

    /// <summary>Locusts rise from hot, dry land, drift with the wind eating the stores of the villages they reach, and die away or drown in rain.</summary>
    private void Swarms(WorldState world, SimulationRandom random, float seconds, float warmth, List<WorldEvent> events)
    {
        var rules = _rules.Locusts;
        if (rules.SwarmsPerHour > 0f && random.NextDouble() < rules.SwarmsPerHour / 3600.0 * seconds)
        {
            var land = world.Regions.Cells.FindAll(r => r.Biome != Biome.Sea && r.SoilMoisture <= rules.MaxSoil && r.Temperature + warmth >= rules.MinTemperature);
            if (land.Count > 0)
            {
                var life = rules.MinSeconds + (random.NextFloat() * (rules.MaxSeconds - rules.MinSeconds));
                var swarm = HazardModel.SpawnSwarm(world, world.Regions.GetRegionCenter(land[random.NextInt(land.Count)].Id), life);
                foreach (var settlement in world.Settlements)
                {
                    // A swarm that rises on a village's fields is upon it at once.
                    if (settlement.Population > 0 && HazardModel.Falloff(settlement.Position, new GroundPosition(swarm.X, swarm.Z), rules.Radius) > 0f)
                    {
                        events.Add(new SwarmArrived { Tick = world.Tick, SettlementId = settlement.Id, SwarmId = swarm.Id });
                    }
                }
            }
        }

        var (windX, windZ) = WeatherModel.WindVector(world.Weather);
        var swarms = world.Disasters.Swarms;
        for (var i = swarms.Count - 1; i >= 0; i--)
        {
            var swarm = swarms[i];
            var before = new GroundPosition(swarm.X, swarm.Z);
            (swarm.X, swarm.Z) = (swarm.X + (windX * rules.Speed * seconds), swarm.Z + (windZ * rules.Speed * seconds));
            var at = new GroundPosition(swarm.X, swarm.Z);
            swarm.Size -= rules.RainLossPerSecond * WeatherModel.At(_weather, world.Weather, at).Rain * seconds;
            var onIsland = world.Regions.TryGetRegionIdAt(at.X, at.Z, out var regionId);
            if (onIsland)
            {
                var region = world.Regions.Cells[regionId];
                swarm.Size -= SimMath.Clamp01(region.Fire) * seconds;
                region.Damage = SimMath.Clamp01(region.Damage + (rules.DamagePerSecond * swarm.Size * seconds));
            }

            if (world.Tick >= swarm.EndsAtTick || swarm.Size <= 0.05f || !onIsland)
            {
                swarms.RemoveAt(i);
                continue;
            }

            foreach (var settlement in world.Settlements)
            {
                if (settlement.Population <= 0 || HazardModel.Falloff(settlement.Position, at, rules.Radius) <= 0f)
                {
                    continue;
                }

                settlement.Food *= Math.Max(0f, 1f - (rules.FoodEatenPerSecond * swarm.Size * seconds * _damage.FoodLoss(settlement)));
                if (HazardModel.Falloff(settlement.Position, before, rules.Radius) <= 0f)
                {
                    events.Add(new SwarmArrived { Tick = world.Tick, SettlementId = settlement.Id, SwarmId = swarm.Id });
                }
            }
        }
    }

    /// <summary>Blight appears and spreads in warm, wet fields, dies away elsewhere, burns with the fields, yields to blessing, and is told as it takes a village's fields.</summary>
    private void Blight(WorldState world, SimulationRandom random, float seconds, float warmth, List<WorldEvent> events)
    {
        var rules = _rules.Blight;
        if (rules.EmergencePerSecond <= 0f && !world.Regions.Cells.Exists(r => r.Blight > 0f))
        {
            return;
        }

        var grid = world.Regions;
        foreach (var region in grid.Cells)
        {
            if (!SettlementEconomy.IsFarmland(region))
            {
                continue;
            }

            var thrives = region.Temperature + warmth >= rules.MinTemperature && region.SoilMoisture >= rules.WetSoil && region.Snow < 0.3f;
            if (region.Blight <= 0f)
            {
                if (thrives && random.NextDouble() < rules.EmergencePerSecond * seconds)
                {
                    region.Blight = 0.05f;
                }

                continue;
            }

            var blight = region.Blight;
            blight += thrives ? rules.GrowthPerSecond * (1f - blight) * seconds : -rules.DeclinePerSecond * blight * seconds;
            blight -= (SimMath.Clamp01(region.Fire) + (rules.BlessingCurePerSecond * SimMath.Clamp01(region.Blessing))) * blight * seconds;
            region.Blight = blight < 0.01f ? 0f : SimMath.Clamp01(blight);
            if (thrives && random.NextDouble() < rules.SpreadPerSecond * region.Blight * seconds)
            {
                Spread(grid, region.Id, random);
            }
        }

        Tell(world, rules, events);
    }

    /// <summary>
    /// Tells a village when the blight in its fields rises to the notice, and again once it has
    /// fallen below half of it, so a blight near the mark is not told over and over.
    /// </summary>
    private void Tell(WorldState world, BlightRules rules, List<WorldEvent> events)
    {
        var told = world.Disasters.BlightedSettlementIds;
        told.RemoveAll(id => !world.Settlements.Exists(s => s.Id == id && s.Population > 0));
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0)
            {
                continue;
            }

            var blight = SettlementEconomy.SurveyFarmland(world.Regions, settlement.RegionId, _settlementRules.FarmlandRadius).MeanBlight;
            var wasTold = told.Contains(settlement.Id);
            if (!wasTold && blight >= rules.Notice)
            {
                told.Add(settlement.Id);
                events.Add(new BlightChanged { Tick = world.Tick, SettlementId = settlement.Id, Started = true, Blight = blight });
            }
            else if (wasTold && blight < rules.Notice * 0.5f)
            {
                told.Remove(settlement.Id);
                events.Add(new BlightChanged { Tick = world.Tick, SettlementId = settlement.Id, Started = false, Blight = blight });
            }
        }
    }

    /// <summary>Blight reaches one neighbouring field, chosen at random.</summary>
    private static void Spread(RegionGrid grid, int regionId, SimulationRandom random)
    {
        var (column, row) = grid.GetCoordinates(regionId);
        var (dx, dz) = random.NextInt(4) switch { 0 => (1, 0), 1 => (-1, 0), 2 => (0, 1), _ => (0, -1) };
        var (c, r) = (column + dx, row + dz);
        if (c >= 0 && r >= 0 && c < grid.Columns && r < grid.Rows && grid.Cells[(r * grid.Columns) + c] is { } next && SettlementEconomy.IsFarmland(next))
        {
            next.Blight = Math.Max(next.Blight, 0.05f);
        }
    }
}
