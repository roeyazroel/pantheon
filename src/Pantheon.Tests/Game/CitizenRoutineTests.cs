using System;
using System.Numerics;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>A representative's routine is a closed-form, continuous loop through its home, work and gathering spots.</summary>
public sealed class CitizenRoutineTests
{
    private const float Tolerance = 0.001f;
    private const double Cycle = 95.0;

    // Pinned values, so the timings below keep their meaning when settlement_view.json is retuned.
    private static readonly CitizenSettings Settings = TestContent.SettlementView.Citizens with
    {
        WalkSpeed = 2f,
        CatchUpSpeed = 4f,
        RestSeconds = 10f,
        WorkSeconds = 20f,
        GatherSeconds = 5f,
        StrideLength = 0.75f,
        StepBounce = 0.07f,
        WorkLean = 0.45f,
        WorkStrokesPerSecond = 0.8f,
    };

    // Home to work is 50 m, work to the gathering spot 40 m and back home 30 m: 60 s of walking,
    // so the farmer rests until 10 s, walks until 35 s, works until 55 s, walks until 75 s, gathers
    // until 80 s and is home again at 95 s.
    private static readonly RepresentativeCitizen Farmer = new(
        Index: 0,
        Role: CitizenRole.Farmer,
        House: 0,
        Home: new Vector2(0f, 0f),
        Work: new Vector2(30f, 40f),
        WorkYaw: 0.5f,
        Gathering: new Vector2(30f, 0f),
        GatherYaw: 1f,
        Phase: 0f);

    [Fact]
    public void CycleSeconds_AddsTheWalksToTheStays()
    {
        Assert.Equal((float)Cycle, CitizenRoutine.CycleSeconds(Farmer, Settings), 4);
    }

    [Theory]
    [InlineData(5.0, 0f, 0f, CitizenActivity.AtHome)]
    [InlineData(22.5, 15f, 20f, CitizenActivity.Walking)]
    [InlineData(45.0, 30f, 40f, CitizenActivity.Working)]
    [InlineData(65.0, 30f, 20f, CitizenActivity.Walking)]
    [InlineData(77.5, 30f, 0f, CitizenActivity.Gathering)]
    [InlineData(87.5, 15f, 0f, CitizenActivity.Walking)]
    [InlineData(100.0, 0f, 0f, CitizenActivity.AtHome)]
    public void Evaluate_RestsWalksWorksWalksGathersAndWalksHome(double seconds, float x, float z, CitizenActivity activity)
    {
        var pose = CitizenRoutine.Evaluate(Farmer, seconds, Settings);

        Assert.Equal(activity, pose.Activity);
        Assert.Equal(x, pose.Ground.X, 3);
        Assert.Equal(z, pose.Ground.Y, 3);
    }

    [Fact]
    public void Evaluate_RepeatsEveryCycle()
    {
        for (var seconds = 0.1; seconds < Cycle; seconds += 0.37)
        {
            var first = CitizenRoutine.Evaluate(Farmer, seconds, Settings);
            var later = CitizenRoutine.Evaluate(Farmer, seconds + (3 * Cycle), Settings);

            Assert.Equal(first.Activity, later.Activity);
            Assert.Equal(first.Ground.X, later.Ground.X, 3);
            Assert.Equal(first.Ground.Y, later.Ground.Y, 3);
        }
    }

    [Fact]
    public void Evaluate_NeverMovesFasterThanWalking_EvenWhereTheCycleWraps()
    {
        const double Step = 0.05;
        var previous = CitizenRoutine.Evaluate(Farmer, 0.0, Settings).Ground;
        for (var seconds = Step; seconds <= 2 * Cycle; seconds += Step)
        {
            var ground = CitizenRoutine.Evaluate(Farmer, seconds, Settings).Ground;

            Assert.InRange(Vector2.Distance(previous, ground), 0f, (Settings.WalkSpeed * (float)Step) + Tolerance);
            previous = ground;
        }
    }

    [Fact]
    public void Phase_ShiftsTheRoutine()
    {
        var later = Farmer with { Phase = 0.25f };

        for (var seconds = 0.0; seconds < Cycle; seconds += 0.5)
        {
            var shifted = CitizenRoutine.Evaluate(later, seconds, Settings);
            var expected = CitizenRoutine.Evaluate(Farmer, seconds + (0.25 * Cycle), Settings);

            Assert.Equal(expected.Activity, shifted.Activity);
            Assert.Equal(expected.Ground.X, shifted.Ground.X, 3);
            Assert.Equal(expected.Ground.Y, shifted.Ground.Y, 3);
        }
    }

    [Fact]
    public void LeansOnlyWhileWorking_AndBobsOnlyWhileWalking()
    {
        var leaned = false;
        var bobbed = false;
        for (var seconds = 0.0; seconds < Cycle; seconds += 0.1)
        {
            var pose = CitizenRoutine.Evaluate(Farmer, seconds, Settings);

            Assert.InRange(pose.Lean, 0f, Settings.WorkLean + Tolerance);
            Assert.InRange(pose.Bounce, 0f, Settings.StepBounce + Tolerance);
            if (pose.Activity != CitizenActivity.Working)
            {
                Assert.Equal(0f, pose.Lean);
            }

            if (pose.Activity != CitizenActivity.Walking)
            {
                Assert.Equal(0f, pose.Bounce);
            }

            leaned |= pose.Lean > 0f;
            bobbed |= pose.Bounce > 0f;
        }

        Assert.True(leaned, "The farmer never leaned into its work.");
        Assert.True(bobbed, "The farmer never bobbed while walking.");
    }

    [Theory]
    [InlineData(5.0, 1f, 0f)]
    [InlineData(22.5, 0.6f, 0.8f)]
    [InlineData(65.0, 0f, -1f)]
    [InlineData(87.5, -1f, 0f)]
    public void FacesTheShrineSpotFromHome_AndTheWayItWalks(double seconds, float x, float z)
    {
        var facing = SettlementSite.Facing(CitizenRoutine.Evaluate(Farmer, seconds, Settings).Yaw);

        Assert.Equal(x, facing.X, 3);
        Assert.Equal(z, facing.Y, 3);
    }

    [Fact]
    public void FacesItsWork_AndTheShrineWhileGathering()
    {
        Assert.Equal(Farmer.WorkYaw, CitizenRoutine.Evaluate(Farmer, 45.0, Settings).Yaw);
        Assert.Equal(Farmer.GatherYaw, CitizenRoutine.Evaluate(Farmer, 77.5, Settings).Yaw);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.1875f, 0.035f)]
    [InlineData(0.375f, 0.07f)]
    [InlineData(0.75f, 0f)]
    [InlineData(1.125f, 0.07f)]
    public void Bounce_RisesGentlyOverEachStep_NotInHops(float walked, float expected)
    {
        Assert.Equal(expected, CitizenRoutine.Bounce(walked, Settings), 4);
    }

    [Fact]
    public void Evaluate_WithEverySpotTogether_StaysPut()
    {
        var idle = Farmer with { Work = Farmer.Home, Gathering = Farmer.Home };

        Assert.Equal(35f, CitizenRoutine.CycleSeconds(idle, Settings), 4);
        for (var seconds = 0.0; seconds < 70.0; seconds += 0.5)
        {
            var pose = CitizenRoutine.Evaluate(idle, seconds, Settings);

            Assert.Equal(Farmer.Home, pose.Ground);
            Assert.False(float.IsNaN(pose.Yaw));
        }
    }
}
