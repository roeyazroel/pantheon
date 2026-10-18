using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The belief step: drift and conversion, Divine Power, ranks, events and fading blessings.</summary>
public sealed class BeliefSystemTests
{
    private static readonly SimulationEngine Engine = new(SettlementWorlds.NoBirthsOrDeaths, DivineWorlds.Rules);

    [Fact]
    public void BeliefStep_RunsOnceEveryInterval()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f);

        Engine.Run(world, 9);
        Assert.Equal(0f, world.Gods[0].DivinePower);

        Engine.Run(world, 1);
        Assert.True(world.Gods[0].DivinePower > 0f);
    }

    [Fact]
    public void God_GainsTheDivinePowerItsWorshippersRaise()
    {
        var world = DivineWorlds.Create(10f, 300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.25f, faith: 0.4f);
        var expected = GodStanding.ComputePowerPerStep(DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths, world, DivineWorlds.GodId);
        var step = new BeliefSystem(DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths);

        var copy = DivineWorlds.Create(10f, 300f, 700f);
        step.Update(new TickContext(world, new Pantheon.Simulation.Randomness.SimulationRandom(1), []));

        Assert.True(expected > 0f);
        Assert.Equal(10f + GodStanding.ComputePowerPerStep(DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths, world, DivineWorlds.GodId), world.Gods[0].DivinePower, 1);
        Assert.Equal(10f, copy.Gods[0].DivinePower);
    }

    [Fact]
    public void FaithfulSettlement_ConvertsTheRestOfItsPeople()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.2f, faith: 0.7f, love: 0.5f, doubt: 0.1f);

        Engine.Run(world, 10 * 120);

        Assert.True(belief.Adherence > 0.6f, $"adherence {belief.Adherence}");
    }

    [Fact]
    public void UnhelpedFamine_TurnsFollowersAway()
    {
        var fed = DivineWorlds.Create(0f, 300f);
        var hungry = DivineWorlds.Create(0f, 300f);
        var fedBelief = DivineWorlds.Believe(fed.Settlements[0], adherence: 0.8f, faith: 0.7f, love: 0.5f, doubt: 0.2f);
        var hungryBelief = DivineWorlds.Believe(hungry.Settlements[0], adherence: 0.8f, faith: 0.7f, love: 0.5f, doubt: 0.2f);
        foreach (var region in hungry.Regions.Cells)
        {
            region.Fertility = 0f;
        }

        hungry.Settlements[0].Food = 0f;

        Engine.Run(fed, 10 * 60);
        Engine.Run(hungry, 10 * 60);

        Assert.True(hungryBelief.Adherence < fedBelief.Adherence - 0.2f, $"hungry {hungryBelief.Adherence}, fed {fedBelief.Adherence}");
        Assert.True(hungryBelief.Doubt > 0.5f);
    }

    [Fact]
    public void Rank_RisesWithFollowers_RaisesAnEvent_AndNeverFalls()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var settlement = world.Settlements[0];
        settlement.Population = 110;
        settlement.Housing = 120;
        DivineWorlds.Believe(settlement, adherence: 1f, faith: 1f, love: 1f);
        var events = new List<WorldEvent>();

        for (var i = 0; i < 10; i++)
        {
            Engine.Step(world, events);
        }

        Assert.Equal(DivineRank.LesserGod, world.Gods[0].Rank);
        var achieved = Assert.Single(events.OfType<RankAchieved>());
        Assert.Equal(DivineRank.LesserGod, achieved.Rank);

        settlement.Population = 5;
        Engine.Run(world, 10);
        Assert.Equal(DivineRank.LesserGod, world.Gods[0].Rank);
    }

    [Fact]
    public void GainingAndLosingTheMajority_RaisesEvents()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.495f, faith: 1f, love: 1f);
        var events = new List<WorldEvent>();

        for (var i = 0; i < 10; i++)
        {
            Engine.Step(world, events);
        }

        var gained = Assert.Single(events.OfType<FollowingChanged>());
        Assert.True(gained.Gained);
        Assert.Equal(world.Settlements[0].Id, gained.SettlementId);

        belief.Adherence = 0.505f;
        belief.Faith = 0f;
        belief.Love = 0f;
        belief.Doubt = 1f;
        events.Clear();
        for (var i = 0; i < 10; i++)
        {
            Engine.Step(world, events);
        }

        Assert.False(Assert.Single(events.OfType<FollowingChanged>()).Gained);
    }

    [Fact]
    public void ForgottenBelief_IsRemoved()
    {
        var world = DivineWorlds.Create(0f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0f, faith: 0.005f, doubt: 0.5f);

        Engine.Run(world, 10);

        Assert.Empty(world.Settlements[0].Beliefs);
    }

    [Fact]
    public void Blessing_FadesEachStep()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Regions.Cells[5].Blessing = 1f;
        world.Regions.Cells[6].Blessing = 0.0005f;

        Engine.Run(world, 10);

        Assert.Equal(1f - DivineWorlds.Rules.BlessingDecayPerStep, world.Regions.Cells[5].Blessing, 5);
        Assert.Equal(0f, world.Regions.Cells[6].Blessing);
    }

    [Fact]
    public void SameBeliefStateAndRules_GiveTheSameWorld()
    {
        var first = DivineWorlds.Create(5f, 300f, 700f);
        var second = DivineWorlds.Create(5f, 300f, 700f);
        DivineWorlds.Believe(first.Settlements[0], 0.3f, 0.6f, 0.2f, 0.4f, 0.3f);
        DivineWorlds.Believe(second.Settlements[0], 0.3f, 0.6f, 0.2f, 0.4f, 0.3f);
        var engine = new SimulationEngine(TestContent.BusyRules, DivineWorlds.Rules);

        engine.Run(first, 600);
        engine.Run(second, 600);

        WorldAssert.Equal(first, second);
    }
}
