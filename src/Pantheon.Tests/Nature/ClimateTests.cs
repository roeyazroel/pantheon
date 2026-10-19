using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Nature;

/// <summary>The seasons and the heat and cold waves (spec §16–17, ADR-028).</summary>
public sealed class ClimateTests
{
    private static readonly WeatherRules Rules = NatureWorlds.Rules;

    [Fact]
    public void TheSeason_FollowsTheTick_FromTheYearsOrigin()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var season = ClimateModel.SeasonTicks(Rules);

        Assert.Null(ClimateModel.SeasonOf(Rules, world));
        Assert.Equal(0, ClimateModel.YearOf(Rules, world));

        world.Seasons = new SeasonState { OriginTick = 0 };
        Assert.Equal(100L, season);
        Assert.Equal(
            [Season.Spring, Season.Summer, Season.Autumn, Season.Winter, Season.Spring],
            new[] { 0L, season, 2 * season, (3 * season) + 99, 4 * season }.Select(t => ClimateModel.SeasonOf(Rules, world, t)));
        world.Tick = (4 * season) + 30;
        Assert.Equal(2, ClimateModel.YearOf(Rules, world));
        Assert.Equal(70L, ClimateModel.TicksLeft(Rules, world));
    }

    [Fact]
    public void ASeason_CanBeMadeToBeginNow_EvenBeforeTheWorldsStart()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        world.Tick = 50;

        NatureWorlds.Begin(world, Season.Winter);

        Assert.Equal(-250L, world.Seasons!.OriginTick);
        Assert.Equal(Season.Winter, ClimateModel.SeasonOf(Rules, world));
        Assert.Equal(Season.Autumn, ClimateModel.SeasonOf(Rules, world, 49));
        Assert.Equal(1, ClimateModel.YearOf(Rules, world));
    }

    [Fact]
    public void ASeasonAndAWave_Combine_ShiftsAndLossesAdding_AndFactorsMultiplying()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        NatureWorlds.Begin(world, Season.Winter);
        world.Weather.Spell = WeatherSpell.ColdWave;

        var climate = ClimateModel.Now(Rules, world);

        Assert.Equal(-0.7f, climate.TemperatureShift, 3);
        Assert.Equal(0.1f, climate.YieldFactor, 3);
        Assert.Equal(0.03f, climate.HealthLossPerSecond, 3);
        Assert.Equal(0.25f, climate.MigrationFactor, 3);
        Assert.Equal(ClimateEffects.None, ClimateModel.OfSpell(Rules, WeatherSpell.Dry));
    }

    [Fact]
    public void Waves_FollowOnlyAnOrdinarySpell_ByTheSeasonsChances()
    {
        var summer = ClimateModel.OfSeason(Rules, Season.Summer);
        var winter = ClimateModel.OfSeason(Rules, Season.Winter);

        Assert.Equal(WeatherSpell.HeatWave, ClimateModel.NextSpell(Rules, summer, WeatherSpell.Normal, 0.5, 0.9));
        Assert.Equal(WeatherSpell.ColdWave, ClimateModel.NextSpell(Rules, winter, WeatherSpell.Normal, 0.5, 0.9));
        Assert.Equal(WeatherSpell.Normal, ClimateModel.NextSpell(Rules, summer, WeatherSpell.HeatWave, 0.0, 0.9));
        Assert.Equal(WeatherSystem.NextSpell(Rules, WeatherSpell.Normal, 0.9), ClimateModel.NextSpell(Rules, ClimateModel.OfSeason(Rules, Season.Spring), WeatherSpell.Normal, 0.0, 0.9));
    }

    [Fact]
    public void TheSeasonStep_TellsOfANewSeasonOnce_SpoilsStoresInSummer_AndSickensInWinter()
    {
        var world = SettlementWorlds.Create(100, 50, 1000f, 0.5f);
        var system = new SeasonSystem(Rules, 10);
        var events = new List<WorldEvent>();
        NatureWorlds.Begin(world, Season.Summer);

        system.Update(new TickContext(world, new SimulationRandom(1), events));
        world.Tick += 10;
        system.Update(new TickContext(world, new SimulationRandom(1), events));

        var season = Assert.Single(events.OfType<SeasonChanged>());
        Assert.Equal((Season.Summer, 1), (season.Season, season.Year));
        Assert.Equal(1000f * 0.99f * 0.99f, SettlementWorlds.Settlement(world).Food, 1);
        Assert.Equal(1f, SettlementWorlds.Settlement(world).Health);

        NatureWorlds.Begin(world, Season.Winter);
        system.Update(new TickContext(world, new SimulationRandom(1), events));

        // Half the people are homeless: the cold takes 0.5 + 0.5 * 0.5 of its toll.
        Assert.Equal(1f - (0.01f * 0.75f), SettlementWorlds.Settlement(world).Health, 4);
    }

    [Fact]
    public void Winter_YieldsLessThanSummer_AndNothingGrowsUnderSnow()
    {
        var world = SettlementWorlds.Create(100, 200, 0f, 0.6f);
        var settlement = SettlementWorlds.Settlement(world);

        NatureWorlds.Begin(world, Season.Summer);
        var summer = SettlementEconomy.Assess(SettlementWorlds.Rules, settlement, world.Regions, ClimateModel.Now(Rules, world).YieldFactor).Production;
        NatureWorlds.Begin(world, Season.Winter);
        var winter = SettlementEconomy.Assess(SettlementWorlds.Rules, settlement, world.Regions, ClimateModel.Now(Rules, world).YieldFactor).Production;
        world.Regions.Cells.ForEach(r => r.Snow = 1f);
        var snowed = SettlementEconomy.Assess(SettlementWorlds.Rules, settlement, world.Regions).Production;

        Assert.Equal(summer * 0.25f / 1.5f, winter, 2);
        Assert.Equal(0f, snowed);
    }

    [Fact]
    public void Snow_LiesOnColdLandUnderRain_AndMeltsIntoTheSoilWhenItWarms()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        world.Weather.SpellEndsTick = 1_000_000;
        world.Regions.Cells.ForEach(r => (r.Temperature, r.SoilMoisture) = (0.6f, 0.3f));
        var centre = world.Regions.GetRegionCenter(SettlementWorlds.Settlement(world).RegionId);
        WeatherWorlds.AddCell(world, centre, 5000f, humidity: 1f, pressure: 0.3f);
        var system = new WeatherSystem(Rules, SettlementWorlds.Rules);
        NatureWorlds.Begin(world, Season.Winter);

        system.Update(new TickContext(world, new SimulationRandom(1), []));
        var region = world.Regions.Cells[SettlementWorlds.Settlement(world).RegionId];

        Assert.True(region.Snow > 0f);
        Assert.Equal(0.3f, region.SoilMoisture, 1);

        world.Weather.Cells.Clear();
        var snow = region.Snow;
        NatureWorlds.Begin(world, Season.Summer);
        system.Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.True(region.Snow < snow);
        Assert.True(region.SoilMoisture > 0.3f);
    }

    [Fact]
    public void Classify_KnowsSnow_Fog_AndHurricanes_OnlyWhereTheRulesHaveThem()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var snow = WeatherWorlds.AddCell(world, new GroundPosition(0f, 0f), 200f, humidity: 1f, pressure: 0.3f);
        snow.Temperature = 0.1f;
        var fog = WeatherWorlds.AddCell(world, new GroundPosition(0f, 0f), 200f, humidity: 0.9f, pressure: 0.1f);
        fog.Temperature = 0.4f;
        var hurricane = WeatherWorlds.AddCell(world, new GroundPosition(0f, 0f), 200f, humidity: 1f, pressure: 0.97f);
        foreach (var cell in world.Weather.Cells)
        {
            WeatherModel.Refresh(Rules, cell);
        }

        Assert.Equal(
            [WeatherKind.Snow, WeatherKind.Fog, WeatherKind.Hurricane],
            world.Weather.Cells.Select(c => WeatherModel.Classify(Rules, c)));
        Assert.DoesNotContain(world.Weather.Cells, c => WeatherModel.Classify(WeatherWorlds.Rules, c) is WeatherKind.Snow or WeatherKind.Fog or WeatherKind.Hurricane);
    }

    [Fact]
    public void AHurricane_IsReportedAsItArrives_AndTearsHousesDown()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        world.Weather.SpellEndsTick = 1_000_000;
        var settlement = SettlementWorlds.Settlement(world);
        // It blows in from just beyond the village and covers it within the step.
        WeatherWorlds.AddCell(world, new GroundPosition(settlement.Position.X - 620f, settlement.Position.Z), 600f, humidity: 1f, pressure: 1f, velocityX: 400f);
        WeatherModel.Refresh(Rules, world.Weather.Cells[0]);
        var events = new List<WorldEvent>();

        new WeatherSystem(Rules, SettlementWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), events));
        var disasters = new Pantheon.Simulation.Disasters.DisasterSystem(DisasterWorlds.Rules with { EarthquakesPerHour = 0f }, SettlementWorlds.Rules, DivineWorlds.Rules, Rules);
        for (var i = 0; i < 20; i++)
        {
            disasters.Update(new TickContext(world, new SimulationRandom(1), events));
            world.Tick += DisasterWorlds.Rules.StepIntervalTicks;
        }

        Assert.True(Assert.Single(events.OfType<StormStruck>()).Hurricane);
        Assert.True(settlement.Housing < 200);
        Assert.Contains(events.OfType<SettlementDamaged>(), d => d.Cause == Domain.Disasters.DisasterKind.Hurricane && d.HousesLost > 0);
    }

    [Fact]
    public void Winter_HoldsPeopleBack_FromSettingOut()
    {
        var urge = new float[2];
        foreach (var (season, i) in new[] { (Season.Summer, 0), (Season.Winter, 1) })
        {
            var world = SettlementWorlds.Create(200, 200, 0f, 0.5f);
            NatureWorlds.Begin(world, season);
            var migration = new Pantheon.Simulation.Migration.MigrationSystem(MigrationWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Rules, weather: Rules);
            migration.Update(new TickContext(world, new SimulationRandom(1), []));
            urge[i] = SettlementWorlds.Settlement(world).Emigration;
        }

        Assert.True(urge[0] > 0f);
        Assert.Equal(urge[0] * 0.25f, urge[1], 3);
    }
}
