using System.Collections.Generic;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Society;

/// <summary>What technology, buildings and priorities do for a settlement's life and its god (spec §9, §21, §29, §36, ADR-023).</summary>
public sealed class CivicTests
{
    private static readonly Pantheon.Domain.Settlements.SettlementRules Rules = SocietyWorlds.Civic;

    [Fact]
    public void Agriculture_AndAFoodPriority_RaiseTheHarvest()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var plain = SettlementEconomy.Assess(Rules, village, world.Regions).Production;
        village.Society.Technology.Agriculture = 2f;
        village.Society.Priority = SettlementPriority.Food;

        var tended = SettlementEconomy.Assess(Rules, village, world.Regions).Production;

        Assert.Equal(plain * (1f + (2f * Rules.Civics.AgriculturePerLevel)) * (1f + Rules.Civics.FoodPriorityYield), tended, 3);
    }

    [Fact]
    public void AGranary_AndACistern_KeepMore()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var (food, water) = (SettlementEconomy.StorageOf(Rules, village), SettlementEconomy.WaterStorageOf(Rules, village));
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Granary, GodId = null });
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Cistern, GodId = null });

        Assert.Equal(food * Rules.Civics.GranaryStorage, SettlementEconomy.StorageOf(Rules, village), 3);
        Assert.Equal(water * Rules.Civics.CisternStorage, SettlementEconomy.WaterStorageOf(Rules, village), 3);
    }

    [Fact]
    public void Medicine_AndAHospital_HealTheSick_AndSaveLives()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        village.Health = 0.4f;
        var before = SettlementEconomy.Assess(Rules, village, world.Regions);
        village.Society.Technology.Medicine = 3f;
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Hospital, GodId = null });

        var after = SettlementEconomy.Assess(Rules, village, world.Regions);

        Assert.True(after.HealthAfter > before.HealthAfter);
        Assert.True(after.DeathChance < before.DeathChance);
    }

    [Fact]
    public void Priorities_ForGrowthAndWorship_BringMoreBirthsAndMoreWorshippers()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.8f);
        var before = SettlementEconomy.Assess(Rules, village, world.Regions);
        village.Society.Priority = SettlementPriority.Growth;
        var growing = SettlementEconomy.Assess(Rules, village, world.Regions);
        village.Society.Priority = SettlementPriority.Worship;
        var worshipping = SettlementEconomy.Assess(Rules, village, world.Regions);

        Assert.Equal(before.BirthChance * (1f + Rules.Civics.GrowthPriorityBirths), growing.BirthChance, 6);
        Assert.Equal(before.Labour.Worshippers * Rules.Civics.WorshipPriorityFactor, worshipping.Labour.Worshippers, 3);
    }

    [Fact]
    public void Architecture_LightensHouses_AndWallsWithArms_TurnAttacksAway()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var work = SettlementEconomy.ComputeHouseWork(Rules, village);
        village.Society.Technology.Architecture = 2.5f;
        village.Society.Technology.Warfare = 2f;
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Walls, GodId = null });

        Assert.Equal(work * (1f - (2.5f * Rules.Civics.ArchitecturePerLevel)), SettlementEconomy.ComputeHouseWork(Rules, village), 3);
        Assert.Equal(Rules.Civics.WallsDefence + (2f * Rules.Civics.WarfarePerLevel), CivicModel.Defence(Rules, village), 4);
    }

    [Fact]
    public void ATempleToAGod_AndReligion_RaiseThatGodsPower_Only()
    {
        var divine = DivineWorlds.Rules with { TempleInfrastructure = 1.5f, ReligionPerLevel = 0.06f };
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var mine = DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.8f);
        var theirs = DivineWorlds.Believe(village, adherence: 0.3f, faith: 0.8f, godId: 2);
        var (myPower, theirPower) = (BeliefModel.ComputePower(divine, Rules, village, mine), BeliefModel.ComputePower(divine, Rules, village, theirs));
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = 1 });
        village.Society.Technology.Religion = 2f;

        Assert.Equal(myPower * 1.5f * 1.12f, BeliefModel.ComputePower(divine, Rules, village, mine), 3);
        Assert.Equal(theirPower * 1.12f, BeliefModel.ComputePower(divine, Rules, village, theirs), 3);
    }

    [Fact]
    public void Firebreaks_AndShelters_SpareASettlementSomeOfADisaster()
    {
        (int Houses, int Deaths) Burn(bool prepared)
        {
            var world = DivineWorlds.Create(0f, 300f);
            var village = world.Settlements[0];
            (village.Housing, village.Population) = (1000, 1000);
            if (prepared)
            {
                village.Society.Buildings.Add(new Building { Kind = BuildingKind.Firebreak, GodId = null });
            }

            var rules = DisasterWorlds.Rules with { EarthquakesPerHour = 0f, HearthFirePerSecond = 0f, Preparedness = TestContent.Shipped.Disasters.Preparedness };
            var system = new DisasterSystem(rules, Rules, DivineWorlds.Rules, WeatherWorlds.Rules);
            for (var i = 0; i < 30; i++)
            {
                world.Regions.Cells[village.RegionId].Fire = 1f;
                system.Update(new TickContext(world, new SimulationRandom(1), new List<WorldEvent>()));
                world.Tick += rules.StepIntervalTicks;
            }

            return (village.Ruins, 1000 - village.Population);
        }

        var bare = Burn(prepared: false);
        var guarded = Burn(prepared: true);

        Assert.True(guarded.Houses < bare.Houses * 0.75f, $"{guarded.Houses} vs {bare.Houses}");
        Assert.True(guarded.Deaths < bare.Deaths);
    }
}
