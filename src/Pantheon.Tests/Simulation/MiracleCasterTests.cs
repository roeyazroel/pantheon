using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The authority checks every cast (spec §73) and applies the miracles' effects.</summary>
public sealed class MiracleCasterTests
{
    private static readonly MiracleCaster Caster = new(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);
    private static readonly GroundPosition Home = new(200f, 512f);

    [Fact]
    public void God_CannotCastWithoutEnoughPower()
    {
        var world = HomeWorld(power: 10f);
        var before = WorldSaveSerializer.Serialize(world);
        var events = new List<WorldEvent>();

        var check = Caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "food", Home), DivineWorlds.GodId, events);

        Assert.Equal(CastRejection.NotEnoughPower, check.Rejection);
        Assert.Equal(before, WorldSaveSerializer.Serialize(world));
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(2, "food", CastRejection.UnknownGod)]
    [InlineData(1, "earthshatter", CastRejection.UnknownMiracle)]
    [InlineData(1, "mass_healing", CastRejection.RankTooLow)]
    public void Check_RejectsUnknownAndLockedRequests(int godId, string miracleId, CastRejection expected)
    {
        var world = HomeWorld(power: 1000f);

        Assert.Equal(expected, Caster.Check(world, new CastMiracleRequest(godId, miracleId, Home), issuerGodId: null).Rejection);
    }

    [Fact]
    public void Check_RejectsAnIssuerActingForAnotherGod()
    {
        var world = HomeWorld(power: 1000f);

        Assert.Equal(CastRejection.NotAuthorized, Caster.Check(world, new CastMiracleRequest(1, "food", Home), issuerGodId: 7).Rejection);
        Assert.Equal(CastRejection.None, Caster.Check(world, new CastMiracleRequest(1, "food", Home), issuerGodId: null).Rejection);
    }

    [Fact]
    public void Check_RejectsTargetsOutsideTheWorldOrTheInfluence()
    {
        var world = HomeWorld(power: 1000f);

        Assert.Equal(CastRejection.OutsideWorld, Caster.Check(world, new CastMiracleRequest(1, "food", new GroundPosition(-5f, 10f)), 1).Rejection);
        var far = Caster.Check(world, new CastMiracleRequest(1, "food", new GroundPosition(1000f, 512f)), 1);
        Assert.Equal(CastRejection.OutsideInfluence, far.Rejection);
        Assert.True(far.Influence < far.Miracle!.InfluenceRequirement);
    }

    [Fact]
    public void Miracle_CoolsDown_ThenIsReadyAgain()
    {
        var world = HomeWorld(power: 1000f);
        var food = DivineWorlds.Miracles.Find("food")!;
        Caster.Cast(world, new CastMiracleRequest(1, "food", Home), 1, new List<WorldEvent>());

        var cooling = Caster.Check(world, new CastMiracleRequest(1, "food", Home), 1);
        world.Tick += food.CooldownTicks;
        var ready = Caster.Check(world, new CastMiracleRequest(1, "food", Home), 1);

        Assert.Equal(CastRejection.OnCooldown, cooling.Rejection);
        Assert.Equal(food.CooldownTicks, cooling.CooldownTicksLeft);
        Assert.Equal(CastRejection.None, ready.Rejection);
    }

    [Fact]
    public void Cost_RisesWhereInfluenceIsWeak()
    {
        var world = HomeWorld(power: 1000f);
        var near = Caster.Check(world, new CastMiracleRequest(1, "food", Home), 1);
        var edge = Caster.Check(world, new CastMiracleRequest(1, "food", new GroundPosition(520f, 512f)), 1);

        Assert.True(near.Influence > edge.Influence);
        Assert.True(edge.Cost > near.Cost);
        Assert.Equal(MiracleCaster.ComputeCost(DivineWorlds.Rules, near.Miracle!, near.Influence), near.Cost);
        Assert.Equal(near.Miracle!.PowerCost * 2f, MiracleCaster.ComputeCost(DivineWorlds.Rules, near.Miracle, 0f));
    }

    [Fact]
    public void Food_FillsTheStoresUpToStorage_SpendsPowerAndRaisesAnEvent()
    {
        var world = HomeWorld(power: 1000f);
        var settlement = world.Settlements[0];
        settlement.Food = 10f;
        var events = new List<WorldEvent>();
        var amount = DivineWorlds.Miracles.Find("food")!.Effects[0].Amount;

        var check = Caster.Cast(world, new CastMiracleRequest(1, "food", Home), 1, events);

        Assert.True(check.IsAccepted);
        Assert.Equal(10f + amount, settlement.Food);
        Assert.Equal(1000f - check.Cost, world.Gods[0].DivinePower, 3);
        var cast = Assert.Single(events.OfType<MiracleCast>());
        Assert.Equal([settlement.Id], cast.AffectedSettlementIds);
        Assert.Equal("food", cast.MiracleId);

        settlement.Food = SettlementEconomy.ComputeStorage(SettlementWorlds.Rules, settlement.Housing) - 1f;
        world.Tick += 1000;
        Caster.Cast(world, new CastMiracleRequest(1, "food", Home), 1, events);
        Assert.Equal(SettlementEconomy.ComputeStorage(SettlementWorlds.Rules, settlement.Housing), settlement.Food);
    }

    [Fact]
    public void Heal_RestoresHealth()
    {
        var world = HomeWorld(power: 1000f);
        world.Settlements[0].Health = 0.4f;

        Caster.Cast(world, new CastMiracleRequest(1, "heal", Home), 1, new List<WorldEvent>());

        Assert.Equal(0.4f + DivineWorlds.Miracles.Find("heal")!.Effects[0].Amount, world.Settlements[0].Health, 5);
    }

    [Fact]
    public void Lightning_KillsAndFrightens()
    {
        var world = HomeWorld(power: 1000f);
        var settlement = world.Settlements[0];
        var belief = settlement.Beliefs[0];
        var fearBefore = belief.Fear;
        var loveBefore = belief.Love;
        var events = new List<WorldEvent>();

        Caster.Cast(world, new CastMiracleRequest(1, "lightning", Home), 1, events);

        Assert.Equal(97, settlement.Population);
        Assert.Equal(3, Assert.Single(events.OfType<MiracleCast>()).Deaths);
        Assert.True(belief.Fear > fearBefore);
        Assert.True(belief.Love < loveBefore);
    }

    [Fact]
    public void BlessCrops_BlessesTheLandAndRaisesTheHarvest()
    {
        var world = HomeWorld(power: 1000f);
        var settlement = world.Settlements[0];
        var before = SettlementEconomy.Assess(SettlementWorlds.Rules, settlement, world.Regions).Production;

        Caster.Cast(world, new CastMiracleRequest(1, "bless_crops", Home), 1, new List<WorldEvent>());

        Assert.Equal(1f, world.Regions.GetRegion(settlement.RegionId).Blessing);
        Assert.Contains(world.Regions.Cells, cell => cell.Blessing == 0f);
        var after = SettlementEconomy.Assess(SettlementWorlds.Rules, settlement, world.Regions).Production;
        Assert.True(after > before * 1.1f, $"before {before}, after {after}");
    }

    [Fact]
    public void Gratitude_IsGreaterWhereTheMiracleWasNeeded()
    {
        var full = HomeWorld(power: 1000f);
        var starving = HomeWorld(power: 1000f);
        starving.Settlements[0].Food = 0f;
        var fullLove = full.Settlements[0].Beliefs[0].Love;

        Caster.Cast(full, new CastMiracleRequest(1, "food", Home), 1, new List<WorldEvent>());
        Caster.Cast(starving, new CastMiracleRequest(1, "food", Home), 1, new List<WorldEvent>());

        var fullGain = full.Settlements[0].Beliefs[0].Love - fullLove;
        var starvingGain = starving.Settlements[0].Beliefs[0].Love - fullLove;
        Assert.True(starvingGain > fullGain * 3f, $"full {fullGain}, starving {starvingGain}");
    }

    [Fact]
    public void Witnesses_FeelLessFurtherAway_AndStrangersStartToBelieve()
    {
        // The target town follows the god; a stranger town 300 m away watches; one 800 m away does not.
        var world = DivineWorlds.Create(1000f, 200f, 500f, 1000f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        var food = DivineWorlds.Miracles.Find("food")!;

        Caster.Cast(world, new CastMiracleRequest(1, "food", Home), 1, new List<WorldEvent>());

        var watcher = Assert.Single(world.Settlements[1].Beliefs);
        Assert.Equal(0f, watcher.Adherence);
        Assert.InRange(watcher.Faith, 0.01f, food.Witness.Faith - 0.01f);
        Assert.True(watcher.Doubt < DivineWorlds.Rules.NewBelieverDoubt);
        Assert.Empty(world.Settlements[2].Beliefs);
    }

    /// <summary>A world whose god is followed in the one settlement it holds.</summary>
    private static WorldState HomeWorld(float power)
    {
        var world = DivineWorlds.Create(power, Home.X);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f, love: 0.3f, fear: 0.1f, doubt: 0.15f);
        return world;
    }
}
