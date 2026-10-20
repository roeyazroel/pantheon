using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Events;
using Pantheon.Game.Flow;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The tutorial (ADR-064): each step ends when the player does what it asks, or on Next.</summary>
public sealed class TutorialTests
{
    private static readonly TutorialView Still = new(Vector2.Zero, 380f, 0f, false, false, 1.0, false);

    [Fact]
    public void TheTutorial_RunsThroughEveryStep_ByDoingWhatEachAsks()
    {
        var tutorial = new Tutorial();
        Assert.False(tutorial.IsActive);
        tutorial.Start();

        Assert.Equal(TutorialStep.Welcome, tutorial.Step);
        tutorial.Next();
        Do(tutorial, Still, Still with { Focus = new Vector2(70f, 0f) });
        Assert.Equal(TutorialStep.Zoom, tutorial.Step);
        Assert.True(tutorial.MarksHome);
        Do(tutorial, Still, Still with { Distance = 120f });
        Do(tutorial, Still, Still with { Yaw = 0.8f });
        Assert.Equal((TutorialStep.Plaque, TutorialTarget.Plaque), (tutorial.Step, tutorial.Target));
        tutorial.Next();
        Assert.Equal(TutorialTarget.FirstMiracle, tutorial.Target);
        tutorial.Observe([new MiracleCast { Tick = 1, GodId = 2, MiracleId = "food", Target = default, Cost = 1f, AffectedSettlementIds = [], Deaths = 0 }], localGodId: 1, creatureId: 1);
        Assert.Equal(TutorialStep.Cast, tutorial.Step);
        tutorial.Observe([new MiracleCast { Tick = 1, GodId = 1, MiracleId = "food", Target = default, Cost = 1f, AffectedSettlementIds = [], Deaths = 0 }], localGodId: 1, creatureId: 1);
        tutorial.Next();
        Do(tutorial, Still, Still with { Following = true });
        tutorial.Observe([new CreatureChanged { Tick = 2, CreatureId = 1, Change = CreatureChange.Rewarded }], localGodId: 1, creatureId: 1);
        Assert.Equal(TutorialStep.Work, tutorial.Step);
        tutorial.See(Still);
        tutorial.Notice(TutorialSignal.WorkOrdered);
        tutorial.See(Still);
        Do(tutorial, Still, Still with { Holding = true });
        Do(tutorial, Still, Still with { TimeScale = 4.0 });
        Do(tutorial, Still, Still with { DiplomacyOpen = true });
        Assert.Equal(TutorialStep.Fight, tutorial.Step);
        tutorial.Next();
        tutorial.Next();

        Assert.False(tutorial.IsActive);
        Assert.Equal(15, Tutorial.StepCount);
    }

    [Fact]
    public void WhatThePlayerDidBeforeAStep_DoesNotCount_AndStepsMayBeSkipped()
    {
        var tutorial = new Tutorial();
        tutorial.Start();
        tutorial.Next();

        // The first view a step sees is where it starts from: already being far away is no move.
        tutorial.See(Still with { Focus = new Vector2(500f, 500f) });
        tutorial.See(Still with { Focus = new Vector2(510f, 500f) });
        Assert.Equal(TutorialStep.Move, tutorial.Step);

        tutorial.Next();
        Assert.Equal(TutorialStep.Zoom, tutorial.Step);
        tutorial.Notice(TutorialSignal.WorkOrdered);
        foreach (var _ in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            tutorial.Next();
        }

        Assert.Equal(TutorialStep.Work, tutorial.Step);
        tutorial.See(Still);
        tutorial.See(Still);
        Assert.Equal(TutorialStep.Work, tutorial.Step);
    }

    [Fact]
    public void EveryStep_HasATitleAndWords_NamingThePlayersGodCreatureAndVillage()
    {
        var tutorial = new Tutorial();
        tutorial.Start();
        var mentioned = new HashSet<string>(StringComparer.Ordinal);
        while (tutorial.IsActive)
        {
            Assert.NotEmpty(tutorial.Title());
            var text = tutorial.Text("Ilyra", "Bramble", "Duskwater");
            Assert.True(text.Length > 40, tutorial.Step.ToString());
            foreach (var name in new[] { "Ilyra", "Bramble", "Duskwater" })
            {
                if (text.Contains(name, StringComparison.Ordinal))
                {
                    mentioned.Add(name);
                }
            }

            tutorial.Next();
        }

        Assert.Equal(3, mentioned.Count);
        Assert.Empty(tutorial.Text("Ilyra", "Bramble", "Duskwater"));
    }

    /// <summary>Shows the step its starting view, then the view that does what it asks.</summary>
    private static void Do(Tutorial tutorial, TutorialView start, TutorialView done)
    {
        tutorial.See(start);
        tutorial.See(done);
    }
}
