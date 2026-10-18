using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Miracles;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The rules of belief, conversion and worship (spec §7–9, ADR-009).</summary>
public sealed class BeliefModelTests
{
    private static readonly DivineRules Rules = DivineWorlds.Rules;
    private static readonly SettlementConditions Safe = new(Hardship: 0f, Comfort: 1f, FoodNeed: 0f, HealthNeed: 0f);
    private static readonly SettlementConditions Famine = new(Hardship: 1f, Comfort: 0f, FoodNeed: 1f, HealthNeed: 0.5f);

    [Fact]
    public void Fear_DecreasesAfterProlongedSafety()
    {
        var belief = Belief(adherence: 0.5f, faith: 0.5f, fear: 0.8f);

        Step(belief, Safe, steps: 120);

        Assert.InRange(belief.Fear, 0.35f, 0.45f);
    }

    [Fact]
    public void Fear_LingersInHardship()
    {
        var belief = Belief(adherence: 0.5f, faith: 0.5f, fear: 0.8f);

        Step(belief, Famine, steps: 120);

        Assert.Equal(0.8f, belief.Fear, 3);
    }

    [Fact]
    public void Doubt_RisesAmongFollowersInHardship_ButNotAmongUnbelievers()
    {
        var follower = Belief(adherence: 0.8f, faith: 0.6f, doubt: 0.2f);
        var unbeliever = Belief(adherence: 0f, faith: 0.6f, doubt: 0.2f);

        Step(follower, Famine, steps: 30);
        Step(unbeliever, Famine, steps: 30);

        Assert.True(follower.Doubt > 0.45f, $"follower doubt {follower.Doubt}");
        Assert.InRange(unbeliever.Doubt, 0.2f, 0.23f);
    }

    [Fact]
    public void Love_GrowsAmongComfortableFollowers_AndWearsDownInHardship()
    {
        var comfortable = Belief(adherence: 0.9f, faith: 0.6f, love: 0.3f);
        var suffering = Belief(adherence: 0.9f, faith: 0.6f, love: 0.3f);

        Step(comfortable, Safe, steps: 100);
        Step(suffering, Famine, steps: 100);

        Assert.True(comfortable.Love > 0.35f, $"comfortable love {comfortable.Love}");
        Assert.True(suffering.Love < 0.15f, $"suffering love {suffering.Love}");
    }

    [Fact]
    public void Faith_IsSustainedByWorship_AndForgottenByUnbelievers()
    {
        var followers = Belief(adherence: 0.9f, faith: 0.5f, doubt: 0.25f);
        var witnesses = Belief(adherence: 0f, faith: 0.5f, doubt: 0.25f);

        Step(followers, Safe, steps: 200);
        Step(witnesses, Safe, steps: 200);

        Assert.True(followers.Faith > 0.6f, $"followers' faith {followers.Faith}");
        Assert.True(witnesses.Faith < 0.15f, $"witnesses' faith {witnesses.Faith}");
    }

    [Fact]
    public void Fear_ConvertsFasterThanLove()
    {
        var feared = Belief(adherence: 0f, faith: 0.5f, fear: 0.6f, doubt: 0.2f);
        var loved = Belief(adherence: 0f, faith: 0.5f, love: 0.6f, doubt: 0.2f);

        Assert.True(BeliefModel.ComputePull(Rules, feared) > BeliefModel.ComputePull(Rules, loved));
    }

    [Fact]
    public void Love_HoldsFollowers_WhileFearDrivesThemAway()
    {
        var loved = Belief(adherence: 0.8f, faith: 0.5f, love: 0.8f, doubt: 0.5f);
        var feared = Belief(adherence: 0.8f, faith: 0.5f, fear: 0.8f, doubt: 0.5f);
        var indifferent = Belief(adherence: 0.8f, faith: 0.5f, doubt: 0.5f);

        var lovedPush = BeliefModel.ComputePush(Rules, loved);
        var fearedPush = BeliefModel.ComputePush(Rules, feared);
        var indifferentPush = BeliefModel.ComputePush(Rules, indifferent);

        Assert.True(lovedPush < indifferentPush);
        Assert.True(fearedPush > indifferentPush);
    }

    [Fact]
    public void Doubt_HoldsBackConversion()
    {
        var certain = Belief(adherence: 0f, faith: 0.6f, love: 0.4f, doubt: 0f);
        var doubtful = Belief(adherence: 0f, faith: 0.6f, love: 0.4f, doubt: 0.8f);

        Assert.True(BeliefModel.ComputePull(Rules, doubtful) < BeliefModel.ComputePull(Rules, certain) / 4f);
    }

    [Fact]
    public void AdherenceChanges_NeverExceedTheFreeShare()
    {
        var settlement = DivineWorlds.Create(0f, 100f).Settlements[0];
        DivineWorlds.Believe(settlement, adherence: 0.45f, faith: 1f, love: 1f, fear: 1f, godId: 1);
        DivineWorlds.Believe(settlement, adherence: 0.45f, faith: 1f, love: 1f, fear: 1f, godId: 2);
        var greedy = Rules with { ConversionPerStep = 1f, ApostasyPerStep = 0f };

        var changes = BeliefModel.ComputeAdherenceChanges(greedy, settlement.Beliefs);

        Assert.Equal(0.1f, changes[0] + changes[1], 4);
        Assert.Equal(changes[0], changes[1], 5);
    }

    [Fact]
    public void Conditions_ReportHungerAndIllness()
    {
        var settlement = DivineWorlds.Create(0f, 100f).Settlements[0];
        settlement.Food = 50f;
        settlement.Health = 0.7f;

        var conditions = BeliefModel.AssessConditions(Rules, settlement);

        // 100 people want 200 food in store: a quarter is there.
        Assert.Equal(0.75f, conditions.FoodNeed, 4);
        Assert.Equal(0.3f, conditions.HealthNeed, 4);
        Assert.Equal(0.75f, conditions.Hardship, 4);
        Assert.Equal(0.7f * 0.25f, conditions.Comfort, 4);
        Assert.Equal(0.75f, BeliefModel.NeedFor(conditions, MiracleNeed.Food), 4);
        Assert.Equal(1f, BeliefModel.NeedFor(conditions, MiracleNeed.None));
    }

    [Fact]
    public void Power_IsFollowersTimesFaithTimesIntensityTimesInfrastructure()
    {
        var world = DivineWorlds.Create(0f, 100f);
        var settlement = world.Settlements[0];
        var belief = DivineWorlds.Believe(settlement, adherence: 0.5f, faith: 0.8f, love: 0.5f, fear: 0.25f);
        var settlementRules = SettlementWorlds.Rules;

        // Workers 100 × 0.6 = 60; worshippers 60 × 0.15 × 0.5 × 0.8 = 3.6;
        // intensity 0.5 + 0.3 × 0.5 + 0.6 × 0.25 = 0.8; power 3.6 × 0.8 × 1 × 1.5 = 4.32.
        Assert.Equal(3.6f, BeliefModel.ComputeWorshippers(settlementRules, settlement, belief), 4);
        Assert.Equal(0.8f, BeliefModel.ComputeWorshipIntensity(Rules, belief), 4);
        Assert.Equal(4.32f, BeliefModel.ComputePower(Rules, settlementRules, settlement, belief), 4);
        Assert.Equal(4.32f, GodStanding.ComputePowerPerStep(Rules, settlementRules, world, DivineWorlds.GodId), 4);
    }

    [Fact]
    public void Worshippers_LeaveTheFields()
    {
        var settlement = DivineWorlds.Create(0f, 100f).Settlements[0];
        var heathen = SettlementEconomy.AllocateLabour(SettlementWorlds.Rules, settlement);
        DivineWorlds.Believe(settlement, adherence: 1f, faith: 1f);

        var devout = SettlementEconomy.AllocateLabour(SettlementWorlds.Rules, settlement);

        Assert.Equal(0f, heathen.Worshippers);
        Assert.Equal(9f, devout.Worshippers, 4);
        Assert.Equal(heathen.Farmers - 9f, devout.Farmers, 4);
        Assert.Equal(1f, SettlementEconomy.ComputeDevotion(settlement));
    }

    [Theory]
    [InlineData(0.5f, 0.5f, 0.75f)]
    [InlineData(0.5f, -0.5f, 0.25f)]
    [InlineData(0f, 1f, 1f)]
    [InlineData(1f, -1f, 0f)]
    [InlineData(0.2f, 3f, 1f)]
    public void ApplyChange_ClosesTheGapUpOrRemovesAShareDown(float value, float change, float expected)
    {
        Assert.Equal(expected, BeliefModel.ApplyChange(value, change), 5);
    }

    [Fact]
    public void Impulse_ScalesLoveByNeedButNotLosses()
    {
        var belief = Belief(adherence: 0f, faith: 0f, love: 0.5f);
        var impulse = new BeliefImpulse { Faith = 0.2f, Love = 0.4f, Fear = 0f, Doubt = 0f };

        BeliefModel.ApplyImpulse(belief, impulse, weight: 1f, loveScale: 0.5f);

        Assert.Equal(0.2f, belief.Faith, 5);
        Assert.Equal(0.6f, belief.Love, 5);

        BeliefModel.ApplyImpulse(belief, impulse with { Love = -0.5f }, weight: 1f, loveScale: 0f);
        Assert.Equal(0.3f, belief.Love, 5);
    }

    /// <summary>A free-standing belief.</summary>
    private static GodBelief Belief(float adherence, float faith, float love = 0f, float fear = 0f, float doubt = 0f) =>
        new() { GodId = 1, Adherence = adherence, Faith = faith, Love = love, Fear = fear, Doubt = doubt };

    /// <summary>Runs the drift (not the conversion) several times.</summary>
    private static void Step(GodBelief belief, SettlementConditions conditions, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            (belief.Faith, belief.Love, belief.Fear, belief.Doubt) = BeliefModel.Drift(Rules, belief, conditions);
        }
    }
}
