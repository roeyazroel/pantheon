using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Belief;

/// <summary>Schisms (spec §34, ADR-021): doubting followers break away to keep the Old Faith, and return or fade.</summary>
public sealed class SchismTests
{
    private static DivineRules Rules => TestContent.Shipped.DivineRules;

    private static SchismRules Schism => Rules.Schism;

    [Fact]
    public void FollowersWhoDeeplyDoubtTheirGod_BreakAwayToKeepItsOldFaith()
    {
        var (world, belief) = Create(doubt: 0.9f);
        var events = new List<WorldEvent>();

        Step(world, events);

        var village = world.Settlements[0];
        var old = Assert.IsType<OldFaith>(village.OldFaith);
        var schism = Assert.Single(events.OfType<SchismStarted>());
        Assert.Equal(1, old.GodId);
        Assert.InRange(old.Share, 0.25f, 0.35f);
        Assert.InRange(belief.Adherence + old.Share, 0.7f, 0.9f);
        Assert.True(belief.Doubt < 0.9f * (1f - Schism.DoubtRelief) + 0.05f);
        Assert.Equal((village.Id, 1, (int)MathF.Round(old.Share * village.Population)), (schism.SettlementId, schism.GodId, schism.People));
    }

    [Fact]
    public void NoSchism_BelowTheDoubt_InASmallVillage_OrUnderASanctuary()
    {
        var (calm, _) = Create(doubt: 0.3f);
        var (small, _) = Create(doubt: 0.9f);
        small.Settlements[0].Population = Schism.MinimumPopulation - 1;
        var (sheltered, _) = Create(doubt: 0.9f);
        (sheltered.Settlements[0].SanctuaryGodId, sheltered.Settlements[0].SanctuaryUntilTick) = (1, 1000);
        var events = new List<WorldEvent>();

        foreach (var world in new[] { calm, small, sheltered })
        {
            Step(world, events);
            Assert.Null(world.Settlements[0].OldFaith);
        }

        Assert.Empty(events.OfType<SchismStarted>());
    }

    [Fact]
    public void TheOldFaith_FollowsNoOtherGod()
    {
        var rival = new GodBelief { GodId = 2, Adherence = 0.1f, Faith = 1f, Love = 1f, Fear = 0f, Doubt = 0f };
        var beliefs = new List<GodBelief> { new() { GodId = 1, Adherence = 0.4f, Faith = 0.5f, Love = 0f, Fear = 0f, Doubt = 0f }, rival };

        var open = BeliefModel.ComputeAdherenceChanges(Rules, beliefs);
        var closed = BeliefModel.ComputeAdherenceChanges(Rules, beliefs, reserved: 0.5f);

        Assert.True(open[1] > 0f);
        Assert.Equal(0f, closed[1]);
    }

    [Fact]
    public void TheOldFaith_ReturnsWhenTheGodIsLovedAgain_AndTheWorldRemembersTheReunion()
    {
        var (world, belief) = Create(doubt: 0.9f);
        var events = new List<WorldEvent>();
        Step(world, events);
        var old = world.Settlements[0].OldFaith!;
        var share = old.Share;

        for (var i = 0; i < 1000 && world.Settlements[0].OldFaith!.Share > 0f; i++)
        {
            (belief.Love, belief.Doubt) = (0.9f, 0.1f);
            Step(world, events);
        }

        var ended = Assert.Single(events.OfType<SchismEnded>());
        Assert.True(ended.Reconciled);
        Assert.Equal(0f, old.Share);
        Assert.Equal(share, old.Returned + old.Faded, 3);
        var feed = new AlertFeed().Push(events, world, 1, 1.0);
        Assert.Contains(feed, a => a.Text.StartsWith("Schism in Town1: ", StringComparison.Ordinal));
        Assert.Contains(feed, a => a.Text == "The Old Faith of Town1 returns to you.");
    }

    [Fact]
    public void TheOldFaith_FadesIntoUnbelief_Otherwise()
    {
        var (world, belief) = Create(doubt: 0.9f);
        var events = new List<WorldEvent>();
        Step(world, events);
        var old = world.Settlements[0].OldFaith!;
        var share = old.Share;

        while (old.Share > 0f)
        {
            belief.Love = 0f;
            Step(world, events);
        }

        var ended = Assert.Single(events.OfType<SchismEnded>());
        Assert.False(ended.Reconciled);
        Assert.Equal(share, old.Faded, 3);
        Assert.Single(events.OfType<SchismStarted>());
        Assert.Contains(new AlertFeed().Push(events, world, 1, 1.0), a => a.Text == "The Old Faith of Town1 has faded into unbelief.");
    }

    [Fact]
    public void AVillage_SplitsAgainOnlyAWhileAfterItsLastSchism()
    {
        var (world, belief) = Create(doubt: 0.9f);
        var events = new List<WorldEvent>();
        Step(world, events);
        var since = world.Settlements[0].OldFaith!.SinceTick;
        while (world.Settlements[0].OldFaith!.Share > 0f)
        {
            (belief.Love, belief.Doubt) = (0.9f, 0.1f);
            Step(world, events);
        }

        belief.Doubt = 0.95f;
        Step(world, events);
        var soon = events.OfType<SchismStarted>().Count();
        world.Tick = since + (long)(Schism.CooldownSeconds * SimulationTime.TicksPerSecond);
        belief.Doubt = 0.95f;
        Step(world, events);

        Assert.Equal(1, soon);
        Assert.Equal(2, events.OfType<SchismStarted>().Count());
    }

    [Fact]
    public void AGodThatHarmsPeopleWhoLoveIt_PlantsMoreDoubt_ThanOneTheyNeverLoved()
    {
        var doubts = new List<float>();
        foreach (var love in new[] { 0.9f, 0f })
        {
            var world = DivineWorlds.Create(1000f, 300f);
            var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f, love: love);
            var caster = new MiracleCaster(Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);
            caster.Cast(world, new CastMiracleRequest(1, "lightning", world.Settlements[0].Position), 1, []);
            doubts.Add(belief.Doubt);
        }

        Assert.True(doubts[0] > doubts[1] + 0.2f, $"{doubts[0]} against {doubts[1]}");
    }

    [Fact]
    public void ASchism_IsHistory()
    {
        var (world, _) = Create(doubt: 0.9f);
        var events = new List<WorldEvent>();
        Step(world, events);
        var recorder = new Pantheon.Simulation.History.HistoryRecorder(TestContent.Shipped.History);

        recorder.Record(world, events, 0);

        var record = world.History.Single(r => r.Kind == HistoryKind.Schism);
        Assert.StartsWith("Year 0 — The Schism of Town1 (", Pantheon.Game.History.HistoryText.Line(record, world, TestContent.Shipped.History), StringComparison.Ordinal);
        Assert.EndsWith("keep the Old Faith of Testa)", Pantheon.Game.History.HistoryText.Line(record, world, TestContent.Shipped.History), StringComparison.Ordinal);
    }

    /// <summary>A village of 100 people that mostly follows god 1 with the given doubt.</summary>
    private static (WorldState World, GodBelief Belief) Create(float doubt)
    {
        var world = DivineWorlds.Create(100f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f, love: 0.1f, doubt: doubt);
        return (world, belief);
    }

    /// <summary>One belief step with the shipped rules, which have schisms.</summary>
    private static void Step(WorldState world, List<WorldEvent> events)
    {
        new BeliefSystem(Rules, SettlementWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), events));
        world.Tick += Rules.StepIntervalTicks;
    }
}
