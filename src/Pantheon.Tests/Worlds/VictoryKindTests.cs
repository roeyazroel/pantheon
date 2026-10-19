using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>Conquest, cultural victory and the sandbox (spec §53, ADR-027).</summary>
public sealed class VictoryKindTests
{
    private static readonly VictorySettings ConquestOnly = new() { Ascendancy = false, Conquest = true };
    private static readonly VictorySettings CultureOnly = new() { Ascendancy = false, Cultural = true };

    [Fact]
    public void Conquest_IsWon_WhenEveryRivalStaysBelowTheConquestShare()
    {
        var world = PantheonWorlds.Create(0.9f, 0.02f, 0.02f, 0.02f);
        world.Victory = ConquestOnly;
        var events = new List<WorldEvent>();

        Run(world, events);

        Assert.Equal(0L, world.Gods[0].ConquestSinceTick);
        Assert.Equal(0L, world.Gods[0].DominantSinceTick);
        Assert.Empty(world.WinnerGodIds);

        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        Run(world, events);

        Assert.Equal([1], world.WinnerGodIds);
        var victory = Assert.Single(events.OfType<VictoryAchieved>());
        Assert.Equal(VictoryKind.Conquest, victory.Kind);
    }

    [Fact]
    public void Conquest_IsSharedWithAllies_WhoDoNotCountAsRivals()
    {
        var world = PantheonWorlds.Create(0.9f, 0.5f, 0.02f, 0.02f);
        world.Victory = ConquestOnly;
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        var events = new List<WorldEvent>();

        Run(world, events);
        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        Run(world, events);

        Assert.Equal([1, 2], world.WinnerGodIds);
        Assert.Equal(VictoryKind.Conquest, Assert.Single(events.OfType<VictoryAchieved>()).Kind);
    }

    [Fact]
    public void Conquest_ClockStops_WhenARivalRecovers()
    {
        var world = PantheonWorlds.Create(0.9f, 0.02f, 0.02f, 0.02f);
        world.Victory = ConquestOnly;
        var system = new VictorySystem(DivineWorlds.Rules);

        Assert.True(system.Conquers(world, 1));
        Run(world, []);
        world.Settlements[1].Beliefs[0].Adherence = 0.3f;
        world.Tick = 10;
        Run(world, []);

        Assert.False(system.Conquers(world, 1));
        Assert.Null(world.Gods[0].ConquestSinceTick);
    }

    [Fact]
    public void Culture_IsWon_ByLeadingThreeVillagesInFour_WhileARivalLivesOn()
    {
        var world = PantheonWorlds.Create(0.6f, 0.4f);
        DivineWorlds.Believe(world.Settlements[1], 0.5f, faith: 0.8f, godId: 1);
        DivineWorlds.Believe(world.Settlements[2], 0.5f, faith: 0.8f, godId: 1);
        world.Victory = CultureOnly;
        var events = new List<WorldEvent>();

        Assert.Equal((4, 3), VictorySystem.CultureCount(world, 1));
        Assert.True(new VictorySystem(DivineWorlds.Rules).LeadsCulture(world, 1));
        Run(world, events);
        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        Run(world, events);

        Assert.Equal([1], world.WinnerGodIds);
        Assert.Equal(VictoryKind.Cultural, Assert.Single(events.OfType<VictoryAchieved>()).Kind);
    }

    [Fact]
    public void Culture_IsNotWon_WithoutARival_NorByLeadingTooFew()
    {
        var alone = PantheonWorlds.Create(0.6f, 0f, 0f, 0f);
        foreach (var settlement in alone.Settlements.Skip(1))
        {
            DivineWorlds.Believe(settlement, 0.5f, faith: 0.8f, godId: 1);
        }

        var split = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var system = new VictorySystem(DivineWorlds.Rules);

        Assert.False(system.LeadsCulture(alone, 1));
        Assert.True(system.Conquers(alone, 1));
        Assert.False(system.LeadsCulture(split, 1));
    }

    [Fact]
    public void CultureCount_SkipsTheDead_AndTheBarelyBelieving()
    {
        var world = PantheonWorlds.Create(0.6f, 0.04f, 0.6f);
        DivineWorlds.Believe(world.Settlements[1], 0.03f, faith: 0.8f, godId: 1);
        world.Settlements[2].Population = 0;

        Assert.Equal((3, 1), VictorySystem.CultureCount(world, 1));
    }

    [Fact]
    public void ASandbox_HasNoWinner_ThoughDominanceIsStillTracked()
    {
        var world = PantheonWorlds.Create(0.9f, 0.02f, 0.02f, 0.02f);
        world.Victory = VictorySettings.Sandbox;
        var events = new List<WorldEvent>();

        Run(world, events);
        world.Tick = 10 * VictorySystem.TicksToWin(DivineWorlds.Rules);
        Run(world, events);

        Assert.True(world.Victory.IsSandbox());
        Assert.Empty(world.WinnerGodIds);
        Assert.Empty(events.OfType<VictoryAchieved>());
        Assert.Equal(0L, world.Gods[0].DominantSinceTick);
        Assert.Null(world.Gods[0].ConquestSinceTick);
    }

    [Fact]
    public void Ascendancy_IsAKindOfItsOwn_AndAPantheonWhenShared()
    {
        var alone = PantheonWorlds.Create(0.9f, 0.1f);
        var shared = PantheonWorlds.Create(0.4f, 0.3f, 0.3f);
        PantheonWorlds.SetStatus(shared, 1, 2, DiplomaticStatus.Alliance);
        var aloneEvents = new List<WorldEvent>();
        var sharedEvents = new List<WorldEvent>();

        foreach (var (world, events) in new[] { (alone, aloneEvents), (shared, sharedEvents) })
        {
            Run(world, events);
            world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
            Run(world, events);
        }

        Assert.Equal(VictoryKind.Ascendancy, Assert.Single(aloneEvents.OfType<VictoryAchieved>()).Kind);
        Assert.Equal(VictoryKind.Pantheon, Assert.Single(sharedEvents.OfType<VictoryAchieved>()).Kind);
    }

    [Fact]
    public void AWorldsSize_SetsHowManyGodsMayWinTogether()
    {
        var world = PantheonWorlds.Create(0.3f, 0.2f, 0.2f, 0.3f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Alliance);
        PantheonWorlds.SetStatus(world, 1, 3, DiplomaticStatus.Alliance);

        Assert.Equal([2], VictorySystem.StandingOf(DivineWorlds.Rules, world, 1).Allies);

        world.Victory = VictorySettings.Default with { PantheonMaxGods = 3 };
        var (share, allies) = VictorySystem.StandingOf(DivineWorlds.Rules, world, 1);

        Assert.Equal([2, 3], allies);
        Assert.Equal(0.7f, share, 3);
    }

    [Fact]
    public void TheFirstVictory_IsTheOnlyOne()
    {
        var world = PantheonWorlds.Create(0.9f, 0.02f, 0.02f, 0.02f);
        world.Victory = VictorySettings.All;
        var events = new List<WorldEvent>();

        Run(world, events);
        world.Tick = VictorySystem.TicksToWin(DivineWorlds.Rules);
        Run(world, events);
        world.Tick *= 2;
        Run(world, events);

        Assert.Equal(VictoryKind.Ascendancy, Assert.Single(events.OfType<VictoryAchieved>()).Kind);
    }

    /// <summary>One victory step.</summary>
    private static void Run(WorldState world, List<WorldEvent> events) =>
        new VictorySystem(DivineWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), events));
}
