using System;
using System.Collections.Generic;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The statistical settlement step: food, health, births, deaths and construction.</summary>
public sealed class SettlementSystemTests
{
    private static readonly SettlementRules Rules = SettlementWorlds.Rules;
    private static readonly SettlementRules Quiet = SettlementWorlds.NoBirthsOrDeaths;

    [Fact]
    public void StarvingPopulation_LosesHealth()
    {
        var world = SettlementWorlds.Create(population: 100, housing: 120, food: 0f, fertility: 0f);

        SettlementWorlds.RunSteps(world, Quiet, 10);

        Assert.Equal(1f - (10 * Rules.StarvationHealthLossPerStep), Settlement(world).Health, 5);
        Assert.Equal(0f, Settlement(world).Food);
    }

    [Fact]
    public void PartlyFedPopulation_LosesHealthInProportionToTheShortfall()
    {
        var world = SettlementWorlds.Create(population: 100, housing: 200, food: 0f, fertility: 0.15f);
        var outlook = SettlementEconomy.Assess(Quiet, Settlement(world), world.Regions);

        SettlementWorlds.RunSteps(world, Quiet, 10);

        Assert.InRange(outlook.FedShare, 0.3f, 0.7f);
        Assert.Equal(1f - (10 * Rules.StarvationHealthLossPerStep * (1f - outlook.FedShare)), Settlement(world).Health, 5);
    }

    [Fact]
    public void FedPopulation_RecoversHealth()
    {
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 400f, fertility: 0.5f, health: 0.5f);

        SettlementWorlds.RunSteps(world, Quiet, 20);
        var recovering = Settlement(world).Health;
        SettlementWorlds.RunSteps(world, Quiet, 200);

        Assert.Equal(0.5f + (20 * Rules.HealthRecoveryPerStep), recovering, 5);
        Assert.Equal(1f, Settlement(world).Health);
    }

    [Fact]
    public void StarvingPopulation_DiesFasterThanAFedOne()
    {
        var starving = SettlementWorlds.Create(population: 200, housing: 200, food: 0f, fertility: 0f);
        var fed = SettlementWorlds.Create(population: 200, housing: 200, food: 1600f, fertility: 1f);

        SettlementWorlds.RunSteps(starving, Rules, 100);
        SettlementWorlds.RunSteps(fed, Rules, 100);

        Assert.True(Settlement(starving).Population > 0);
        Assert.Equal(0f, Settlement(starving).Health);
        Assert.Equal(1f, Settlement(fed).Health);
        Assert.True(
            Settlement(starving).Population < Settlement(fed).Population / 2,
            $"Starving {Settlement(starving).Population}, fed {Settlement(fed).Population}.");
    }

    [Fact]
    public void FoodSurplus_GrowsThePopulationUpToHousing()
    {
        var rules = Quiet with { BirthChancePerPersonPerStep = Rules.BirthChancePerPersonPerStep, MaxHousing = 60 };
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 400f, fertility: 0.8f);
        var engine = new SimulationEngine(rules);
        var populations = new List<int>();

        for (var step = 0; step < 600; step++)
        {
            engine.Run(world, rules.StepIntervalTicks);
            populations.Add(Settlement(world).Population);
        }

        Assert.Equal(60, populations[^1]);
        Assert.All(populations, population => Assert.InRange(population, 40, 60));
        Assert.Equal(60, Settlement(world).Housing);
    }

    [Fact]
    public void Births_NeedAHarvestSurplus()
    {
        var rules = Quiet with { BirthChancePerPersonPerStep = 0.05 };
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 400f, fertility: 0f);

        SettlementWorlds.RunSteps(world, rules, 50);

        Assert.Equal(40, Settlement(world).Population);
    }

    [Fact]
    public void Births_NeedStoredFood()
    {
        var rules = Quiet with { BirthChancePerPersonPerStep = 0.05 };
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 0f, fertility: 0.8f);

        SettlementWorlds.RunSteps(world, rules, 1);
        var beforeTheStoresFill = Settlement(world).Population;
        SettlementWorlds.RunSteps(world, rules, 200);

        Assert.Equal(40, beforeTheStoresFill);
        Assert.True(Settlement(world).Population > 40);
    }

    [Fact]
    public void CrowdedSettlement_StartsBuilding_AndARoomyOneDoesNot()
    {
        var crowded = SettlementWorlds.Create(population: 55, housing: 60, food: 400f, fertility: 0.5f);
        var roomy = SettlementWorlds.Create(population: 40, housing: 60, food: 400f, fertility: 0.5f);

        SettlementWorlds.RunSteps(crowded, Quiet, 1);
        SettlementWorlds.RunSteps(roomy, Quiet, 50);

        Assert.Equal(55 * Rules.WorkforceShare * Rules.MaxBuilderShare, Settlement(crowded).ConstructionProgress, 4);
        Assert.Equal(0f, Settlement(roomy).ConstructionProgress);
        Assert.Equal(60, Settlement(roomy).Housing);
    }

    [Fact]
    public void Construction_AddsAHouseWhenTheWorkIsDone()
    {
        var world = SettlementWorlds.Create(population: 55, housing: 60, food: 400f, fertility: 0.5f);
        var builders = SettlementEconomy.AllocateLabour(Quiet, Settlement(world)).Builders;
        var steps = (int)MathF.Ceiling(Rules.WorkPerHouse / builders);

        SettlementWorlds.RunSteps(world, Quiet, steps - 1);
        var almostDone = (Settlement(world).Housing, Settlement(world).ConstructionProgress);
        SettlementWorlds.RunSteps(world, Quiet, 1);

        Assert.Equal(60, almostDone.Housing);
        Assert.True(almostDone.ConstructionProgress > Rules.WorkPerHouse - builders);
        Assert.Equal(60 + Rules.HousingPerHouse, Settlement(world).Housing);
        Assert.Equal(0f, Settlement(world).ConstructionProgress);
    }

    [Fact]
    public void Construction_WaitsWhileTheStoresAreLow()
    {
        var world = SettlementWorlds.Create(population: 55, housing: 60, food: 10f, fertility: 0f, constructionProgress: 50f);

        SettlementWorlds.RunSteps(world, Quiet, 5);

        Assert.Equal(50f, Settlement(world).ConstructionProgress);
        Assert.Equal(60, Settlement(world).Housing);
    }

    [Fact]
    public void Construction_StopsAtMaxHousing()
    {
        var rules = Quiet with { MaxHousing = 60 };
        var world = SettlementWorlds.Create(population: 60, housing: 60, food: 480f, fertility: 0.5f);

        SettlementWorlds.RunSteps(world, rules, 50);

        Assert.Equal(60, Settlement(world).Housing);
        Assert.Equal(0f, Settlement(world).ConstructionProgress);
    }

    [Fact]
    public void Food_IsCappedByStorage()
    {
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 470f, fertility: 1f);

        SettlementWorlds.RunSteps(world, Quiet, 20);

        Assert.Equal(SettlementEconomy.ComputeStorage(Rules, 60), Settlement(world).Food);
    }

    [Fact]
    public void Step_AppliesItsOutlook()
    {
        var world = SettlementWorlds.Create(population: 55, housing: 60, food: 120f, fertility: 0.3f, health: 0.7f, constructionProgress: 20f);
        var outlook = SettlementEconomy.Assess(Rules, Settlement(world), world.Regions);

        SettlementWorlds.RunSteps(world, Rules, 1);

        Assert.True(outlook.Labour.Builders > 0f);
        Assert.Equal(outlook.FoodAfter, Settlement(world).Food);
        Assert.Equal(outlook.HealthAfter, Settlement(world).Health);
        Assert.Equal(20f + outlook.Labour.Builders, Settlement(world).ConstructionProgress, 5);
    }

    [Fact]
    public void EmptySettlement_StaysEmpty()
    {
        var world = SettlementWorlds.Create(population: 0, housing: 60, food: 100f, fertility: 0.5f);

        SettlementWorlds.RunSteps(world, Rules, 100);

        Assert.Equal(0, Settlement(world).Population);
        Assert.Equal(60, Settlement(world).Housing);
    }

    [Fact]
    public void StartingSettlement_GrowsAndBuildsOnItsOwn()
    {
        var world = WorldGenerator.Generate(new WorldSeed(1234), TestContent.SmallWorldGen).State;
        var rules = TestContent.Shipped.SettlementRules;
        var founded = (world.Settlements[0].Population, world.Settlements[0].Housing);

        SettlementWorlds.RunSteps(world, rules, 600);

        var settlement = world.Settlements[0];
        Assert.True(settlement.Housing > founded.Housing, $"Housing {settlement.Housing}.");
        Assert.True(settlement.Population > founded.Population, $"Population {settlement.Population}.");
        Assert.InRange(settlement.Population, 1, settlement.Housing);
        Assert.Equal(1f, settlement.Health);
    }

    [Fact]
    public void Validator_ListsEveryInvalidRule()
    {
        var rules = Rules with
        {
            StepIntervalTicks = 0,
            WorkforceShare = 1.5f,
            DeathChancePerPersonPerStep = -0.1,
            HousingPerHouse = 0,
            ConstructionStartCrowding = 0f,
            WorkPerHouse = 0f,
        };

        var exception = Assert.Throws<ArgumentException>(() => SettlementRulesValidator.Validate(rules));
        Assert.Throws<ArgumentException>(() => new SettlementSystem(rules));
        foreach (var name in new[] { "StepIntervalTicks", "WorkforceShare", "DeathChancePerPersonPerStep", "HousingPerHouse", "ConstructionStartCrowding", "WorkPerHouse" })
        {
            Assert.Contains(name, exception.Message);
        }
    }

    /// <summary>The world's only settlement.</summary>
    private static SettlementState Settlement(WorldState world) => SettlementWorlds.Settlement(world);
}
