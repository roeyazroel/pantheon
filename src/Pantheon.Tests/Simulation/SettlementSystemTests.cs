using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>Statistical settlement model: food balance, storage and population limits.</summary>
public sealed class SettlementSystemTests
{
    private static readonly SettlementRules Rules = TestContent.Shipped.SettlementRules;

    [Fact]
    public void FoodBalance_IsProductionMinusConsumption()
    {
        var balance = SettlementSystem.ComputeFoodBalance(Rules, 40, 0.5f);

        var expected = (40 * Rules.WorkforceShare * Rules.FoodPerWorkerPerTick * 0.5f) - (40 * Rules.FoodPerPersonPerTick);
        Assert.Equal(expected, balance, 5);
        Assert.True(SettlementSystem.ComputeFoodBalance(Rules, 40, 0f) < 0f);
    }

    [Fact]
    public void Food_IsCappedByStorage()
    {
        var world = CreateWorld(population: 40, housing: 60, food: 299.9f, fertility: 1f);

        new SimulationEngine(Rules).Run(world, 50);

        Assert.Equal(SettlementSystem.ComputeFoodStorage(Rules, 60), world.Settlements[0].Food);
    }

    [Fact]
    public void BarrenLand_StarvesTheSettlement()
    {
        var rules = Rules with { BirthChancePerPersonPerTick = 0.0, DeathChancePerPersonPerTick = 0.0, StarvationDeathChancePerPersonPerTick = 0.01 };
        var world = CreateWorld(population: 40, housing: 60, food: 1f, fertility: 0f);

        new SimulationEngine(rules).Run(world, 2000);

        Assert.Equal(0f, world.Settlements[0].Food);
        Assert.True(world.Settlements[0].Population < 40);
        Assert.True(world.Settlements[0].Population >= 0);
    }

    [Fact]
    public void Births_NeverExceedHousing()
    {
        var rules = Rules with { BirthChancePerPersonPerTick = 0.05, DeathChancePerPersonPerTick = 0.0 };
        var world = CreateWorld(population: 40, housing: 45, food: 225f, fertility: 1f);

        new SimulationEngine(rules).Run(world, 500);

        Assert.Equal(45, world.Settlements[0].Population);
    }

    [Fact]
    public void Births_RequireFoodSurplus()
    {
        var rules = Rules with { BirthChancePerPersonPerTick = 0.05, DeathChancePerPersonPerTick = 0.0, StarvationDeathChancePerPersonPerTick = 0.0 };
        var world = CreateWorld(population: 40, housing: 60, food: 0f, fertility: 0.2f);

        new SimulationEngine(rules).Run(world, 100);

        Assert.Equal(40, world.Settlements[0].Population);
    }

    [Fact]
    public void Validator_RejectsOutOfRangeRules()
    {
        var rules = Rules with { WorkforceShare = 1.5f, DeathChancePerPersonPerTick = -0.1 };

        var exception = Assert.Throws<ArgumentException>(() => SettlementRulesValidator.Validate(rules));
        Assert.Contains("WorkforceShare", exception.Message);
        Assert.Contains("DeathChancePerPersonPerTick", exception.Message);
    }

    /// <summary>A one-region world with a single settlement, for focused tick tests.</summary>
    private static WorldState CreateWorld(int population, int housing, float food, float fertility)
    {
        var region = new RegionState
        {
            Id = 0,
            Biome = Biome.Grassland,
            Elevation = 10f,
            Temperature = 0.6f,
            Moisture = 0.5f,
            Fertility = fertility,
            Vegetation = 0.5f,
        };

        return new WorldState
        {
            Seed = new WorldSeed(1),
            Generation = TestContent.SmallWorldGen,
            Tick = 0,
            Random = new SimulationRandom(42).GetState(),
            Regions = new RegionGrid { Columns = 1, Rows = 1, RegionSize = 64f, Cells = [region] },
            Settlements = new List<SettlementState>
            {
                new()
                {
                    Id = SettlementFounder.StartingSettlementId,
                    Name = "Testvale",
                    RegionId = 0,
                    Position = new GroundPosition(32f, 32f),
                    Population = population,
                    Food = food,
                    Housing = housing,
                    Health = 1f,
                    ConstructionProgress = 0f,
                },
            },
        };
    }
}
