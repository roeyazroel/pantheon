using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Weather;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Weather;

/// <summary>The weather step (spec §16, ADR-013): cells, spells, soil water, storms and droughts.</summary>
public sealed class WeatherSystemTests
{
    private static readonly WeatherRules Rules = WeatherWorlds.Rules;

    [Fact]
    public void FirstStep_SchedulesTheSpell_AndScattersTheStartingClouds()
    {
        var world = DivineWorlds.Create(0f, 300f);

        Step(world);

        var (min, max) = WeatherModel.SpellSeconds(Rules, WeatherSpell.Normal);
        Assert.InRange(world.Weather.SpellEndsTick!.Value, (long)(min * SimulationTime.TicksPerSecond), (long)(max * SimulationTime.TicksPerSecond) + 1);
        Assert.Equal(Rules.InitialCells, world.Weather.Cells.Count);
        Assert.Equal(WeatherSpell.Normal, world.Weather.Spell);
    }

    [Fact]
    public void Cells_MoveWithTheirVelocity_AndRainSpendsTheirWater()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        var cell = WeatherWorlds.AddCell(world, new GroundPosition(300f, 500f), 200f, humidity: 0.9f, pressure: 0.3f, velocityX: 4f);
        var humidity = cell.Humidity;

        Step(world, steps: 20);

        var seconds = 20 * WeatherModel.StepSeconds(Rules);
        Assert.Equal(300f + (4f * seconds), cell.X, 2);
        Assert.True(cell.Humidity < humidity);
    }

    [Fact]
    public void OverTheSea_CellsGatherWater_AndOverLand_StormsWeaken()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        foreach (var region in world.Regions.Cells.Where(r => world.Regions.GetCoordinates(r.Id).Column < 4))
        {
            region.Biome = Biome.Sea;
        }

        var overSea = WeatherWorlds.AddCell(world, new GroundPosition(100f, 500f), 50f, humidity: 0.2f, pressure: 0.9f);
        var overLand = WeatherWorlds.AddCell(world, new GroundPosition(800f, 500f), 50f, humidity: 0.2f, pressure: 0.9f);

        Step(world, steps: 20);

        Assert.True(overSea.Humidity > 0.2f);
        Assert.Equal(0.9f, overSea.Pressure, 4);
        Assert.True(overLand.Pressure < 0.9f);
    }

    [Fact]
    public void Cells_ThatRainThemselvesOutOrLeaveTheMap_Vanish()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        var spent = WeatherWorlds.AddCell(world, new GroundPosition(500f, 500f), 100f, humidity: 0.051f, pressure: 0.9f);
        var leaving = WeatherWorlds.AddCell(world, new GroundPosition(1150f, 500f), 100f, humidity: 0.8f, pressure: 0.2f, velocityX: 5f);
        var arriving = WeatherWorlds.AddCell(world, new GroundPosition(-150f, 500f), 100f, humidity: 0.8f, pressure: 0.2f, velocityX: 5f);

        Step(world, steps: 4);

        Assert.DoesNotContain(spent, world.Weather.Cells);
        Assert.DoesNotContain(leaving, world.Weather.Cells);
        Assert.Contains(arriving, world.Weather.Cells);
    }

    [Fact]
    public void Rain_WetsTheSoilBeneathIt_WhileTheRestDries()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        WeatherWorlds.AddCell(world, new GroundPosition(300f, 500f), 150f, humidity: 1f, pressure: 0.5f);
        var wet = world.Regions.Cells[RegionAt(world, 300f, 500f)];
        var dry = world.Regions.Cells[RegionAt(world, 900f, 100f)];

        Step(world, steps: 20);

        Assert.True(wet.SoilMoisture > 0.5f);
        Assert.True(dry.SoilMoisture < 0.5f);
    }

    [Fact]
    public void ADrySpell_BringsFarFewerCellsThanANormalOne()
    {
        int Arrivals(WeatherSpell spell)
        {
            var world = Calm(DivineWorlds.Create(0f, 300f));
            world.Weather.Spell = spell;
            world.Weather.SpellEndsTick = long.MaxValue;
            var system = new WeatherSystem(Rules with { MaxCells = 1000 }, SettlementWorlds.Rules);
            for (var i = 0; i < 4000; i++)
            {
                system.Update(new TickContext(world, new SimulationRandom(1), []));
                world.Tick += Rules.StepIntervalTicks;
            }

            return world.Weather.NextCellId - 1;
        }

        var normal = Arrivals(WeatherSpell.Normal);
        var dry = Arrivals(WeatherSpell.Dry);

        // 4000 steps of 0.5 s at 0.012 cells per second: about 24 in a normal spell, a tenth in a dry one.
        Assert.InRange(normal, 12, 40);
        Assert.True(dry * 4 < normal);
    }

    [Fact]
    public void WhenASpellEnds_TheNextBegins_TheWindTurns_AndItIsReported()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        world.Weather.SpellEndsTick = world.Tick;
        var events = new List<WorldEvent>();

        Step(world, events: events);

        var change = Assert.Single(events.OfType<WeatherSpellChanged>());
        Assert.Equal(WeatherSpell.Normal, change.Previous);
        Assert.Equal(world.Weather.Spell, change.Spell);
        Assert.Equal(world.Weather.SpellEndsTick, change.EndsAtTick);
        Assert.True(change.EndsAtTick > world.Tick);
        Assert.InRange(System.Math.Abs(world.Weather.WindDirection - 0f) % (2f * System.MathF.PI), 0f, 2f * System.MathF.PI);
    }

    [Theory]
    [InlineData(WeatherSpell.Normal, 0.1, WeatherSpell.Dry)]
    [InlineData(WeatherSpell.Normal, 0.6, WeatherSpell.Normal)]
    [InlineData(WeatherSpell.Normal, 0.9, WeatherSpell.Wet)]
    [InlineData(WeatherSpell.Dry, 0.1, WeatherSpell.Normal)]
    [InlineData(WeatherSpell.Wet, 0.9, WeatherSpell.Normal)]
    public void NextSpell_FollowsANormalSpellWithAnyAndOthersWithANormalOne(WeatherSpell spell, double roll, WeatherSpell expected)
    {
        Assert.Equal(expected, WeatherSystem.NextSpell(Rules, spell, roll));
    }

    [Fact]
    public void AStorm_RuinsStoresAndBuildingWork_AndIsReportedWhenItArrives()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        var settlement = world.Settlements[0];
        settlement.ConstructionProgress = 50f;
        var events = new List<WorldEvent>();
        WeatherWorlds.AddCell(world, new GroundPosition(-20f, 512f), 250f, humidity: 1f, pressure: 1f, velocityX: 5f);

        Step(world, steps: 80, events: events);

        Assert.True(settlement.Food < 400f * 0.95f);
        Assert.True(settlement.ConstructionProgress < 50f * 0.95f);
        var struck = Assert.Single(events.OfType<StormStruck>());
        Assert.Equal(settlement.Id, struck.SettlementId);
        Assert.True(struck.Wind >= Rules.StormWind);
    }

    [Fact]
    public void AShower_DoesNoDamage()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        WeatherWorlds.AddCell(world, new GroundPosition(300f, 512f), 250f, humidity: 0.9f, pressure: 0.3f);
        var events = new List<WorldEvent>();

        Step(world, steps: 20, events: events);

        Assert.Equal(400f, world.Settlements[0].Food);
        Assert.Empty(events.OfType<StormStruck>());
    }

    [Fact]
    public void Drought_IsReportedWhenTheFarmlandDriesOut_AndWhenRainEndsIt()
    {
        var world = Calm(DivineWorlds.Create(0f, 300f));
        var settlement = world.Settlements[0];
        var threshold = SettlementWorlds.Rules.WellWateredSoil * (1f - SettlementWorlds.Rules.DroughtThreshold);
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = threshold + 0.001f;
        }

        var events = new List<WorldEvent>();
        Step(world, steps: 40, events: events);
        var started = Assert.Single(events.OfType<DroughtChanged>());
        Assert.True(started.Started);
        Assert.Equal(settlement.Id, started.SettlementId);

        events.Clear();
        WeatherWorlds.AddCell(world, settlement.Position, 300f, humidity: 1f, pressure: 0.5f);
        Step(world, steps: 20, events: events);
        var ended = Assert.Single(events.OfType<DroughtChanged>());
        Assert.False(ended.Started);
    }

    [Fact]
    public void Weather_IsTheSameEveryTime_AndAcrossASaveAndLoad()
    {
        var engine = new SimulationEngine([new IntervalSystem(new WeatherSystem(Rules, SettlementWorlds.Rules), Rules.StepIntervalTicks)]);
        var straight = WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State;
        var resumed = WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State;

        engine.Run(straight, 6000);
        engine.Run(resumed, 3000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 3000);

        Assert.NotEmpty(straight.Weather.Cells);
        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Weather_DrawsFromItsOwnStream_SoItNeverShiftsTheSettlements()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var before = world.Random;

        new SimulationEngine([new IntervalSystem(new WeatherSystem(Rules, SettlementWorlds.Rules), Rules.StepIntervalTicks)]).Run(world, 3000);

        Assert.Equal(before, world.Random);
        Assert.True(world.Weather.NextCellId > 1);
    }

    /// <summary>A world whose spell is scheduled far ahead and whose sky is clear.</summary>
    private static WorldState Calm(WorldState world)
    {
        world.Weather.SpellEndsTick = long.MaxValue;
        return world;
    }

    /// <summary>Runs weather steps directly.</summary>
    private static void Step(WorldState world, int steps = 1, List<WorldEvent>? events = null)
    {
        var system = new WeatherSystem(Rules with { MaxCells = world.Weather.Cells.Count + Rules.InitialCells, CellsPerSecond = 0f }, SettlementWorlds.Rules);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += Rules.StepIntervalTicks;
        }
    }

    /// <summary>The region at a point.</summary>
    private static int RegionAt(WorldState world, float x, float z)
    {
        world.Regions.TryGetRegionIdAt(x, z, out var id);
        return id;
    }
}
