using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Persistence;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>The hazard step (spec §19–21 and §64, ADR-029): waves, the volcano, landslides and meteors, and the preparations against them.</summary>
public sealed class HazardSystemTests
{
    private static readonly HazardRules Hazards = HazardWorlds.Hazards;

    [Fact]
    public void AStrongQuakeByTheCoast_CracksTheLand_AndRaisesAWaveThatStrikesTheVillage()
    {
        var world = DivineWorlds.Create(0f, 200f);
        HazardWorlds.Coast(world, 2);
        world.Settlements[0].Housing = 1000;
        DisasterModel.StartQuake(HazardWorlds.Rules, world.Disasters, new GroundPosition(200f, 512f), 7f, world.Tick, godId: null);
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, events: events);
        HazardWorlds.Step(world, events: events);

        Assert.Single(events.OfType<TsunamiStruck>());
        var damage = Assert.Single(events.OfType<SettlementDamaged>());
        Assert.Equal(DisasterKind.Tsunami, damage.Cause);
        Assert.True(damage.HousesLost > 0);
        Assert.Equal(TerrainMarkKind.Crack, Assert.Single(world.Disasters.Marks).Kind);
    }

    [Fact]
    public void ASeaWall_SparesAVillageMostOfTheWave()
    {
        int HousesLost(bool walled)
        {
            var world = DivineWorlds.Create(0f, 200f);
            HazardWorlds.Coast(world, 2);
            world.Settlements[0].Housing = 2000;
            if (walled)
            {
                HazardWorlds.Build(world, 0, BuildingKind.SeaWall);
            }

            DisasterModel.StartQuake(HazardWorlds.Rules, world.Disasters, new GroundPosition(200f, 512f), 7.5f, world.Tick, godId: null);
            var events = new List<WorldEvent>();
            HazardWorlds.Step(world, events: events);
            return events.OfType<SettlementDamaged>().Sum(d => d.HousesLost);
        }

        var open = HousesLost(walled: false);
        Assert.True(open > 20);
        Assert.InRange(HousesLost(walled: true), open * 0.25, open * 0.55);
    }

    [Fact]
    public void TheVolcano_Rumbles_ShakesTheGround_Erupts_AndFallsQuiet()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        HazardWorlds.Mountain(world, 100f);
        world.Regions.Cells[(8 * world.Regions.Columns) + world.Regions.Columns - 1].Elevation = 120f;
        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Volcano = Hazards.Volcano with { EruptionsPerHour = 100_000f } } };
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, steps: (int)(Hazards.Volcano.RumbleSeconds + Hazards.Volcano.EruptSeconds) + 3, rules: rules, events: events);

        Assert.Equal(
            [VolcanoPhase.Rumbling, VolcanoPhase.Erupting, VolcanoPhase.Dormant],
            events.OfType<VolcanoChanged>().Select(v => v.Phase));
        Assert.InRange(events.OfType<EarthquakeStruck>().Count(), 4, 6);
        var volcano = world.Disasters.Volcano!;
        Assert.Equal(VolcanoPhase.Dormant, volcano.Phase);
        Assert.True(volcano.PhaseEndsTick > world.Tick + 10_000);
        foreach (var regionId in HazardModel.LavaPath(world.Regions, volcano.RegionId, Hazards.Volcano.LavaRegions))
        {
            Assert.Equal((1f, 1f, 0f), (world.Regions.Cells[regionId].Scorch, world.Regions.Cells[regionId].Damage, world.Regions.Cells[regionId].Wildlife));
        }

        var nearCrater = world.Settlements[1];
        Assert.True(nearCrater.Health < 1f);
        Assert.True(nearCrater.Food < 400f);
        Assert.Equal(1f, world.Settlements[0].Health);
    }

    [Fact]
    public void ASoakedSteepSlope_Slides_BuryingTheVillageBelow_AndTheRubbleClears()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(world, 300f);
        world.Settlements[0].Housing = 1000;
        var settlementColumn = world.Regions.GetCoordinates(world.Settlements[0].RegionId).Column;
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = world.Regions.GetCoordinates(region.Id).Column == settlementColumn + 1 ? 1f : 0f;
        }

        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Landslide = Hazards.Landslide with { SlidePerSecond = 1f } } };
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, rules: rules, events: events);

        Assert.Contains(events.OfType<LandslideStruck>(), s => s.ToRegionId == world.Settlements[0].RegionId);
        Assert.Contains(events.OfType<SettlementDamaged>(), d => d.Cause == DisasterKind.Landslide && d.HousesLost > 0);
        var buried = world.Regions.Cells[world.Settlements[0].RegionId];
        Assert.False(RegionPathfinder.IsWalkable(buried));

        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0f;
        }

        HazardWorlds.Step(world, steps: (int)(0.5f / Hazards.Landslide.RubbleClearPerSecond) + 2, rules: rules);
        Assert.True(RegionPathfinder.IsWalkable(buried));
    }

    [Fact]
    public void AnEarthquake_ShakesSteepSlopesLoose()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardWorlds.Mountain(world, 300f);
        world.Regions.Cells.ForEach(r => r.SoilMoisture = 0f);
        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Landslide = Hazards.Landslide with { ShakeChance = 1f } } };
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, rules: rules, events: events);
        Assert.Empty(events.OfType<LandslideStruck>());

        DisasterModel.StartQuake(rules, world.Disasters, new GroundPosition(600f, 512f), 7f, world.Tick, godId: null);
        HazardWorlds.Step(world, rules: rules, events: events);

        Assert.NotEmpty(events.OfType<LandslideStruck>());
    }

    [Fact]
    public void Meteors_StrikeAtAboutTheirRate_AndRuinTheVillagesTheyFallNear()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var rules = HazardWorlds.Rules with { Hazards = Hazards with { Meteor = Hazards.Meteor with { PerHour = 40f } } };
        var events = new List<WorldEvent>();

        HazardWorlds.Step(world, steps: 3600, rules: rules, events: events);

        var meteors = events.OfType<MeteorStruck>().ToList();
        Assert.InRange(meteors.Count, 22, 60);
        Assert.Equal(meteors.Count, events.OfType<EarthquakeStruck>().Count());
        Assert.Contains(events.OfType<SettlementDamaged>(), d => d.Cause == DisasterKind.Meteor);
    }

    [Fact]
    public void Evacuation_SavesHalfTheLives()
    {
        int Deaths(bool evacuated)
        {
            var world = DivineWorlds.Create(0f, 300f);
            var settlement = world.Settlements[0];
            (settlement.Population, settlement.Housing) = (2000, 2000);
            if (evacuated)
            {
                HazardWorlds.Build(world, 0, BuildingKind.Evacuation);
            }

            var rules = HazardWorlds.Rules with { Hazards = Hazards with { Meteor = Hazards.Meteor with { PerHour = 1e9f, Radius = 5000f } } };
            var events = new List<WorldEvent>();
            HazardWorlds.Step(world, rules: rules, events: events);
            return events.OfType<SettlementDamaged>().Where(d => d.Cause == DisasterKind.Meteor).Sum(d => d.Deaths);
        }

        var open = Deaths(evacuated: false);
        Assert.True(open > 100);
        Assert.InRange(Deaths(evacuated: true), open * 0.35, open * 0.65);
    }

    [Fact]
    public void OldMarks_FadeFromTheLand()
    {
        var world = DivineWorlds.Create(0f, 300f);
        HazardModel.AddMark(world, TerrainMarkKind.Crack, new GroundPosition(100f, 100f), 30f);

        HazardWorlds.Step(world, steps: (int)Hazards.MarkSeconds - 1);
        Assert.Single(world.Disasters.Marks);

        HazardWorlds.Step(world, steps: 3);
        Assert.Empty(world.Disasters.Marks);
    }

    [Fact]
    public void Hazards_AreTheSameEveryTime_AcrossASaveAndLoad_AndLeaveTheSharedStreamAlone()
    {
        var rules = HazardWorlds.Rules with
        {
            Hazards = Hazards with
            {
                Meteor = Hazards.Meteor with { PerHour = 30f },
                Volcano = Hazards.Volcano with { EruptionsPerHour = 30f, MinElevation = 0f, CooldownSeconds = 60f },
                Landslide = Hazards.Landslide with { SlidePerSecond = 0.0002f, WetSoil = 0f },
                Locusts = Hazards.Locusts with { SwarmsPerHour = 30f, MinTemperature = 0f, MaxSoil = 1f, FoodEatenPerSecond = 0.05f },
                Blight = Hazards.Blight with { EmergencePerSecond = 0.0005f, MinTemperature = 0f, WetSoil = 0f },
            },
        };
        var system = new HazardSystem(rules, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules, rules.StepIntervalTicks);
        var engine = new SimulationEngine([new IntervalSystem(system, rules.StepIntervalTicks)]);
        var straight = WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State;
        var resumed = WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State;
        var random = straight.Random;

        engine.Run(straight, 6000);
        engine.Run(resumed, 3000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 3000);

        Assert.True(straight.Disasters.NextHazardId > 1);
        Assert.NotEmpty(straight.Disasters.Marks);
        Assert.NotNull(straight.Disasters.Volcano);
        Assert.Contains(straight.Regions.Cells, r => r.Blight > 0f);
        Assert.Equal(random, straight.Random);
        WorldAssert.Equal(straight, resumed);
    }
}
