using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Weather;

/// <summary>What the soil's water does (ADR-013): harvests, hardship, the Rain miracle and the gods who answer drought.</summary>
public sealed class WaterTests
{
    [Fact]
    public void DryFarmland_YieldsLess_DownToTheFloorWhenParched()
    {
        var rules = SettlementWorlds.Rules;
        var watered = new FarmlandSurvey(9, 0.6f, 0f, MeanSoilMoisture: rules.WellWateredSoil);
        var dry = watered with { MeanSoilMoisture = rules.WellWateredSoil / 2f };
        var parched = watered with { MeanSoilMoisture = 0f };

        Assert.Equal(0f, SettlementEconomy.ComputeDrought(rules, watered));
        Assert.Equal(0.5f, SettlementEconomy.ComputeDrought(rules, dry), 4);
        Assert.Equal(1f, SettlementEconomy.ComputeWaterYield(rules, watered));
        Assert.Equal(rules.DroughtYieldFloor, SettlementEconomy.ComputeWaterYield(rules, parched), 4);
        Assert.Equal(
            SettlementEconomy.ComputeProduction(rules, 30f, watered) * SettlementEconomy.ComputeWaterYield(rules, dry),
            SettlementEconomy.ComputeProduction(rules, 30f, dry),
            3);
    }

    [Fact]
    public void ADroughtStarvesASettlementThatTheLandFedBefore()
    {
        var rules = SettlementWorlds.NoBirthsOrDeaths;
        var world = SettlementWorlds.Create(population: 120, housing: 140, food: 0f, fertility: 0.6f);
        var settlement = SettlementWorlds.Settlement(world);
        var fed = SettlementEconomy.Assess(rules, settlement, world.Regions);
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.05f;
        }

        var parched = SettlementEconomy.Assess(rules, settlement, world.Regions);

        Assert.False(fed.IsStarving);
        Assert.True(parched.IsStarving);
    }

    [Fact]
    public void Drought_IsAHardship_PeopleWorryBeforeTheStoresRunOut()
    {
        var settlement = DivineWorlds.Create(0f, 300f).Settlements[0];

        var conditions = BeliefModel.AssessConditions(DivineWorlds.Rules, settlement, drought: 0.8f);

        Assert.Equal(0f, conditions.FoodNeed);
        Assert.Equal(0.8f, conditions.WaterNeed, 4);
        Assert.Equal(DivineWorlds.Rules.DroughtWorry * 0.8f, conditions.Hardship, 4);
        Assert.Equal(0.8f, BeliefModel.NeedFor(conditions, MiracleNeed.Water), 4);
    }

    [Fact]
    public void Rain_CallsACloudOverTheTarget_AndIsLovedMoreWhereTheLandIsParched()
    {
        float LoveAfterRain(float soil)
        {
            var world = DivineWorlds.Create(1000f, 300f);
            var settlement = world.Settlements[0];
            DivineWorlds.Believe(settlement, adherence: 0.6f, faith: 0.6f, love: 0.2f);
            foreach (var region in world.Regions.Cells)
            {
                region.SoilMoisture = soil;
            }

            var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, weather: WeatherWorlds.Rules);
            var events = new List<WorldEvent>();
            Assert.True(caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "rain", settlement.Position), DivineWorlds.GodId, events).IsAccepted);
            var cloud = Assert.Single(world.Weather.Cells);
            Assert.Equal(DivineWorlds.GodId, cloud.SummonedByGodId);
            Assert.Equal(settlement.Position, new GroundPosition(cloud.X, cloud.Z));
            return BeliefModel.Find(settlement, DivineWorlds.GodId)!.Love;
        }

        Assert.True(LoveAfterRain(soil: 0.02f) > LoveAfterRain(soil: 0.6f) + 0.02f);
    }

    [Fact]
    public void Rain_WithoutWeather_StillWorksItsWonder_ButCallsNoCloud()
    {
        var world = DivineWorlds.Create(1000f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.6f);
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);

        Assert.True(caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "rain", world.Settlements[0].Position), DivineWorlds.GodId, new List<WorldEvent>()).IsAccepted);
        Assert.Empty(world.Weather.Cells);
    }

    [Fact]
    public void RainOnARivalsParchedFollowers_IsRememberedAsHelp()
    {
        var world = DiplomacyWorlds.Create(200f, 600f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f, godId: RivalWorlds.RivalId);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.3f, faith: 0.8f);
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.02f;
        }

        world.Gods[0].DivinePower = 1000f;
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(DiplomacyWorlds.Rules), WeatherWorlds.Rules);

        caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "rain", world.Settlements[0].Position), DivineWorlds.GodId, new List<WorldEvent>());

        Assert.Equal([DiplomaticAct.Helped], DiplomacyModel.FindRelation(world, RivalWorlds.RivalId, DivineWorlds.GodId)!.Memories.Select(m => m.Act));
    }

    [Fact]
    public void AGuardian_CallsRainOnItsFollowersInDrought_AndSaysWhy()
    {
        var world = RivalWorlds.Create(GodPersonality.Guardian, 1000f, 300f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.9f, faith: 0.9f, love: 0.5f, godId: RivalWorlds.RivalId);
        foreach (var region in world.Regions.Cells)
        {
            region.SoilMoisture = 0.02f;
        }

        var best = RivalWorlds.CreateAi().Evaluate(world, world.Gods.First(g => g.Id == RivalWorlds.RivalId))[0];

        Assert.Equal(AiActionKind.CastMiracle, best.Kind);
        Assert.Equal("rain", best.MiracleId);
        Assert.Equal(settlement.Id, best.SettlementId);
        Assert.Contains("protect", best.Reason, System.StringComparison.Ordinal);
    }
}
