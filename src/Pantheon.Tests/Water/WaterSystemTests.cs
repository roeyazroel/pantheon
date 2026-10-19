using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Water;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Water;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Water;

/// <summary>The water step (spec §15, §19, ADR-022): rivers rise and fall, overflow downstream, and their floods drown, soak and enrich the land.</summary>
public sealed class WaterSystemTests
{
    private static readonly WaterRules Rules = WaterWorlds.Rules;

    [Fact]
    public void ARiver_RisesWithWetSoil_AndFallsBackWhenItDries()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var river = WaterWorlds.LayRiver(world);
        var dry = WaterModel.TargetLevel(Rules, WeatherWorlds.Rules, world, river);

        Soak(world, 1f);
        var wet = WaterModel.TargetLevel(Rules, WeatherWorlds.Rules, world, river);

        Assert.Equal(Rules.BaseLevel, dry, 3);
        Assert.Equal(Rules.BaseLevel + (Rules.SoilLevelGain * (1f - Rules.SoilThreshold)), wet, 3);
        Step(world, steps: 120);
        Assert.True(river.Level > Rules.FloodLevel);
        Soak(world, 0.2f);
        Step(world, steps: 200);
        Assert.True(river.Level < Rules.FloodLevel);
    }

    [Fact]
    public void AnOverflowingRiver_FloodsDeepestAtItsMouth_AndRaisesTheAlarmOnce()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var river = WaterWorlds.LayRiver(world);
        Soak(world, 1f);
        var events = new List<WorldEvent>();
        var depths = new Dictionary<int, float>();
        WaterModel.Spread(Rules, world.Regions, river, 1f, depths);

        river.Level = 0.99f;
        Step(world, steps: 3, events: events);
        var flooded = world.Regions.Cells[river.Regions[^1]].Flood;
        river.Level = 0.5f;
        Step(world, steps: 1, events: events);
        river.Level = 0.99f;
        Step(world, steps: 3, events: events);

        Assert.Equal(1f, depths[river.Regions[^1]], 3);
        Assert.Equal(1f / river.Regions.Count, depths[river.Regions[0]], 3);
        Assert.True(flooded > 0f);
        Assert.Equal(2, events.OfType<FloodStarted>().Count());
        Assert.All(events.OfType<FloodStarted>(), flood => Assert.Equal((river.Id, null, river.Regions[^1]), (flood.RiverId, flood.GodId, flood.RegionId)));
    }

    [Fact]
    public void AFlood_DrownsHousesPeopleAndStores_AndFrightens_UnlessShielded()
    {
        (int Ruins, int Population, float Food, float Fear) After(bool shielded)
        {
            var world = DivineWorlds.Create(0f, 300f);
            var settlement = world.Settlements[0];
            settlement.Housing = 1000;
            settlement.Population = 900;
            settlement.ShieldedUntilTick = shielded ? 100_000 : 0;
            var belief = DivineWorlds.Believe(settlement, adherence: 0.5f, faith: 0.5f);
            var events = new List<WorldEvent>();
            for (var i = 0; i < 60; i++)
            {
                world.Regions.Cells[settlement.RegionId].Flood = 1f;
                Step(world, steps: 1, events: events);
            }

            Assert.All(events.OfType<SettlementDamaged>(), damage => Assert.Equal(DisasterKind.Flood, damage.Cause));
            return (settlement.Ruins, settlement.Population, settlement.Food, belief.Fear);
        }

        var bare = After(shielded: false);
        var shielded = After(shielded: true);

        Assert.True(bare.Ruins > 20, $"{bare.Ruins} ruins");
        Assert.True(bare.Population < 900);
        Assert.True(bare.Food < 400f * 0.5f);
        Assert.True(bare.Fear > 0.2f);
        Assert.True(shielded.Ruins * 4 < bare.Ruins);
    }

    [Fact]
    public void AFlood_SoaksTheLand_PutsOutItsFire_LeavesSilt_AndDrainsAway()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var region = world.Regions.Cells[5];
        region.Fire = 0.8f;
        region.SoilMoisture = 0.1f;
        region.Flood = 1f;

        Step(world, steps: 1);

        Assert.True(region.SoilMoisture > 0.95f);
        Assert.True(region.Fire < 0.05f);
        Assert.Equal(Rules.SiltBlessing * region.Flood, region.Blessing, 2);
        Step(world, steps: 200);
        Assert.Equal(0f, region.Flood);
        Assert.True(region.Blessing > 0f);
    }

    [Fact]
    public void RiversAndLakes_KeepTheLandBesideThemMoist()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var river = WaterWorlds.LayRiver(world);
        Soak(world, 0.1f);

        Step(world, steps: 100);

        Assert.True(world.Regions.Cells[river.Regions[3]].SoilMoisture > 0.3f);
        Assert.Equal(0.1f, world.Regions.Cells[0 + (2 * world.Regions.Columns)].SoilMoisture, 3);
    }

    [Fact]
    public void AWave_FloodsTheLowLandItReaches_TheLowestDeepest()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells[(8 * 16) + 4].Elevation = 2f;
        world.Regions.Cells[(8 * 16) + 5].Elevation = 8f;
        world.Regions.Cells[(8 * 16) + 6].Elevation = 30f;

        var deepest = WaterModel.Inundate(world, world.Regions.GetRegionCenter((8 * 16) + 5), 100f, 12f);

        Assert.Equal((8 * 16) + 4, deepest);
        Assert.True(world.Regions.Cells[(8 * 16) + 4].Flood > world.Regions.Cells[(8 * 16) + 5].Flood);
        Assert.Equal(0f, world.Regions.Cells[(8 * 16) + 6].Flood);
    }

    private static void Soak(WorldState world, float soil)
    {
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = soil;
        }
    }

    private static void Step(WorldState world, int steps = 1, List<WorldEvent>? events = null)
    {
        var system = new WaterSystem(Rules, WeatherWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Rules, DisasterWorlds.Rules);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += Rules.StepIntervalTicks;
        }
    }
}
