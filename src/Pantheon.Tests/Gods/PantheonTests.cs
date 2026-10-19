using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>Four gods (spec §124, ADR-015): blocs of allies, pantheon victory and allies' memory of wrongs.</summary>
public sealed class PantheonTests
{
    [Fact]
    public void Allies_CountTheirFollowersTogether_AndWinTogether()
    {
        var world = PantheonWorlds.Create(0.4f, 0.3f, 0.3f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        var system = new VictorySystem(DivineWorlds.Rules);
        var events = new List<WorldEvent>();
        var context = new TickContext(world, new SimulationRandom(1), events);

        system.Update(context);

        var (share, allies) = VictorySystem.StandingOf(DivineWorlds.Rules, world, 1);
        Assert.Equal(0.7f, share, 3);
        Assert.Equal([2], allies);
        Assert.Equal([0L, 0L, null, null], world.Gods.Select(g => g.DominantSinceTick));
        Assert.Equal(
            [(1, new[] { 2 }), (2, new[] { 1 })],
            events.OfType<DominanceChanged>().Select(e => (e.GodId, e.Allies.ToArray())));

        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        system.Update(context);

        Assert.Equal([1, 2], world.WinnerGodIds);
        Assert.Equal([1, 2], Assert.Single(events.OfType<VictoryAchieved>()).GodIds);
    }

    [Fact]
    public void WithoutPantheonVictory_EveryGodStandsAlone()
    {
        var rules = DivineWorlds.Rules with { PantheonVictory = false };
        var world = PantheonWorlds.Create(0.4f, 0.3f, 0.3f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);

        new VictorySystem(rules).Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.Equal((0.4f, 0), (VictorySystem.StandingOf(rules, world, 1).Share, VictorySystem.StandingOf(rules, world, 1).Allies.Count));
        Assert.All(world.Gods, god => Assert.Null(god.DominantSinceTick));
    }

    [Fact]
    public void AGodThatDominatesAlone_DoesNotShareItsVictory()
    {
        var world = PantheonWorlds.Create(0.8f, 0.1f, 0.1f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        var system = new VictorySystem(DivineWorlds.Rules);
        var events = new List<WorldEvent>();

        system.Update(new TickContext(world, new SimulationRandom(1), events));
        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        system.Update(new TickContext(world, new SimulationRandom(1), events));

        Assert.Empty(VictorySystem.StandingOf(DivineWorlds.Rules, world, 1).Allies);
        Assert.Equal([1], world.WinnerGodIds);
    }

    [Fact]
    public void BreakingTheAlliance_EndsItsDominance()
    {
        var world = PantheonWorlds.Create(0.4f, 0.3f, 0.3f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        var system = new VictorySystem(DivineWorlds.Rules);
        var events = new List<WorldEvent>();
        system.Update(new TickContext(world, new SimulationRandom(1), events));

        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Neutral);
        system.Update(new TickContext(world, new SimulationRandom(1), events));

        Assert.All(world.Gods, god => Assert.Null(god.DominantSinceTick));
        Assert.Equal(2, events.OfType<DominanceChanged>().Count(e => !e.Started));
    }

    [Fact]
    public void ABloc_ReachesAlliesOfAllies()
    {
        var world = PantheonWorlds.Create(0.5f, 0.5f, 0.5f, 0.5f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        PantheonWorlds.SetStatus(world, 3, 2, DiplomaticStatus.Alliance);
        PantheonWorlds.SetStatus(world, 1, 4, DiplomaticStatus.War);

        Assert.Equal([2], DiplomacyModel.AlliesOf(world, 1));
        Assert.Equal([1, 2, 3], DiplomacyModel.BlocOf(world, 3));
        Assert.Equal([4], DiplomacyModel.BlocOf(world, 4));
        Assert.Equal(0.75f, VictorySystem.StandingOf(DivineWorlds.Rules, world, 1).Share, 3);
    }

    [Fact]
    public void HarmingAGod_WrongsItsAllies_WhoRememberIt()
    {
        var world = PantheonWorlds.Create(0.5f, 0.5f, 0.5f, 0.5f);
        PantheonWorlds.SetStatus(world, 2, 3, DiplomaticStatus.Alliance);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);

        ledger.Record(world, 2, 1, DiplomaticAct.Harmed, 2);
        ledger.Record(world, 2, 1, DiplomaticAct.Helped, 2);

        var ally = DiplomacyModel.FindRelation(world, 3, 1)!;
        var memory = Assert.Single(ally.Memories);
        Assert.Equal((DiplomaticAct.WrongedAlly, (int?)2), (memory.Act, memory.SettlementId));
        Assert.Equal(DiplomacyModel.InitialWeight(DiplomaticAct.WrongedAlly), memory.Weight);
        Assert.Equal(0.5f - (DiplomacyWorlds.Rules.HarmTrustLoss / 2f), ally.Trust, 4);
        Assert.Equal(0.2f + (DiplomacyWorlds.Rules.HarmRivalry / 2f), ally.Rivalry, 4);
        Assert.True(DiplomacyModel.Grudge(ally) > 0f);
        Assert.Null(DiplomacyModel.FindRelation(world, 4, 1));
    }

    [Fact]
    public void AnAlly_IsNotWrongedByItsOwnActs()
    {
        var world = PantheonWorlds.Create(0.5f, 0.5f, 0.5f, 0.5f);
        PantheonWorlds.SetStatus(world, 2, 3, DiplomaticStatus.Alliance);

        new DiplomacyLedger(DiplomacyWorlds.Rules).Record(world, 2, 3, DiplomaticAct.Converted, 2);

        Assert.Null(DiplomacyModel.FindRelation(world, 3, 3));
        Assert.Single(DiplomacyModel.FindRelation(world, 2, 3)!.Memories);
    }

    [Fact]
    public void FourGods_PlayOutTheSameWayEachTime_AcrossASave()
    {
        var content = TestContent.Small;
        var engine = content.CreateEngine();
        var straight = Pantheon.Game.GameSession.CreateNew(content, new WorldSeed(19)).World;
        var resumed = Pantheon.Game.GameSession.CreateNew(content, new WorldSeed(19)).World;

        engine.Run(straight, 3000);
        engine.Run(resumed, 1500);
        resumed = Pantheon.Persistence.WorldSaveSerializer.Deserialize(Pantheon.Persistence.WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 1500);

        Assert.Equal(4, straight.Gods.Count);
        WorldAssert.Equal(straight, resumed);
        Assert.Contains(straight.Diplomacy.Relations, r => r.ObserverGodId > 2 && r.SubjectGodId > 1);
    }
}
