using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Gods;
using Pantheon.Game.Minds.Agents;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>A god's mind acts through the same authorities as a player (ADR-072): what the rules allow is done, and what they refuse is told back.</summary>
public sealed class GodToolsTests
{
    [Fact]
    public void Casting_OnItsOwnVillage_IsDone_AndPaidFor()
    {
        var (session, rival, _) = AgentWorlds.Setup();
        rival.DivinePower = 5000f;
        var home = GodPlanner.Home(session.World, rival.Id)!;

        var results = GodTools.Do(session, rival, [new GodAct("cast", home.Name.ToUpperInvariant(), "heal")], AgentWorlds.Rules);

        Assert.StartsWith("cast " + home.Name.ToUpperInvariant() + " (heal): done", Assert.Single(results));
        Assert.True(rival.DivinePower < 5000f);
    }

    [Theory]
    [InlineData("cast", "Nowhere", "heal", "not done: there is no such village")]
    [InlineData("cast", "HOME", "nonsense", "refused:")]
    [InlineData("declare_war", "Nobody", "", "not done: there is no such god")]
    [InlineData("summon_dragons", "", "", "not done: there is no such act")]
    [InlineData("propose_treaty", "PLAYER", "joint_war", "not done: name the god it is against")]
    [InlineData("propose_treaty", "PLAYER", "eternal_love", "not done: there is no such treaty")]
    public void WhatTheRulesDoNotAllow_IsNotDone_AndSaysWhy(string tool, string target, string detail, string said)
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var named = target.Replace("HOME", GodPlanner.Home(session.World, rival.Id)!.Name).Replace("PLAYER", session.World.Gods.Single(g => g.Id == me).Name);

        var result = Assert.Single(GodTools.Do(session, rival, [new GodAct(tool, named, detail)], AgentWorlds.Rules));

        Assert.Contains(said, result);
    }

    [Fact]
    public void DeclaringWar_OnAGodItKnows_MakesWar()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var player = session.World.Gods.Single(g => g.Id == me);

        var result = Assert.Single(GodTools.Do(session, rival, [new GodAct("declare_war", "the " + player.Name, string.Empty)], AgentWorlds.Rules));

        Assert.EndsWith(": done", result);
        Assert.True(DiplomacyModel.AreAtWar(session.World, rival.Id, me));
    }

    [Fact]
    public void ProposingATreaty_LeavesAProposalForTheOtherGod()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var player = session.World.Gods.Single(g => g.Id == me);

        var result = Assert.Single(GodTools.Do(session, rival, [new GodAct("propose_treaty", player.Name, "non_aggression")], AgentWorlds.Rules));

        Assert.EndsWith(": proposed", result);
        Assert.Equal(ProposalKind.Treaty, DiplomacyAuthority.FindProposal(session.World, rival.Id, me)?.Kind);
    }

    [Fact]
    public void SendingTheCreature_ToAVillage_SetsItOnItsWay()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var target = GodPlanner.Home(session.World, me)!;

        Assert.EndsWith(": done", Assert.Single(GodTools.Do(session, rival, [new GodAct("send_creature", target.Name, string.Empty)], AgentWorlds.Rules)));

        var creature = session.World.Creatures.Single(c => c.Id == rival.CreatureId);
        Assert.Equal((CreatureAction.GoTo, CreatureSituation.Commanded), (creature.Activity.Action, creature.Activity.Situation));
    }

    [Fact]
    public void Aims_AreSetTogether_WhenTheyMakeSense_AndHeldFromThePlanner()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var world = session.World;
        var player = world.Gods.Single(g => g.Id == me);
        var theirs = GodPlanner.Home(world, me)!;
        var own = GodPlanner.Home(world, rival.Id)!;

        var results = GodTools.Do(session, rival, [new GodAct("aim", player.Name, "weaken"), new GodAct("aim", own.Name, "convert"), new GodAct("aim", "Nowhere", "convert")], AgentWorlds.Rules);

        Assert.Equal(3, results.Count);
        Assert.EndsWith("weaken): taken", results.Single(r => r.Contains("weaken")));
        Assert.Contains("not taken", results.Single(r => r.Contains(own.Name)));
        Assert.Contains("not done", results.Single(r => r.Contains("Nowhere")));
        var goal = Assert.Single(rival.Goals);
        Assert.Equal((GodGoalKind.Weaken, (int?)me), (goal.Kind, goal.GodId));
        Assert.True(goal.Weight >= AgentWorlds.Rules.AimWeight);
        Assert.True(rival.NextPlanTick >= world.Tick + (long)(AgentWorlds.Rules.AimHoldSeconds * Domain.World.SimulationTime.TicksPerSecond) - 1);
        Assert.NotEqual(own.Id, theirs.Id);
    }

    [Fact]
    public void AGod_TakesNoMoreActsThanTheRulesAllow()
    {
        var (session, rival, _) = AgentWorlds.Setup();

        var results = GodTools.Do(session, rival, [.. Enumerable.Repeat(new GodAct("summon_dragons", string.Empty, string.Empty), 9)], AgentWorlds.Rules);

        Assert.Equal(AgentWorlds.Rules.MaxActions, results.Count);
    }
}
