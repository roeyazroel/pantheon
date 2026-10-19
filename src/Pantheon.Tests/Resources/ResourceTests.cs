using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Resources;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Resources;

/// <summary>The land's wood and stone, and the villages that gather them (spec §5, §29, §93, ADR-033).</summary>
public sealed class ResourceTests
{
    [Fact]
    public void Generation_LaysForestsAndStoneByBiome_TheSameForTheSameSeed()
    {
        var first = WorldGenerator.Generate(new WorldSeed(42), TestContent.SmallWorldGen).State;
        var again = WorldGenerator.Generate(new WorldSeed(42), TestContent.SmallWorldGen).State;
        var other = WorldGenerator.Generate(new WorldSeed(43), TestContent.SmallWorldGen).State;

        Assert.Equal(first.Regions.Cells.Select(r => (r.Forest, r.Stone)), again.Regions.Cells.Select(r => (r.Forest, r.Stone)));
        Assert.NotEqual(first.Regions.Cells.Select(r => r.Forest), other.Regions.Cells.Select(r => r.Forest));
        Assert.All(first.Regions.Cells, r => Assert.Equal(r.Forest, r.Timber));
        Assert.All(first.Regions.Cells.Where(r => r.Biome == Biome.Sea), r => Assert.Equal((0f, 0f), (r.Forest, r.Stone)));
        Assert.True(Mean(first, Biome.Forest, r => r.Forest) > Mean(first, Biome.Grassland, r => r.Forest));
        Assert.True(Mean(first, Biome.Highland, r => r.Stone) > Mean(first, Biome.Grassland, r => r.Stone));
        Assert.All(first.Settlements, s => Assert.Equal((30f, 15f), (s.Wood, s.Stone)));
    }

    [Fact]
    public void AVillageShortOfWoodAndStone_SendsGatherers_WhoTakeThemFromTheLand()
    {
        var world = ResourceWorlds.Wooded(SettlementWorlds.Create(100, 150, 500f, 0.6f), 0.5f, 0.4f);
        var village = SettlementWorlds.Settlement(world);
        var rules = ResourceWorlds.Rules;
        var (wood, stone) = (village.Wood, village.Stone);
        var timber = world.Regions.Cells.Sum(r => r.Timber);

        var labour = SettlementEconomy.AllocateLabour(rules, village);
        var brought = ResourceModel.Gather(rules.Resources, world.Regions, village, labour.Gatherers);

        Assert.Equal(100 * rules.WorkforceShare * rules.Resources.GatherShare, labour.Gatherers, 3);
        Assert.Equal(labour.Workers - labour.Builders - labour.Worshippers - labour.Gatherers, labour.Farmers, 3);
        Assert.True(brought.Wood > 0f && brought.Stone > 0f);
        Assert.Equal(wood + brought.Wood, village.Wood, 4);
        Assert.Equal(stone + brought.Stone, village.Stone, 4);
        Assert.Equal(timber - (brought.Wood * rules.Resources.TimberPerWood), world.Regions.Cells.Sum(r => r.Timber), 3);

        (village.Wood, village.Stone) = ResourceModel.Wanted(rules.Resources, village);
        Assert.Equal(0f, SettlementEconomy.AllocateLabour(rules, village).Gatherers);
    }

    [Fact]
    public void BareLand_YieldsLittle_AndAVillageOfStoneGathersStone()
    {
        var bare = ResourceWorlds.Wooded(SettlementWorlds.Create(100, 150, 500f, 0.6f), 0.02f, 0.8f);
        var village = SettlementWorlds.Settlement(bare);
        (village.Wood, village.Stone) = (0f, 0f);

        var (wood, stone) = ResourceModel.Gather(ResourceWorlds.Resources, bare.Regions, village, 10f);

        Assert.True(stone > wood * 10f);
    }

    [Fact]
    public void AHouse_TakesWoodWhenBegun_AndWithoutWoodNoneIsBegun()
    {
        var world = SettlementWorlds.Create(100, 100, 500f, 0.6f);
        var village = SettlementWorlds.Settlement(world);
        var rules = ResourceWorlds.Rules;
        village.Wood = 0f;

        Assert.Equal(0f, SettlementEconomy.AllocateLabour(rules, village).Builders);
        Assert.True(SettlementEconomy.AllocateLabour(SettlementWorlds.Rules, village).Builders > 0f);

        village.Wood = 20f;
        new SettlementSystem(rules).Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.True(village.ConstructionProgress > 0f);
        Assert.Equal(20f - rules.Resources.WoodPerHouse, village.Wood, 1);
    }

    [Fact]
    public void ABuilding_WaitsForItsWoodAndStone_AndTakesThem()
    {
        var world = SettlementWorlds.Create(100, 150, 500f, 0.6f);
        var village = SettlementWorlds.Settlement(world);
        village.Society.Priority = SettlementPriority.Food;
        village.Society.Wealth = 500f;
        var society = SocietyWorlds.Rules with { Buildings = TestContent.Shipped.Society.Buildings };
        var granary = society.Buildings.Single(b => b.Kind == BuildingKind.Granary);
        (village.Wood, village.Stone) = (granary.Wood - 1f, granary.Stone);
        var system = new Pantheon.Simulation.Society.SocietySystem(society, ResourceWorlds.Rules, DivineWorlds.Rules);

        system.Update(new TickContext(world, new SimulationRandom(1), []));
        Assert.Null(village.Society.Project);

        village.Wood = granary.Wood + 3f;
        system.Update(new TickContext(world, new SimulationRandom(1), []));
        Assert.Equal(BuildingKind.Granary, village.Society.Project);
        Assert.Equal((3f, 0f), (village.Wood, village.Stone));
    }

    [Fact]
    public void Timber_GrowsBackTowardsTheForest_NotUnderSnow_AndFireBurnsIt()
    {
        var world = ResourceWorlds.Wooded(SettlementWorlds.Create(10, 20, 50f, 0.6f), 0.8f, 0.2f);
        var (cut, snowy, burning) = (world.Regions.Cells[0], world.Regions.Cells[1], world.Regions.Cells[2]);
        (cut.Timber, snowy.Timber, snowy.Snow, burning.Fire) = (0.2f, 0.2f, 0.9f, 1f);
        var system = new ResourceSystem(ResourceWorlds.Resources, 10);

        for (var i = 0; i < 100; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), []));
        }

        var grown = 0.8f - (0.6f * System.MathF.Pow(1f - ResourceWorlds.Resources.TimberRegrowthPerSecond, 100));
        Assert.Equal(grown, cut.Timber, 3);
        Assert.Equal(0.2f, snowy.Timber);
        Assert.Equal(0f, burning.Timber);
        Assert.Equal(1f, ResourceModel.Cleared(burning));
    }

    [Fact]
    public void AClearedForest_HoldsLessWildlife()
    {
        var world = ResourceWorlds.Wooded(SettlementWorlds.Create(10, 20, 50f, 0.6f), 0.8f, 0.2f);
        var (whole, cleared) = (world.Regions.Cells[0], world.Regions.Cells[1]);
        cleared.Timber = 0f;
        (whole.Wildlife, cleared.Wildlife) = (0f, 0f);
        var ecology = new Pantheon.Simulation.Ecology.EcologySystem(NatureWorlds.Ecology, ResourceWorlds.Rules);

        for (var i = 0; i < 600; i++)
        {
            ecology.Update(new TickContext(world, new SimulationRandom(1), []));
        }

        Assert.Equal(whole.Wildlife * (1f - ResourceWorlds.Resources.ClearedWildlife), cleared.Wildlife, 2);
    }

    [Fact]
    public void Settlers_CarryTheirShareOfTheStores()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var home = world.Settlements[0];
        home.Population = 150;
        (home.Wood, home.Stone) = (90f, 45f);
        home.Society.Priority = SettlementPriority.Expansion;
        world.Tick = 100;

        new Pantheon.Simulation.Society.SocietySystem(SocietyWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Rules)
            .Update(new TickContext(world, new SimulationRandom(1), []));

        var village = world.Settlements.Single(s => s.Id != home.Id);
        var share = village.Population / 150f;
        Assert.Equal(90f * share, village.Wood, 3);
        Assert.Equal(45f * share, village.Stone, 3);
        Assert.Equal(90f, home.Wood + village.Wood, 3);
    }

    /// <summary>The mean of a value over a biome's regions.</summary>
    private static float Mean(WorldState world, Biome biome, System.Func<RegionState, float> value) =>
        world.Regions.Cells.Where(r => r.Biome == biome).Select(value).DefaultIfEmpty(0f).Average();
}
