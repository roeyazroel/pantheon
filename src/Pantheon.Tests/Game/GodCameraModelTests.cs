using System;
using System.Numerics;
using Pantheon.Game.Camera;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>God camera math: zoom limits, bands, view-relative panning, bounds and ground clearance.</summary>
public sealed class GodCameraModelTests
{
    private static readonly GodCameraSettings Settings = GodCameraSettings.Default;
    private static readonly Func<float, float, float> FlatGround = (_, _) => 0f;

    [Fact]
    public void GodCameraModel_ZoomIsClampedToLimits()
    {
        var camera = new GodCameraModel(Settings);

        camera.Zoom(1000f);
        Assert.Equal(Settings.MinDistance, camera.TargetDistance);

        camera.Zoom(-1000f);
        Assert.Equal(Settings.MaxDistance, camera.TargetDistance);
    }

    [Theory]
    [InlineData(25f, CameraZoomBand.Close)]
    [InlineData(120f, CameraZoomBand.Close)]
    [InlineData(121f, CameraZoomBand.Regional)]
    [InlineData(700f, CameraZoomBand.Regional)]
    [InlineData(701f, CameraZoomBand.Strategic)]
    [InlineData(2600f, CameraZoomBand.Strategic)]
    public void GodCameraModel_ClassifiesZoomBands(float distance, CameraZoomBand expected)
    {
        Assert.Equal(expected, GodCameraModel.ClassifyBand(Settings, distance));
    }

    [Fact]
    public void GodCameraModel_BandFollowsEasedDistance()
    {
        var camera = new GodCameraModel(Settings);
        Assert.Equal(CameraZoomBand.Regional, camera.Band);

        camera.FocusOn(Vector2.Zero, 2000f);
        camera.SnapToTarget();
        Assert.Equal(CameraZoomBand.Strategic, camera.Band);

        camera.FocusOn(Vector2.Zero, 60f);
        camera.SnapToTarget();
        Assert.Equal(CameraZoomBand.Close, camera.Band);
    }

    [Fact]
    public void GodCameraModel_PanIsRelativeToYaw()
    {
        var camera = new GodCameraModel(Settings);
        camera.Pan(new Vector2(0f, 1f), 1f);
        Assert.True(camera.TargetFocus.Y < 0f, "At yaw 0, forward is -Z.");
        Assert.Equal(0f, camera.TargetFocus.X, 3);

        var rotated = new GodCameraModel(Settings);
        rotated.Rotate(MathF.PI / 2f);
        rotated.SnapToTarget();
        rotated.Pan(new Vector2(0f, 1f), 1f);
        Assert.True(rotated.TargetFocus.X < 0f, "At yaw 90 degrees, forward is -X.");
        Assert.Equal(0f, rotated.TargetFocus.Y, 3);
    }

    [Fact]
    public void GodCameraModel_PanSpeedScalesWithDistance()
    {
        var near = new GodCameraModel(Settings);
        near.FocusOn(Vector2.Zero, 50f);
        near.Pan(new Vector2(1f, 0f), 1f);

        var far = new GodCameraModel(Settings);
        far.FocusOn(Vector2.Zero, 1000f);
        far.Pan(new Vector2(1f, 0f), 1f);

        Assert.Equal(20f, far.TargetFocus.X / near.TargetFocus.X, 3);
    }

    [Fact]
    public void GodCameraModel_FocusStaysInsideBounds()
    {
        var camera = new GodCameraModel(Settings);
        camera.SetBounds(Vector2.Zero, new Vector2(100f, 100f));

        camera.Pan(new Vector2(-1f, 0f), 10f);
        camera.PanWorld(new Vector2(0f, 500f));
        camera.FocusOn(new Vector2(-50f, 300f));

        Assert.Equal(new Vector2(0f, 100f), camera.TargetFocus);
        Assert.InRange(camera.Focus.Y, 0f, 100f);
    }

    [Fact]
    public void GodCameraModel_ZoomAtMovesFocusTowardsAnchor()
    {
        var camera = new GodCameraModel(Settings);
        var anchor = new Vector2(100f, 0f);

        camera.ZoomAt(1f, anchor);

        var fraction = 1f - Settings.ZoomStepFactor;
        Assert.Equal(anchor.X * fraction, camera.TargetFocus.X, 3);
    }

    [Fact]
    public void GodCameraModel_UpdateConvergesToTargets()
    {
        var camera = new GodCameraModel(Settings);
        camera.FocusOn(new Vector2(300f, -200f), 900f);
        camera.Rotate(1f);

        for (var i = 0; i < 300; i++)
        {
            camera.Update(1f / 60f, FlatGround);
        }

        Assert.Equal(300f, camera.Focus.X, 1);
        Assert.Equal(-200f, camera.Focus.Y, 1);
        Assert.Equal(900f, camera.Distance, 1);
        Assert.Equal(1f, camera.Yaw, 3);
    }

    [Fact]
    public void GodCameraModel_PitchSteepensWhenZoomingOut()
    {
        var camera = new GodCameraModel(Settings);
        camera.FocusOn(Vector2.Zero, Settings.MinDistance);
        camera.SnapToTarget();
        var closePitch = camera.Pitch;

        camera.FocusOn(Vector2.Zero, Settings.MaxDistance);
        camera.SnapToTarget();

        Assert.Equal(Settings.MinPitchDegrees * MathF.PI / 180f, closePitch, 4);
        Assert.Equal(Settings.MaxPitchDegrees * MathF.PI / 180f, camera.Pitch, 4);
    }

    [Fact]
    public void GodCameraModel_PoseOrbitsFocusAtDistance()
    {
        var camera = new GodCameraModel(Settings);
        camera.FocusOn(new Vector2(500f, 500f), 400f);
        camera.SnapToTarget();
        camera.Update(0f, FlatGround);

        var pose = camera.ComputePose(FlatGround);

        Assert.Equal(new Vector3(500f, 0f, 500f), pose.Target);
        Assert.Equal(400f, Vector3.Distance(pose.Position, pose.Target), 2);
        Assert.True(pose.Position.Z > pose.Target.Z, "At yaw 0 the camera sits on the +Z side.");
    }

    [Fact]
    public void GodCameraModel_PoseKeepsClearanceAboveGround()
    {
        var camera = new GodCameraModel(Settings);
        camera.FocusOn(Vector2.Zero, Settings.MinDistance);
        camera.SnapToTarget();
        camera.Update(0f, FlatGround);

        var pose = camera.ComputePose((_, _) => 500f);

        Assert.Equal(500f + Settings.MinClearance, pose.Position.Y);
    }

    [Fact]
    public void GodCameraSettings_RejectsInconsistentBands()
    {
        var settings = Settings with { CloseBandMaxDistance = 800f };

        Assert.Throws<ArgumentException>(() => new GodCameraModel(settings));
    }
}
