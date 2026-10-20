using System;
using System.Linq;
using System.Numerics;
using Pantheon.Game.Hand;
using Pantheon.Game.Presentation;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The hand's held things that swing, and the camera kept above the roofs (spec §55, ADR-057).</summary>
public sealed class TactileTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void AHeldThing_HangsStill_WhileTheHandRestsOrGlidesEvenly()
    {
        var swing = new HeldSwing();
        for (var i = 0; i < 120; i++)
        {
            swing.Step(new Vector2(3f, 4f), Frame);
        }

        Assert.Equal(Vector2.Zero, swing.Tilt);

        var gliding = new HeldSwing();
        for (var i = 0; i < 600; i++)
        {
            gliding.Step(new Vector2(i * 0.05f, 0f), Frame);
        }

        Assert.True(gliding.Tilt.Length() < 0.01f, "an even glide leaves it hanging straight down");
    }

    [Fact]
    public void AQuickMove_LeavesItLaggingBehind_ThenItSwingsPastAndSettles()
    {
        var swing = new HeldSwing();
        var hand = Vector2.Zero;
        swing.Step(hand, Frame);
        var tilts = new System.Collections.Generic.List<float>();
        for (var i = 0; i < 300; i++)
        {
            // Half a second's sweep towards +X, then the hand stops.
            hand += new Vector2(i < 30 ? 0.2f : 0f, 0f);
            swing.Step(hand, Frame);
            tilts.Add(swing.Tilt.X);
        }

        Assert.True(tilts.Take(10).Min() < -0.05f, "it lags behind as the hand sets off");
        Assert.True(tilts.Skip(30).Max() > 0.05f, "it swings past as the hand stops");
        Assert.True(MathF.Abs(tilts[^1]) < 0.02f, "and settles");
        Assert.All(tilts, t => Assert.InRange(t, -HeldSwing.MostTilt, HeldSwing.MostTilt));
    }

    [Fact]
    public void AJumpOfTheCursor_IsNotAThrow_TheSwingStaysInBounds()
    {
        var swing = new HeldSwing();
        swing.Step(Vector2.Zero, Frame);
        swing.Step(new Vector2(500f, -300f), Frame);
        swing.Step(new Vector2(-800f, 900f), Frame);

        Assert.True(swing.Tilt.Length() <= HeldSwing.MostTilt + 1e-4f);
        swing.Reset();
        Assert.Equal(Vector2.Zero, swing.Tilt);
    }

    [Fact]
    public void TheCamera_KeepsAboveTheHighestRoofBesideIt_AndNoRoofFarAway()
    {
        var low = new HutPlacement(new Vector3(100f, 5f, 100f), 0f, 1f);
        var high = new HutPlacement(new Vector3(104f, 9f, 100f), 0f, 1.3f);
        var site = new SettlementSite(new Vector3(100f, 5f, 90f), [low, high], null, [], 30f, []);

        Assert.Equal(9f + (HouseMesh.Top * 1.3f), Rooftops.Highest(site, 102f, 100f, 3f)!.Value, 3);
        Assert.Equal(5f + HouseMesh.Top, Rooftops.Highest(site, 96f, 100f, 3f)!.Value, 3);
        Assert.Null(Rooftops.Highest(site, 100f, 120f, 3f));
        Assert.Null(Rooftops.Highest(site, 600f, 600f, 3f));
    }
}
