using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>Religious competition (spec §120): conversion between gods, contested influence, rival doubt and the AI god.</summary>
public sealed class RivalGodTests
{
    [Fact]
    public void FollowersWhoLeaveAGod_GoToTheRivalThatPullsThem()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 0f, 200f);
        var settlement = world.Settlements[0];
        var doubting = DivineWorlds.Believe(settlement, adherence: 0.6f, faith: 0.3f, doubt: 0.9f);
        var rival = DivineWorlds.Believe(settlement, adherence: 0.3f, faith: 0.9f, love: 0.8f, doubt: 0f, godId: RivalWorlds.RivalId);

        var changes = BeliefModel.ComputeAdherenceChanges(DivineWorlds.Rules, settlement.Beliefs);

        Assert.True(changes[0] < 0f);
        Assert.True(changes[1] > 0f);
        Assert.True(changes[1] > -changes[0] * 0.5f, $"rival gain {changes[1]}, loss {changes[0]}");
        Assert.True(BeliefModel.TotalAdherence(settlement.Beliefs) + changes.Sum() <= 1f + 1e-5f);
    }

    [Fact]
    public void ARivalsMiracle_PlantsDoubtAboutTheOtherGod()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 1000f, 200f);
        var settlement = world.Settlements[0];
        var mine = DivineWorlds.Believe(settlement, adherence: 0.5f, faith: 0.7f, doubt: 0.1f);
        DivineWorlds.Believe(settlement, adherence: 0.3f, faith: 0.6f, godId: RivalWorlds.RivalId);
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);

        var check = caster.Cast(world, new CastMiracleRequest(RivalWorlds.RivalId, "food", settlement.Position), RivalWorlds.RivalId, new List<WorldEvent>());

        Assert.True(check.IsAccepted);
        Assert.True(mine.Doubt > 0.1f);
    }

    [Fact]
    public void CastingWhereARivalHoldsMoreInfluence_CostsMore()
    {
        var food = DivineWorlds.Miracles.Find("food")!;

        var alone = MiracleCaster.ComputeCost(DivineWorlds.Rules, food, influence: 0.4f, rivalInfluence: 0f);
        var contested = MiracleCaster.ComputeCost(DivineWorlds.Rules, food, influence: 0.4f, rivalInfluence: 0.9f);
        var ahead = MiracleCaster.ComputeCost(DivineWorlds.Rules, food, influence: 0.9f, rivalInfluence: 0.4f);

        Assert.Equal(alone * (1f + (DivineWorlds.Rules.ContestedCostFactor * 0.5f)), contested, 3);
        Assert.Equal(MiracleCaster.ComputeCost(DivineWorlds.Rules, food, 0.9f), ahead);
    }

    [Fact]
    public void Guardian_HelpsItsFollowersInNeed()
    {
        var world = RivalWorlds.Create(GodPersonality.Guardian, 500f, 200f, 700f);
        var home = world.Settlements[1];
        DivineWorlds.Believe(home, adherence: 0.7f, faith: 0.6f, godId: RivalWorlds.RivalId);
        home.Food = 0f;
        home.Health = 0.4f;

        var best = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1])[0];

        Assert.Equal(AiActionKind.CastMiracle, best.Kind);
        Assert.Contains(best.MiracleId, new[] { "food", "heal" });
        Assert.Equal(home.Id, best.SettlementId);
        Assert.Contains("protect", best.Reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Tyrant_StrikesItsRivalsFollowers_AndExplainsWhy()
    {
        var world = RivalWorlds.Create(GodPersonality.Tyrant, 500f, 200f, 450f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.8f, godId: RivalWorlds.RivalId);

        var best = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1])[0];

        Assert.Equal("lightning", best.MiracleId);
        Assert.Equal(world.Settlements[0].Id, best.SettlementId);
        Assert.StartsWith("intimidate Town1", best.Reason, System.StringComparison.Ordinal);
    }

    [Fact]
    public void PoorGod_Waits()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 5f, 200f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f, godId: RivalWorlds.RivalId);

        var candidates = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1]);

        Assert.Equal(AiActionKind.Wait, Assert.Single(candidates).Kind);
    }

    [Fact]
    public void AiGod_ActsThroughTheAuthority()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 500f, 200f, 450f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.8f, godId: RivalWorlds.RivalId);
        var events = new List<WorldEvent>();

        var taken = RivalWorlds.CreateAi().Act(world, world.Gods[1], new SimulationRandom(3), events);

        Assert.Equal(AiActionKind.CastMiracle, taken.Kind);
        var cast = Assert.Single(events.OfType<MiracleCast>());
        Assert.Equal(RivalWorlds.RivalId, cast.GodId);
        Assert.Equal(500f - taken.Cost, world.Gods[1].DivinePower, 2);
    }

    [Fact]
    public void Victory_GoesToTheGodThatHoldsTheShareLongEnough()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 0f, 200f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.2f, faith: 0.5f, godId: RivalWorlds.RivalId);
        var system = new VictorySystem(DivineWorlds.Rules);
        var events = new List<WorldEvent>();
        var context = new TickContext(world, new SimulationRandom(1), events);

        system.Update(context);
        Assert.Equal(0L, world.Gods[0].DominantSinceTick);
        Assert.Contains(events, e => e is DominanceChanged { GodId: 1, Started: true });

        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        system.Update(context);

        Assert.Equal([1], world.WinnerGodIds);
        Assert.Single(events.OfType<VictoryAchieved>());
        Assert.Equal(0.9f * 100 / ((0.9f * 100) + (0.2f * 100)), VictorySystem.ShareOf(world, 1), 2);
    }

    [Fact]
    public void Dominance_EndsWhenTheShareFalls_AndOneGodAloneCannotWin()
    {
        var world = RivalWorlds.Create(GodPersonality.Zealot, 0f, 200f, 700f);
        var mine = DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.5f, faith: 0.5f, godId: RivalWorlds.RivalId);
        var system = new VictorySystem(DivineWorlds.Rules);
        var events = new List<WorldEvent>();

        system.Update(new TickContext(world, new SimulationRandom(1), events));
        mine.Adherence = 0.1f;
        system.Update(new TickContext(world, new SimulationRandom(1), events));

        Assert.Null(world.Gods[0].DominantSinceTick);
        Assert.Contains(events, e => e is DominanceChanged { Started: false });

        world.Gods.RemoveAt(1);
        world.Tick = 1_000_000;
        system.Update(new TickContext(world, new SimulationRandom(1), events));
        Assert.Empty(world.WinnerGodIds);
    }

    [Fact]
    public void NewWorld_ContestIsPlayedOut_TheSameWayEachTime()
    {
        var first = Pantheon.Game.GameSession.CreateNew(TestContent.Small, new WorldSeed(31));
        var second = Pantheon.Game.GameSession.CreateNew(TestContent.Small, new WorldSeed(31));

        first.RunTicks(1800);
        second.RunTicks(1800);

        WorldAssert.Equal(first.World, second.World);
        Assert.Contains(first.World.Settlements, s => s.Beliefs.Any(b => b.GodId == RivalWorlds.RivalId && b.Adherence > 0f));
    }
}
