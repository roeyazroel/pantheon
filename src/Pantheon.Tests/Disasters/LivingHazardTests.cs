using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>The hazards that move or spread (spec §19–20, ADR-029): tornadoes, locusts and blight.</summary>
public sealed class LivingHazardTests
{
    private static readonly HazardRules Hazards = HazardWorlds.Hazards;

    [Fact]
    public void AThunderstormOverWarmLand_SpinsDownTornadoes_ButNotOverColdLandOrTheSea()
    {
        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Tornado = Hazards.Tornado with { PerStormPerSecond = 1f } } };
        int Tornadoes(float temperature, bool sea)
        {
            var world = DivineWorlds.Create(0f, 300f);
            world.Regions.Cells.ForEach(r => (r.Temperature, r.Biome) = (temperature, sea ? Biome.Sea : r.Biome));
            var storm = WeatherWorlds.AddCell(world, new GroundPosition(600f, 600f), 200f, humidity: 0.7f, pressure: 1f);
            Assert.Equal(WeatherKind.Thunderstorm, WeatherModel.Classify(WeatherWorlds.Rules, storm));
            var events = new List<WorldEvent>();
            HazardWorlds.Step(world, rules: rules, events: events);
            Assert.Equal(world.Disasters.Tornadoes.Count, events.OfType<TornadoTouchedDown>().Count());
            return world.Disasters.Tornadoes.Count;
        }

        Assert.Equal(1, Tornadoes(0.9f, sea: false));
        Assert.Equal(0, Tornadoes(0.3f, sea: false));
        Assert.Equal(0, Tornadoes(0.9f, sea: true));
    }

    [Fact]
    public void ATornado_RunsItsPath_TearingDownTheVillageItCrosses_AndLifts()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        village.Housing = 1000;
        var tornado = HazardModel.SpawnTornado(Hazards.Tornado, world, new GroundPosition(260f, 512f), 0f, 6f);
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, steps: 3, events: events);

        Assert.Equal(260f + (3f * Hazards.Tornado.Speed), tornado.X, 3);
        var damage = events.OfType<SettlementDamaged>().Where(d => d.Cause == DisasterKind.Tornado).ToList();
        Assert.NotEmpty(damage);
        Assert.True(damage.Sum(d => d.HousesLost) > 25);
        Assert.True(world.Regions.Cells[village.RegionId].Damage > 0f);

        HazardWorlds.Step(world, steps: 4);
        Assert.Empty(world.Disasters.Tornadoes);
    }

    [Fact]
    public void Locusts_DriftWithTheWind_EatTheStoresOfTheVillagesTheyReach_AndAreToldOnce()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        HazardModel.SpawnSwarm(world, new GroundPosition(40f, 512f), 120f);
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, steps: 30, events: events);

        Assert.Equal(40f + (30f * Hazards.Locusts.Speed), world.Disasters.Swarms[0].X, 2);
        Assert.Equal(village.Id, Assert.Single(events.OfType<SwarmArrived>()).SettlementId);
        Assert.True(village.Food < 400f * 0.95f);
    }

    [Fact]
    public void ASwarmThatRisesOnAVillagesFields_IsToldAtOnce()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells.ForEach(r => (r.Temperature, r.SoilMoisture) = (0.9f, 0f));
        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Locusts = Hazards.Locusts with { SwarmsPerHour = 1e9f, Radius = 5000f } } };
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, rules: rules, events: events);

        Assert.Single(world.Disasters.Swarms);
        Assert.Single(events.OfType<SwarmArrived>());
    }

    [Fact]
    public void Rain_DrownsTheLocusts()
    {
        float SizeAfter(bool raining)
        {
            var world = DivineWorlds.Create(0f, 300f);
            if (raining)
            {
                WeatherWorlds.AddCell(world, new GroundPosition(512f, 512f), 600f, humidity: 1f, pressure: 0.5f);
            }

            var swarm = HazardModel.SpawnSwarm(world, new GroundPosition(500f, 500f), 120f);
            HazardWorlds.Step(world, steps: 5);
            return world.Disasters.Swarms.Count == 0 ? 0f : swarm.Size;
        }

        Assert.Equal(1f, SizeAfter(raining: false));
        Assert.True(SizeAfter(raining: true) < 1f);
    }

    [Fact]
    public void Blight_GrowsAndSpreadsInWarmWetFields_AndWithersTheHarvest()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.6f);
        world.Regions.Cells.ForEach(r => r.SoilMoisture = 0.8f);
        var healthy = SettlementEconomy.SurveyFarmland(world.Regions, SettlementWorlds.CenterRegionId, 2).MeanFertility;
        var centre = world.Regions.Cells[SettlementWorlds.CenterRegionId];
        centre.Blight = 0.5f;
        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Blight = Hazards.Blight with { SpreadPerSecond = 2f } } };

        HazardWorlds.Step(world, steps: 30, rules: rules);

        Assert.True(centre.Blight > 0.55f);
        Assert.True(world.Regions.Cells.Count(r => r.Blight > 0f) > 3);
        Assert.True(SettlementEconomy.SurveyFarmland(world.Regions, SettlementWorlds.CenterRegionId, 2).MeanFertility < healthy);
    }

    [Fact]
    public void Blight_DiesAwayInDryFields_AndFasterUnderFireOrBlessing()
    {
        float BlightAfter(float soil, float fire, float blessing)
        {
            var world = SettlementWorlds.Create(100, 200, 100f, 0.6f);
            var centre = world.Regions.Cells[SettlementWorlds.CenterRegionId];
            (centre.SoilMoisture, centre.Fire, centre.Blessing, centre.Blight) = (soil, fire, blessing, 0.5f);
            HazardWorlds.Step(world, steps: 5);
            return centre.Blight;
        }

        var thriving = BlightAfter(0.8f, 0f, 0f);
        var dry = BlightAfter(0.2f, 0f, 0f);
        var blessed = BlightAfter(0.2f, 0f, 1f);
        var burnt = BlightAfter(0.2f, 1f, 0f);

        Assert.True(thriving > 0.5f && dry < 0.5f);
        Assert.True(blessed < dry);
        Assert.True(burnt < blessed);
    }

    [Fact]
    public void Blight_IsToldWhenItTakesAVillagesFields_AndWhenItLeaves()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.6f);
        world.Regions.Cells.ForEach(r => (r.SoilMoisture, r.Blight) = (0.8f, 0.24f));
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, steps: 5, events: events);

        var started = Assert.Single(events.OfType<BlightChanged>());
        Assert.True(started.Started);
        Assert.True(started.Blight >= Hazards.Blight.Notice);

        world.Regions.Cells.ForEach(r => (r.SoilMoisture, r.Blessing) = (0.2f, 1f));
        HazardWorlds.Step(world, steps: 30, events: events);

        Assert.Equal([true, false], events.OfType<BlightChanged>().Select(b => b.Started));
    }

    [Fact]
    public void Blight_NeverAppearsWhereItIsOff_AndAppearsWhereFieldsAreWarmAndWet()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.6f);
        world.Regions.Cells.ForEach(r => r.SoilMoisture = 0.8f);

        HazardWorlds.Step(world, steps: 50);
        Assert.DoesNotContain(world.Regions.Cells, r => r.Blight > 0f);

        HazardWorlds.Step(world, rules: HazardWorlds.Rules with { Hazards = Hazards with { Blight = Hazards.Blight with { EmergencePerSecond = 1f } } });
        Assert.All(world.Regions.Cells, r => Assert.True(r.Blight > 0f));
    }
}
