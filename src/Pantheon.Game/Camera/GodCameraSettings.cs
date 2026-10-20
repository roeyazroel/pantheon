using System;

namespace Pantheon.Game.Camera;

/// <summary>Tuning of the god camera. Distances are in metres and speeds are per second.</summary>
public sealed record GodCameraSettings
{
    /// <summary>Default tuning.</summary>
    public static GodCameraSettings Default { get; } = new();

    /// <summary>Closest camera distance to the focus point.</summary>
    public float MinDistance { get; init; } = 12f;

    /// <summary>Farthest camera distance to the focus point.</summary>
    public float MaxDistance { get; init; } = 2600f;

    /// <summary>Distance used when a world is first shown.</summary>
    public float InitialDistance { get; init; } = 380f;

    /// <summary>Distances up to this value belong to <see cref="CameraZoomBand.Close"/>.</summary>
    public float CloseBandMaxDistance { get; init; } = 120f;

    /// <summary>Distances up to this value (and above the close band) belong to <see cref="CameraZoomBand.Regional"/>.</summary>
    public float RegionalBandMaxDistance { get; init; } = 700f;

    /// <summary>Camera pitch below the horizon at <see cref="MinDistance"/>, in degrees.</summary>
    public float MinPitchDegrees { get; init; } = 28f;

    /// <summary>Camera pitch below the horizon at <see cref="MaxDistance"/>, in degrees.</summary>
    public float MaxPitchDegrees { get; init; } = 72f;

    /// <summary>Keyboard and edge-scroll pan speed, as metres per second per metre of camera distance.</summary>
    public float PanSpeedPerDistance { get; init; } = 0.9f;

    /// <summary>Keyboard rotation speed in radians per second.</summary>
    public float KeyRotateSpeed { get; init; } = 1.7f;

    /// <summary>Mouse-drag rotation in radians per pixel.</summary>
    public float DragRotateRadiansPerPixel { get; init; } = 0.006f;

    /// <summary>Distance multiplier per zoom-in step. Values below 1 zoom in.</summary>
    public float ZoomStepFactor { get; init; } = 0.85f;

    /// <summary>Exponential smoothing rate (1/s) for focus, yaw and distance.</summary>
    public float Smoothing { get; init; } = 10f;

    /// <summary>How far the player may tilt the view from the pitch its zoom gives, in degrees either way.</summary>
    public float MaxTiltDegrees { get; init; } = 30f;

    /// <summary>The shallowest pitch the view may have, tilt included, in degrees below the horizon.</summary>
    public float LowestPitchDegrees { get; init; } = 10f;

    /// <summary>The steepest pitch the view may have, tilt included, in degrees below the horizon.</summary>
    public float SteepestPitchDegrees { get; init; } = 85f;

    /// <summary>Mouse-drag tilt in radians per pixel.</summary>
    public float DragTiltRadiansPerPixel { get; init; } = 0.004f;

    /// <summary>How fast a flung view slows (1/s) after the land is let go mid-drag.</summary>
    public float FlingDamping { get; init; } = 4f;

    /// <summary>The fastest a flung view may glide, as metres per second per metre of camera distance.</summary>
    public float MaxFlingSpeedPerDistance { get; init; } = 3f;

    /// <summary>Minimum height of the camera above the ground.</summary>
    public float MinClearance { get; init; } = 4f;

    /// <summary>Width of the screen border, in pixels, that triggers edge scrolling.</summary>
    public float EdgeScrollMarginPixels { get; init; } = 12f;

    /// <summary>Throws when the settings are inconsistent.</summary>
    /// <exception cref="ArgumentException">A setting is out of range.</exception>
    public void Validate()
    {
        if (!(MinDistance > 0f && MinDistance < MaxDistance))
        {
            throw new ArgumentException("MinDistance must be positive and below MaxDistance.");
        }

        if (!(CloseBandMaxDistance > MinDistance && CloseBandMaxDistance < RegionalBandMaxDistance && RegionalBandMaxDistance < MaxDistance))
        {
            throw new ArgumentException("Zoom bands must satisfy MinDistance < CloseBandMaxDistance < RegionalBandMaxDistance < MaxDistance.");
        }

        if (!(MinPitchDegrees > 0f && MinPitchDegrees <= MaxPitchDegrees && MaxPitchDegrees < 90f))
        {
            throw new ArgumentException("Pitch must satisfy 0 < MinPitchDegrees <= MaxPitchDegrees < 90.");
        }

        if (!(MaxTiltDegrees >= 0f && LowestPitchDegrees > 0f && LowestPitchDegrees < SteepestPitchDegrees && SteepestPitchDegrees < 90f))
        {
            throw new ArgumentException("Tilt must be non-negative and 0 < LowestPitchDegrees < SteepestPitchDegrees < 90.");
        }

        if (!(FlingDamping > 0f && MaxFlingSpeedPerDistance >= 0f))
        {
            throw new ArgumentException("FlingDamping must be positive and MaxFlingSpeedPerDistance non-negative.");
        }

        if (!(ZoomStepFactor > 0f && ZoomStepFactor < 1f) || !(Smoothing > 0f) || !(PanSpeedPerDistance > 0f))
        {
            throw new ArgumentException("ZoomStepFactor must lie in (0, 1); Smoothing and PanSpeedPerDistance must be positive.");
        }
    }
}
