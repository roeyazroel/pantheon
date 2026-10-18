using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>How settlements and gods answer disasters (spec §20–21, ADR-014): rebuilding, exposure, worry, and the Fire, Earthquake and Shield miracles.</summary>
public sealed class ResponseTests
{
    [Fact]
    public void ScorchedFarmland_YieldsLess()
    {
        var rules = SettlementWorlds.Rules;
        var land = new FarmlandSurvey(9, 0.6f, 0f, MeanSoilMoisture: 1f, MeanScorch: 0f);

        var scorched = SettlementEconomy.ComputeProduction(rules, 30f, land with { MeanScorch = 0.5f });

        Assert.Equal(SettlementEconomy.ComputeProduction(rules, 30f, land) * (1f - (rules.ScorchYieldLoss * 0.5f)), scorched, 3);
    }

    [Fact]
    public void TheHomeless_FallIll()
    {
        var rules = SettlementWorlds.NoBirthsOrDeaths;
        var world = SettlementWorlds.Create(population: 100, housing: 50, food: 500f, fertility: 0.6f, health: 0.8f);

        var outlook = SettlementEconomy.Assess(rules, SettlementWorlds.Settlement(world), world.Regions);

        Assert.Equal(0.5f, SettlementEconomy.ComputeHomelessShare(SettlementWorlds.Settlement(world)), 4);
        Assert.Equal(0.8f + rules.HealthRecoveryPerStep - (rules.HomelessHealthLossPerStep * 0.5f), outlook.HealthAfter, 4);
    }

    [Fact]
    public void Ruins_AreRebuiltWithHalfTheWork_AndEachNewHouseClearsOne()
    {
        var rules = SettlementWorlds.NoBirthsOrDeaths;
        var world = SettlementWorlds.Create(population: 100, housing: 90, food: 2000f, fertility: 0.6f);
        var settlement = SettlementWorlds.Settlement(world);
        settlement.Ruins = 2;

        Assert.Equal(rules.WorkPerHouse * rules.RuinWorkShare, SettlementEconomy.ComputeHouseWork(rules, settlement));
        SettlementWorlds.RunSteps(world, rules, 15);

        Assert.True(settlement.Housing >= 95);
        Assert.Equal(2 - ((settlement.Housing - 90) / rules.HousingPerHouse), settlement.Ruins);
    }

    [Fact]
    public void Danger_IsAHardship_AndANeedAShieldAnswers()
    {
        var settlement = DivineWorlds.Create(0f, 300f).Settlements[0];

        var conditions = BeliefModel.AssessConditions(DivineWorlds.Rules, settlement, new Hazards(Drought: 0f, Danger: 0.5f));

        Assert.Equal(DivineWorlds.Rules.DangerWorry * 0.5f, conditions.Hardship, 4);
        Assert.Equal(0.5f, BeliefModel.NeedFor(conditions, MiracleNeed.Danger), 4);
    }

    [Fact]
    public void Fire_SetsTheTargetAlight_AndItsWitnessesFearTheGod()
    {
        var world = HomeWorld();
        DisasterWorlds.Parch(world);
        world.Gods[0].Rank = DivineRank.LesserGod;
        var events = new List<WorldEvent>();

        Assert.True(Caster().Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "fire", world.Settlements[0].Position), DivineWorlds.GodId, events).IsAccepted);

        Assert.True(world.Regions.Cells[world.Settlements[0].RegionId].Fire >= 0.8f);
        Assert.Contains(events.OfType<WildfireStarted>(), f => f.Cause == FireCause.Miracle);
        Assert.True(BeliefModel.Find(world.Settlements[0], DivineWorlds.GodId)!.Fear > 0.2f);
    }

    [Fact]
    public void Earthquake_ShakesTheGroundInTheGodsName()
    {
        var world = HomeWorld();
        world.Gods[0].Rank = DivineRank.God;
        var events = new List<WorldEvent>();

        Assert.True(Caster().Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "earthquake", world.Settlements[0].Position), DivineWorlds.GodId, events).IsAccepted);

        var quake = Assert.Single(world.Disasters.Earthquakes);
        Assert.Equal(DivineWorlds.GodId, quake.CausedByGodId);
        Assert.Equal(6.8f, quake.Magnitude);
        Assert.Equal(quake.Id, Assert.Single(events.OfType<EarthquakeStruck>()).QuakeId);
    }

    [Fact]
    public void Shield_ProtectsTheSettlementsItCovers_ForItsTime()
    {
        var world = HomeWorld();
        world.Gods[0].Rank = DivineRank.LesserGod;

        Assert.True(Caster().Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "shield", world.Settlements[0].Position), DivineWorlds.GodId, new List<WorldEvent>()).IsAccepted);

        Assert.Equal(world.Tick + (180 * SimulationTime.TicksPerSecond), world.Settlements[0].ShieldedUntilTick);
        Assert.Equal(0, world.Settlements[1].ShieldedUntilTick);
    }

    [Fact]
    public void FireOnARivalsFollowers_IsRememberedAsHarm()
    {
        var world = DiplomacyWorlds.Create(200f, 600f);
        DisasterWorlds.Parch(world);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f, godId: RivalWorlds.RivalId);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.3f, faith: 0.8f);
        world.Gods[0].DivinePower = 1000f;
        world.Gods[0].Rank = DivineRank.LesserGod;
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(DiplomacyWorlds.Rules), WeatherWorlds.Rules, DisasterWorlds.Rules);

        caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "fire", world.Settlements[0].Position), DivineWorlds.GodId, new List<WorldEvent>());

        Assert.Equal([DiplomaticAct.Harmed], DiplomacyModel.FindRelation(world, RivalWorlds.RivalId, DivineWorlds.GodId)!.Memories.Select(m => m.Act));
    }

    [Fact]
    public void AGuardian_ShieldsItsFollowersFromAFire()
    {
        var world = RivalWorlds.Create(GodPersonality.Guardian, 1000f, 300f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.9f, faith: 0.9f, love: 0.5f, godId: RivalWorlds.RivalId);
        world.Gods.First(g => g.Id == RivalWorlds.RivalId).Rank = DivineRank.LesserGod;
        world.Regions.Cells[settlement.RegionId].Fire = 0.9f;

        var best = Ai().Evaluate(world, world.Gods.First(g => g.Id == RivalWorlds.RivalId))[0];

        Assert.Equal("shield", best.MiracleId);
        Assert.Contains("shield", best.Reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ATyrant_NeverShakesItsOwnVillage()
    {
        var world = RivalWorlds.Create(GodPersonality.Tyrant, 5000f, 300f);
        var settlement = world.Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.9f, faith: 0.9f, godId: RivalWorlds.RivalId);
        world.Gods.First(g => g.Id == RivalWorlds.RivalId).Rank = DivineRank.God;

        var candidates = Ai().Evaluate(world, world.Gods.First(g => g.Id == RivalWorlds.RivalId));

        Assert.DoesNotContain(candidates, c => c.MiracleId is "earthquake" or "fire" && c.Utility > 0f);
    }

    /// <summary>A world with two towns whose god, with power to spare, is followed in the first.</summary>
    private static WorldState HomeWorld()
    {
        var world = DivineWorlds.Create(2000f, 300f, 900f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.6f);
        return world;
    }

    /// <summary>A caster with weather and disasters.</summary>
    private static MiracleCaster Caster() => new(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, weather: WeatherWorlds.Rules, disasters: DisasterWorlds.Rules);

    /// <summary>A god AI whose caster knows disasters.</summary>
    private static GodAi Ai() => new(RivalWorlds.AiRules, DivineWorlds.Rules, DivineWorlds.Miracles, Caster(), null, null);
}
