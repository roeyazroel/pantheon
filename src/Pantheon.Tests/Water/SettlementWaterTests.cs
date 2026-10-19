using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Water;

/// <summary>Drinking water (spec §30, ADR-022): rivers, lakes and wells, the stores they fill, and thirst.</summary>
public sealed class SettlementWaterTests
{
    /// <summary>The pinned rules with people who drink.</summary>
    private static readonly SettlementRules Rules = SettlementWorlds.Rules with
    {
        WaterPerPersonPerStep = 0.05f,
        WaterStoragePerHousing = 3f,
        WellFloor = 0.25f,
        WellAccess = 0.5f,
    };

    [Fact]
    public void ARiverBesideAVillage_GivesItAllTheWaterItNeeds_WhateverTheSoil()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        Dry(world, 0f);
        var parched = SettlementEconomy.ComputeWaterAccess(Rules, world.Regions, village.RegionId);
        WaterWorlds.LayRiver(world);

        Assert.Equal(Rules.WellFloor, parched, 3);
        Assert.Equal(1f, SettlementEconomy.ComputeWaterAccess(Rules, world.Regions, village.RegionId), 3);
    }

    [Fact]
    public void ShallowWells_DryUpWithTheSoil_AndAVillageBesideTheRiverStillDraws()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        Dry(world, 0.6f);
        var wet = SettlementEconomy.ComputeWaterAccess(Rules, world.Regions, village.RegionId);
        Dry(world, 0.1f);
        var dry = SettlementEconomy.ComputeWaterAccess(Rules, world.Regions, village.RegionId);

        Assert.Equal(Rules.WellFloor + Rules.WellAccess, wet, 3);
        Assert.True(dry < wet);
    }

    [Fact]
    public void Wells_FillUpToTheirStorage()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        village.Water = 0f;

        Step(world, steps: 1000, rules: Rules with { BirthChancePerPersonPerStep = 0, DeathChancePerPersonPerStep = 0, IllHealthDeathChancePerPersonPerStep = 0 });

        Assert.Equal(100, village.Population);
        Assert.Equal(SettlementEconomy.ComputeWaterStorage(Rules, village.Housing), village.Water, 2);
    }

    [Fact]
    public void WithoutWater_PeopleGoThirsty_LoseHealth_AndTheWellsRunningDryIsReported()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        Dry(world, 0f);
        village.Water = 5f;
        var events = new List<WorldEvent>();

        Step(world, steps: 30, events: events);

        Assert.Equal(0f, village.Water);
        Assert.True(village.Health < 0.9f);
        Assert.Single(events.OfType<ThirstStarted>());
        Assert.Equal(1f, Hazards.ThirstOf(Rules, village));
        Assert.Equal(1f, BeliefModel.AssessConditions(DivineWorlds.Rules, village, Hazards.Of(Rules, world, village)).WaterNeed);
    }

    [Fact]
    public void FloodedFarmland_YieldsLess()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var before = SettlementEconomy.Assess(Rules, village, world.Regions).Production;
        foreach (var region in world.Regions.Cells)
        {
            region.Flood = 1f;
        }

        var flooded = SettlementEconomy.Assess(Rules, village, world.Regions).Production;

        Assert.Equal(before * (1f - Rules.FloodYieldLoss), flooded, 3);
    }

    private static void Dry(WorldState world, float soil)
    {
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = soil;
        }
    }

    private static void Step(WorldState world, int steps, List<WorldEvent>? events = null, SettlementRules? rules = null)
    {
        var system = new SettlementSystem(rules ?? Rules);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += Rules.StepIntervalTicks;
        }
    }
}
