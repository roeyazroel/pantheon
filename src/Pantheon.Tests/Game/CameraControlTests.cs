using System;
using System.Numerics;
using Pantheon.Game.Camera;
using Pantheon.Game.Settings;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The camera's controls (ADR-058): tilt, trackpad pan and pinch, the fling and aiming above a followed thing.</summary>
public sealed class CameraControlTests
{
    private static readonly GodCameraSettings Settings = GodCameraSettings.Default;
    private static readonly Func<float, float, float> FlatGround = (_, _) => 0f;

    [Fact]
    public void Tilting_ChangesThePitch_WithinItsLimits()
    {
        var camera = Snapped(100f);
        var level = camera.Pitch;

        camera.TiltBy(0.2f);
        camera.SnapToTarget();
        Assert.Equal(level + 0.2f, camera.Pitch, 3);

        camera.TiltBy(-10f);
        camera.SnapToTarget();
        Assert.Equal(-Settings.MaxTiltDegrees * MathF.PI / 180f, camera.TargetTilt, 3);
        Assert.True(camera.Pitch >= Settings.LowestPitchDegrees * MathF.PI / 180f - 1e-4f, "never flatter than the lowest pitch");

        camera.FocusOn(Vector2.Zero, 2600f);
        camera.TiltBy(20f);
        camera.SnapToTarget();
        Assert.True(camera.Pitch <= Settings.SteepestPitchDegrees * MathF.PI / 180f + 1e-4f, "never steeper than the steepest pitch");
    }

    [Fact]
    public void ATrackpadPan_MovesAShareOfTheView_FartherWhenZoomedOut()
    {
        var near = Snapped(50f);
        var far = Snapped(500f);
        var fov = 50f * MathF.PI / 180f;

        near.PanViews(new Vector2(0f, 1f), fov);
        far.PanViews(new Vector2(0f, 1f), fov);

        Assert.True(near.Focus.Y < 0f, "forward at yaw 0 is -Z, and the pan is at once");
        Assert.Equal(2f * 50f * MathF.Tan(fov / 2f), -near.Focus.Y, 2);
        Assert.Equal(10f, far.Focus.Y / near.Focus.Y, 2);
    }

    [Fact]
    public void APinch_ZoomsByItsFactor_AboutTheFingers()
    {
        var camera = Snapped(200f);

        camera.ZoomByFactor(2f);
        Assert.Equal(100f, camera.TargetDistance, 2);

        camera.ZoomByFactor(0.5f, new Vector2(40f, 0f));
        Assert.Equal(200f, camera.TargetDistance, 2);
        Assert.True(camera.TargetFocus.X < 0f, "pulling back about a point to the right moves the focus away from it");

        camera.ZoomByFactor(float.NaN);
        camera.ZoomByFactor(0f);
        Assert.Equal(200f, camera.TargetDistance, 2);
    }

    [Fact]
    public void AFlungView_GlidesOn_SlowsAndStops_AndAFocusStopsIt()
    {
        var camera = Snapped(100f);
        camera.Fling(new Vector2(50f, 0f));

        camera.Update(0.1f, FlatGround);
        var early = camera.TargetFocus.X;
        Assert.True(early > 0f);
        for (var i = 0; i < 100; i++)
        {
            camera.Update(0.1f, FlatGround);
        }

        Assert.Equal(Vector2.Zero, camera.FlingVelocity);
        // Stepped in tenths of a second, the glide covers v·dt / (1 - e^(-k·dt)) in all.
        Assert.InRange(camera.TargetFocus.X, early, (50f * 0.1f / (1f - MathF.Exp(-Settings.FlingDamping * 0.1f))) + 0.5f);

        camera.Fling(new Vector2(0f, 1e6f));
        Assert.Equal(Settings.MaxFlingSpeedPerDistance * 100f, camera.FlingVelocity.Length(), 1);
        camera.FocusOn(Vector2.Zero);
        Assert.Equal(Vector2.Zero, camera.FlingVelocity);
    }

    [Fact]
    public void TheCamera_AimsAboveAFollowedThing_AndBackDownAfter()
    {
        var camera = Snapped(60f);
        camera.AimAbove = 6f;
        for (var i = 0; i < 60; i++)
        {
            camera.Update(0.05f, FlatGround);
        }

        Assert.Equal(6f, camera.ComputePose(FlatGround).Target.Y, 1);

        camera.AimAbove = 0f;
        for (var i = 0; i < 60; i++)
        {
            camera.Update(0.05f, FlatGround);
        }

        Assert.Equal(0f, camera.ComputePose(FlatGround).Target.Y, 1);
    }

    [Fact]
    public void TheControls_AreSettings_KeptInRange()
    {
        var settings = GameSettings.Parse("""{ "EdgeScroll": true, "TrackpadScrollZooms": true, "CameraSpeed": 9 }""");
        Assert.True(settings.EdgeScroll);
        Assert.True(settings.TrackpadScrollZooms);
        Assert.Equal(GameSettings.MaxCameraSpeed, settings.CameraSpeed);
        Assert.False(new GameSettings().EdgeScroll, "the window's edge does not pan unless asked");
        Assert.Throws<ArgumentException>(() => new GodCameraModel(Settings with { LowestPitchDegrees = 80f, SteepestPitchDegrees = 70f }));
    }

    [Fact]
    public void AHillBetween_LiftsTheCamera_UntilItSeesTheFocusOverIt()
    {
        var camera = Snapped(100f);
        var open = camera.ComputePose(FlatGround);

        // A ridge halfway between the camera and the focus, higher than the line of sight.
        Func<float, float, float> ridge = (_, z) => MathF.Abs(z - (open.Position.Z / 2f)) < 3f ? open.Position.Y : 0f;
        var lifted = camera.ComputePose(ridge);

        Assert.True(lifted.Position.Y > open.Position.Y, "the camera rises over the ridge");
        var mid = (lifted.Position + lifted.Target) / 2f;
        Assert.True(mid.Y >= open.Position.Y + Settings.MinClearance - 0.5f, "the line of sight clears the ridge");
    }

    /// <summary>A camera at the origin, already at a distance.</summary>
    private static GodCameraModel Snapped(float distance)
    {
        var camera = new GodCameraModel(Settings);
        camera.FocusOn(Vector2.Zero, distance);
        camera.SnapToTarget();
        return camera;
    }
}
