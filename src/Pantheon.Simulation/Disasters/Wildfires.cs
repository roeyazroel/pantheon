using System;
using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Weather;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// One step of the wildfires (spec §19, ADR-014): thunderstorms light dry land, fires grow on their
/// fuel and scorch it, spread to neighbouring regions (more readily downwind), and die out in the
/// rain, under settlements' firefighting or for want of fuel. Scorched land regrows.
/// </summary>
internal sealed class Wildfires
{
    // A fire this weak goes out.
    private const float Embers = 0.02f;

    // Neighbours along a diagonal are this much less likely to catch.
    private const float DiagonalSpread = 0.7f;

    // Wind that blows even under a clear sky, as a share of a storm's.
    private const float BreezeWind = 0.3f;

    private readonly DisasterRules _rules;
    private readonly WeatherRules _weather;

    // The season's and a heat or cold wave's factor on how readily the land catches (ADR-028), for this step.
    private float _season = 1f;

    /// <summary>Creates the fire step.</summary>
    /// <param name="rules">Disaster rules.</param>
    /// <param name="weather">Weather rules, for the rain and the wind over a region.</param>
    public Wildfires(DisasterRules rules, WeatherRules weather)
    {
        _rules = rules;
        _weather = weather;
    }

    /// <summary>Runs one step: lightning, burning, spreading and regrowth.</summary>
    /// <param name="world">World state.</param>
    /// <param name="random">This step's random stream.</param>
    /// <param name="seconds">Simulated seconds the step covers.</param>
    /// <param name="events">Receives <see cref="WildfireStarted"/> for fires lit by lightning.</param>
    public void Step(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        _season = Weather.ClimateModel.Now(_weather, world).FireFactor;
        Lightning(world, random, seconds, events);
        Hearths(world, random, seconds, events);
        var burning = new List<(int RegionId, float Fire)>();
        foreach (var region in world.Regions.Cells)
        {
            if (region.Fire > 0f)
            {
                burning.Add((region.Id, region.Fire));
            }
        }

        foreach (var (regionId, fire) in burning)
        {
            Spread(world, regionId, fire, random, seconds);
        }

        foreach (var (regionId, _) in burning)
        {
            Burn(world, world.Regions.Cells[regionId], seconds);
        }

        foreach (var region in world.Regions.Cells)
        {
            if (region.Fire <= 0f && region.Scorch > 0f)
            {
                region.Scorch = Math.Max(0f, region.Scorch - (_rules.ScorchRecoveryPerSecond * region.Scorch * seconds));
            }
        }
    }

    /// <summary>Thunderstorms may light every dry, vegetated region beneath them.</summary>
    private void Lightning(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        foreach (var cell in world.Weather.Cells)
        {
            if (cell.ElectricalActivity < _weather.ThunderActivity)
            {
                continue;
            }

            foreach (var region in world.Regions.Cells)
            {
                if (region.Fire > 0f)
                {
                    continue;
                }

                var coverage = WeatherModel.Coverage(_weather, cell, world.Regions.GetRegionCenter(region.Id));
                var flammability = coverage > 0f ? DisasterModel.Flammability(_rules, region) * _season : 0f;
                var chance = _rules.LightningIgnitionPerSecond * cell.ElectricalActivity * coverage * flammability * seconds;
                if (chance > 0f && random.NextDouble() < chance && DisasterModel.Ignite(_rules, region, _rules.IgnitionIntensity))
                {
                    events.Add(new WildfireStarted { Tick = world.Tick, RegionId = region.Id, Cause = FireCause.Lightning });
                }
            }
        }
    }

    /// <summary>A spark from a settlement's hearths may set one of the fields around it alight when the land is dry.</summary>
    private void Hearths(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var grid = world.Regions;
        foreach (var settlement in world.Settlements)
        {
            var (column, row) = grid.GetCoordinates(settlement.RegionId);
            var c = Math.Clamp(column + random.NextInt(-1, 2), 0, grid.Columns - 1);
            var r = Math.Clamp(row + random.NextInt(-1, 2), 0, grid.Rows - 1);
            var region = grid.Cells[(r * grid.Columns) + c];
            var chance = _rules.HearthFirePerSecond * Math.Max(0, settlement.Population) / 100f * DisasterModel.Flammability(_rules, region) * _season * seconds;
            if (region.Fire <= 0f && chance > 0f && random.NextDouble() < chance && DisasterModel.Ignite(_rules, region, _rules.IgnitionIntensity))
            {
                events.Add(new WildfireStarted { Tick = world.Tick, RegionId = region.Id, Cause = FireCause.Hearth });
            }
        }
    }

    /// <summary>A burning region may set each unburnt neighbour alight: fiercer fires, drier fuel and the wind behind it make it likelier.</summary>
    private void Spread(WorldState world, int regionId, float fire, SimulationRandom random, float seconds)
    {
        var grid = world.Regions;
        var (column, row) = grid.GetCoordinates(regionId);
        var centre = grid.GetRegionCenter(regionId);
        var wind = Math.Max(BreezeWind, WeatherModel.At(_weather, world.Weather, centre).Wind);
        var (windX, windZ) = WeatherModel.WindVector(world.Weather);
        for (var dr = -1; dr <= 1; dr++)
        {
            for (var dc = -1; dc <= 1; dc++)
            {
                var r = row + dr;
                var c = column + dc;
                if ((dr == 0 && dc == 0) || r < 0 || c < 0 || r >= grid.Rows || c >= grid.Columns)
                {
                    continue;
                }

                var neighbour = grid.Cells[(r * grid.Columns) + c];
                if (neighbour.Fire > 0f)
                {
                    continue;
                }

                var length = MathF.Sqrt((dr * dr) + (dc * dc));
                var alignment = ((dc * windX) + (dr * windZ)) / length;
                var downwind = 1f + (_rules.WindSpread * wind * Math.Max(0f, alignment));
                var diagonal = dr != 0 && dc != 0 ? DiagonalSpread : 1f;
                var chance = _rules.SpreadPerSecond * fire * DisasterModel.Flammability(_rules, neighbour) * _season * downwind * diagonal * seconds;
                if (chance > 0f && random.NextDouble() < chance)
                {
                    DisasterModel.Ignite(_rules, neighbour, _rules.IgnitionIntensity);
                }
            }
        }
    }

    /// <summary>A fire grows towards what its fuel allows, scorches the land, and is put out by rain and firefighting.</summary>
    private void Burn(WorldState world, RegionState region, float seconds)
    {
        var fuel = Math.Min(1f, _rules.FuelHeat * DisasterModel.Flammability(_rules, region));
        var rain = WeatherModel.At(_weather, world.Weather, world.Regions.GetRegionCenter(region.Id)).Rain;
        var fighting = Firefighting(world, region.Id);
        var fire = region.Fire
            + (_rules.FireGrowthPerSecond * (fuel - region.Fire) * seconds)
            - (_rules.RainQuenchPerSecond * rain * seconds)
            - (fighting * seconds);
        region.Scorch = SimMath.Clamp01(region.Scorch + (_rules.BurnPerSecond * region.Fire * seconds));
        region.Fire = fire < Embers ? 0f : SimMath.Clamp01(fire);
    }

    /// <summary>How much fire the settlements in a region put out per second: more people, more buckets.</summary>
    private float Firefighting(WorldState world, int regionId)
    {
        var fighting = 0f;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.RegionId == regionId)
            {
                fighting += _rules.FirefightingPerSecond * Math.Max(0, settlement.Population) / 100f;
            }
        }

        return fighting;
    }
}
