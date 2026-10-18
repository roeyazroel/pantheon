using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>The disaster step (spec §19–21 and §94, ADR-014): earthquakes, wildfires, destruction and fear.</summary>
public sealed class DisasterSystemTests
{
    private static readonly DisasterRules Rules = DisasterWorlds.Rules with { EarthquakesPerHour = 0f, HearthFirePerSecond = 0f };

    [Fact]
    public void AnEarthquake_TurnsHousesToRuins_KillsSpoilsAndFrightens_AndIsReported()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var settlement = world.Settlements[0];
        var belief = DivineWorlds.Believe(settlement, adherence: 0.5f, faith: 0.5f);
        DisasterModel.StartQuake(Rules, world.Disasters, settlement.Position, 7.5f, world.Tick, null);
        var events = new List<WorldEvent>();

        Step(world, steps: 13, events: events);

        Assert.True(settlement.Housing < 120);
        Assert.Equal((120 - settlement.Housing) / SettlementWorlds.Rules.HousingPerHouse, settlement.Ruins);
        Assert.True(settlement.Population < 100);
        Assert.True(settlement.Food < 400f * 0.8f);
        Assert.True(belief.Fear > 0.2f);
        Assert.True(belief.Doubt > 0.15f);
        Assert.Equal(settlement.Ruins, events.OfType<SettlementDamaged>().Sum(d => d.HousesLost));
        Assert.All(events.OfType<SettlementDamaged>(), d => Assert.Equal(DisasterKind.Earthquake, d.Cause));
        Assert.Empty(world.Disasters.Earthquakes);
    }

    [Fact]
    public void AShield_SparesASettlementMostOfTheDamage()
    {
        int RuinsAfterQuake(bool shielded)
        {
            var world = DivineWorlds.Create(0f, 300f);
            var settlement = world.Settlements[0];
            settlement.Housing = 1000;
            settlement.ShieldedUntilTick = shielded ? 10_000 : 0;
            DisasterModel.StartQuake(Rules, world.Disasters, settlement.Position, 7.5f, world.Tick, null);
            Step(world, steps: 12);
            return settlement.Ruins;
        }

        var bare = RuinsAfterQuake(shielded: false);
        Assert.True(bare > 50);
        Assert.True(RuinsAfterQuake(shielded: true) * 4 < bare);
    }

    [Fact]
    public void NaturalEarthquakes_StrikeAtAboutTheirRate()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var events = new List<WorldEvent>();

        Step(world, steps: 3600, rules: Rules with { EarthquakesPerHour = 20f }, events: events);

        var quakes = events.OfType<EarthquakeStruck>().ToList();
        Assert.InRange(quakes.Count, 10, 32);
        Assert.All(quakes, q => Assert.InRange(q.Magnitude, Rules.MinMagnitude, Rules.MaxMagnitude));
        Assert.All(quakes, q => Assert.Null(q.GodId));
    }

    [Fact]
    public void AFire_GrowsOnItsFuel_ScorchesTheLand_AndBurnsOut()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DisasterWorlds.Parch(world);
        var region = world.Regions.Cells[0];
        region.Fire = Rules.IgnitionIntensity;

        Step(world, steps: 5, rules: Rules with { SpreadPerSecond = 0f });
        Assert.True(region.Fire > Rules.IgnitionIntensity);
        Assert.True(region.Scorch > 0f);

        Step(world, steps: 120, rules: Rules with { SpreadPerSecond = 0f, ScorchRecoveryPerSecond = 0f });
        Assert.Equal(0f, region.Fire);
        Assert.True(region.Scorch > 0.8f);
    }

    [Fact]
    public void RainAndFirefighting_PutFiresOut()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DisasterWorlds.Parch(world);
        var village = world.Regions.Cells[world.Settlements[0].RegionId];
        var field = world.Regions.Cells[0];
        WeatherWorlds.AddCell(world, world.Regions.GetRegionCenter(field.Id), 100f, humidity: 1f, pressure: 0.5f);
        village.Fire = 0.5f;
        field.Fire = 0.5f;
        var lone = world.Regions.Cells[world.Regions.Columns * 15];
        lone.Fire = 0.5f;

        Step(world, steps: 15, rules: Rules with { SpreadPerSecond = 0f, FirefightingPerSecond = 0.05f });

        Assert.True(field.Fire < lone.Fire);
        Assert.True(village.Fire < lone.Fire);
    }

    [Fact]
    public void Fire_SpreadsToDryNeighbours_MoreReadilyDownwind_AndNotOntoWetLand()
    {
        var downwind = 0;
        var wet = 0;
        var across = 0;
        for (var trial = 0; trial < 200; trial++)
        {
            var world = DivineWorlds.Create(0f, 300f);
            DisasterWorlds.Parch(world);
            world.Weather.WindDirection = 0f;
            world.Tick = trial * 10L;
            var columns = world.Regions.Columns;
            var centre = (8 * columns) + 8;
            world.Regions.Cells[centre].Fire = 1f;
            world.Regions.Cells[centre - 1].SoilMoisture = 0.9f;
            Step(world, steps: 1);
            downwind += world.Regions.Cells[centre + 1].Fire > 0f ? 1 : 0;
            across += world.Regions.Cells[centre + columns].Fire > 0f ? 1 : 0;
            wet += world.Regions.Cells[centre - 1].Fire > 0f ? 1 : 0;
        }

        Assert.True(downwind > across, $"downwind {downwind}, across {across}");
        Assert.True(across > 0);
        Assert.Equal(0, wet);
    }

    [Fact]
    public void FireInAVillage_BurnsItsHousesPeopleAndStores()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DisasterWorlds.Parch(world);
        var settlement = world.Settlements[0];
        settlement.Housing = 500;
        world.Regions.Cells[settlement.RegionId].Fire = 1f;
        var events = new List<WorldEvent>();

        Step(world, steps: 10, rules: Rules with { SpreadPerSecond = 0f, FirefightingPerSecond = 0f }, events: events);

        Assert.True(settlement.Ruins > 0);
        Assert.True(settlement.Food < 400f * 0.9f);
        Assert.Contains(events.OfType<SettlementDamaged>(), d => d.Cause == DisasterKind.Fire);
    }

    [Fact]
    public void ScorchedLand_Regrows()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells[0].Scorch = 0.8f;

        Step(world, steps: 100);

        Assert.InRange(world.Regions.Cells[0].Scorch, 0.4f, 0.7f);
    }

    [Fact]
    public void Hearths_LightDryFieldsAroundVillages_ButNeverWetOnes()
    {
        var dry = DivineWorlds.Create(0f, 300f);
        DisasterWorlds.Parch(dry);
        var wet = DivineWorlds.Create(0f, 300f);
        var hearths = Rules with { HearthFirePerSecond = 0.05f, SpreadPerSecond = 0f };
        var events = new List<WorldEvent>();

        Step(dry, steps: 60, rules: hearths, events: events);
        Step(wet, steps: 60, rules: hearths);

        Assert.Contains(events.OfType<WildfireStarted>(), f => f.Cause == FireCause.Hearth);
        Assert.DoesNotContain(wet.Regions.Cells, r => r.Fire > 0f);
    }

    [Fact]
    public void Thunderstorms_LightDryLandBeneathThem()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DisasterWorlds.Parch(world);
        WeatherWorlds.AddCell(world, new GroundPosition(800f, 800f), 200f, humidity: 0.7f, pressure: 1f);
        var events = new List<WorldEvent>();

        Step(world, steps: 20, rules: Rules with { LightningIgnitionPerSecond = 0.05f, SpreadPerSecond = 0f }, events: events);

        var fires = events.OfType<WildfireStarted>().Where(f => f.Cause == FireCause.Lightning).ToList();
        Assert.NotEmpty(fires);
        Assert.All(fires, f => Assert.True(Distance(world.Regions.GetRegionCenter(f.RegionId), new GroundPosition(800f, 800f)) <= 200f));
    }

    [Fact]
    public void Disasters_AreTheSameEveryTime_AcrossASaveAndLoad_AndLeaveTheSharedStreamAlone()
    {
        var system = new DisasterSystem(DisasterWorlds.Rules with { EarthquakesPerHour = 60f, HearthFirePerSecond = 0.02f }, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules);
        var engine = new SimulationEngine([new IntervalSystem(system, Rules.StepIntervalTicks)]);
        var straight = Parched(WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State);
        var resumed = Parched(WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State);
        var random = straight.Random;

        engine.Run(straight, 6000);
        engine.Run(resumed, 3000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 3000);

        Assert.True(straight.Disasters.NextEarthquakeId > 1);
        Assert.Contains(straight.Regions.Cells, r => r.Scorch > 0f);
        Assert.Equal(random, straight.Random);
        WorldAssert.Equal(straight, resumed);
    }

    /// <summary>Runs disaster steps directly.</summary>
    private static void Step(WorldState world, int steps = 1, DisasterRules? rules = null, List<WorldEvent>? events = null)
    {
        var system = new DisasterSystem(rules ?? Rules, SettlementWorlds.Rules, DivineWorlds.Rules, WeatherWorlds.Rules);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += Rules.StepIntervalTicks;
        }
    }

    /// <summary>A generated world with parched, vegetated land.</summary>
    private static WorldState Parched(WorldState world)
    {
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.05f;
        }

        return world;
    }

    /// <summary>Distance between two ground points.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) =>
        System.MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));
}
