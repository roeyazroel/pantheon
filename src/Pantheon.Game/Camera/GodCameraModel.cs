using System;
using System.Numerics;

namespace Pantheon.Game.Camera;

/// <summary>
/// Engine-agnostic god camera: an orbit around a focus point on the ground, with yaw, distance
/// and a pitch that steepens as the camera zooms out. Input changes target values, and
/// <see cref="Update"/> eases the current values towards them.
/// </summary>
/// <remarks>
/// Ground coordinates are (X, Z) packed into a <see cref="Vector2"/> as (X, Y). At yaw 0 the camera
/// sits on the +Z side of the focus and looks towards -Z, which matches Godot's forward axis.
/// </remarks>
public sealed class GodCameraModel
{
    // The world extent the settings' maximum distance was chosen for, in metres.
    private const float ReferenceExtent = 2048f;

    private readonly GodCameraSettings _settings;
    private Vector2 _targetFocus;
    private float _targetYaw;
    private float _targetDistance;
    private Vector2 _boundsMin = new(float.NegativeInfinity);
    private Vector2 _boundsMax = new(float.PositiveInfinity);
    private (Vector2 Focus, float Distance)? _beforeOverview;
    private bool _hasFocusHeight;

    /// <summary>Creates a camera at the initial distance, looking at the origin.</summary>
    /// <param name="settings">Validated camera tuning.</param>
    public GodCameraModel(GodCameraSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
        MaxDistance = settings.MaxDistance;
        _targetDistance = Math.Clamp(settings.InitialDistance, settings.MinDistance, settings.MaxDistance);
        Distance = _targetDistance;
    }

    /// <summary>Camera tuning.</summary>
    public GodCameraSettings Settings => _settings;

    /// <summary>The farthest the camera may pull back: the settings' maximum, more for a world larger than 2 km.</summary>
    public float MaxDistance { get; private set; }

    /// <summary>Current (eased) focus point on the ground.</summary>
    public Vector2 Focus { get; private set; }

    /// <summary>Focus point the camera is easing towards.</summary>
    public Vector2 TargetFocus => _targetFocus;

    /// <summary>Current yaw in radians.</summary>
    public float Yaw { get; private set; }

    /// <summary>Yaw the camera is easing towards.</summary>
    public float TargetYaw => _targetYaw;

    /// <summary>Current distance from the focus point.</summary>
    public float Distance { get; private set; }

    /// <summary>Distance the camera is easing towards.</summary>
    public float TargetDistance => _targetDistance;

    /// <summary>Smoothed ground height under the focus point.</summary>
    public float FocusHeight { get; private set; }

    /// <summary>Current pitch below the horizon, in radians.</summary>
    public float Pitch => ComputePitch(Distance);

    /// <summary>Zoom band of the current distance.</summary>
    public CameraZoomBand Band => ClassifyBand(_settings, Distance);

    /// <summary>Maps a camera distance to its zoom band.</summary>
    /// <param name="settings">Camera tuning that defines the band limits.</param>
    /// <param name="distance">Camera distance in metres.</param>
    /// <returns>The zoom band.</returns>
    public static CameraZoomBand ClassifyBand(GodCameraSettings settings, float distance)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (distance <= settings.CloseBandMaxDistance)
        {
            return CameraZoomBand.Close;
        }

        return distance <= settings.RegionalBandMaxDistance ? CameraZoomBand.Regional : CameraZoomBand.Strategic;
    }

    /// <summary>Restricts the focus point to a rectangle on the ground.</summary>
    /// <param name="min">Minimum (X, Z) corner.</param>
    /// <param name="max">Maximum (X, Z) corner.</param>
    public void SetBounds(Vector2 min, Vector2 max)
    {
        if (min.X > max.X || min.Y > max.Y)
        {
            throw new ArgumentException("Bounds minimum must not exceed the maximum.", nameof(min));
        }

        _boundsMin = min;
        _boundsMax = max;

        // A larger world may be seen whole from farther away (ADR-027): the default reach is for a 2 km island.
        MaxDistance = Math.Max(_settings.MaxDistance, Math.Max(max.X - min.X, max.Y - min.Y) * _settings.MaxDistance / ReferenceExtent);
        _targetDistance = Math.Clamp(_targetDistance, _settings.MinDistance, MaxDistance);
        _targetFocus = ClampToBounds(_targetFocus);
        Focus = ClampToBounds(Focus);
    }

    /// <summary>Pans relative to the view direction, faster when zoomed out.</summary>
    /// <param name="axes">X = right, Y = forward. Longer than 1 is normalised.</param>
    /// <param name="deltaSeconds">Frame time.</param>
    public void Pan(Vector2 axes, float deltaSeconds)
    {
        if (axes.LengthSquared() > 1f)
        {
            axes = Vector2.Normalize(axes);
        }

        var speed = _settings.PanSpeedPerDistance * _targetDistance;
        _targetFocus = ClampToBounds(_targetFocus + (ViewToGround(axes) * (speed * deltaSeconds)));
    }

    /// <summary>
    /// Moves the focus by a ground offset immediately, without easing. Grab-panning uses this so
    /// the grabbed point stays under the cursor.
    /// </summary>
    /// <param name="offset">World (X, Z) offset in metres.</param>
    public void PanWorld(Vector2 offset)
    {
        _targetFocus = ClampToBounds(_targetFocus + offset);
        Focus = ClampToBounds(Focus + offset);
    }

    /// <summary>Rotates the view around the focus point.</summary>
    /// <param name="radians">Yaw change; positive turns the camera counter-clockwise seen from above.</param>
    public void Rotate(float radians) => _targetYaw += radians;

    /// <summary>Zooms by a number of steps; positive steps zoom in.</summary>
    /// <param name="steps">Zoom steps (one mouse-wheel notch is one step).</param>
    public void Zoom(float steps) =>
        _targetDistance = Math.Clamp(_targetDistance * MathF.Pow(_settings.ZoomStepFactor, steps), _settings.MinDistance, MaxDistance);

    /// <summary>Zooms towards (or away from) a ground point, keeping it roughly under the cursor.</summary>
    /// <param name="steps">Zoom steps; positive zooms in.</param>
    /// <param name="groundPoint">World (X, Z) point under the cursor.</param>
    public void ZoomAt(float steps, Vector2 groundPoint)
    {
        var before = _targetDistance;
        Zoom(steps);

        // The focus moves by the same fraction the distance shrinks, as in a zoom about a point.
        var fraction = 1f - (_targetDistance / before);
        _targetFocus = ClampToBounds(_targetFocus + ((groundPoint - _targetFocus) * fraction));
    }

    /// <summary>Glides to a new focus point, optionally changing the distance.</summary>
    /// <param name="point">World (X, Z) point.</param>
    /// <param name="distance">New distance, or <c>null</c> to keep the current one.</param>
    public void FocusOn(Vector2 point, float? distance = null)
    {
        _targetFocus = ClampToBounds(point);
        if (distance is { } value)
        {
            _targetDistance = Math.Clamp(value, _settings.MinDistance, MaxDistance);
        }
    }

    /// <summary>Whether the camera shows the strategic overview (spec §56, ADR-031).</summary>
    public bool InOverview => _beforeOverview is not null;

    /// <summary>
    /// Toggles the strategic overview (spec §56, ADR-031): the whole island from as far as the camera
    /// may go, and back to where the camera was before.
    /// </summary>
    public void ToggleOverview()
    {
        if (_beforeOverview is { } before)
        {
            _beforeOverview = null;
            FocusOn(before.Focus, before.Distance);
            return;
        }

        _beforeOverview = (_targetFocus, _targetDistance);
        FocusOn((_boundsMin + _boundsMax) / 2f, MaxDistance);
    }

    /// <summary>Jumps to the target values immediately, for example after loading a world.</summary>
    public void SnapToTarget()
    {
        Focus = _targetFocus;
        Yaw = _targetYaw;
        Distance = _targetDistance;
        _hasFocusHeight = false;
    }

    /// <summary>Eases current values towards their targets.</summary>
    /// <param name="deltaSeconds">Frame time.</param>
    /// <param name="groundHeight">Ground height at a world (X, Z) position.</param>
    public void Update(float deltaSeconds, Func<float, float, float> groundHeight)
    {
        ArgumentNullException.ThrowIfNull(groundHeight);
        var blend = 1f - MathF.Exp(-_settings.Smoothing * MathF.Max(0f, deltaSeconds));

        Focus = Vector2.Lerp(Focus, _targetFocus, blend);
        Yaw += (_targetYaw - Yaw) * blend;

        // Easing in log space gives the same feel at every zoom level.
        Distance = MathF.Exp(MathF.Log(Distance) + ((MathF.Log(_targetDistance) - MathF.Log(Distance)) * blend));

        var ground = groundHeight(Focus.X, Focus.Y);
        FocusHeight = _hasFocusHeight ? FocusHeight + ((ground - FocusHeight) * blend) : ground;
        _hasFocusHeight = true;

        WrapYaw();
    }

    /// <summary>Computes the camera placement, kept above the ground.</summary>
    /// <param name="groundHeight">Ground height at a world (X, Z) position.</param>
    /// <returns>Camera position and look-at target.</returns>
    public CameraPose ComputePose(Func<float, float, float> groundHeight)
    {
        ArgumentNullException.ThrowIfNull(groundHeight);
        var pitch = Pitch;
        var horizontal = Distance * MathF.Cos(pitch);
        var target = new Vector3(Focus.X, FocusHeight, Focus.Y);
        var position = target + new Vector3(MathF.Sin(Yaw) * horizontal, Distance * MathF.Sin(pitch), MathF.Cos(Yaw) * horizontal);

        var minimumHeight = groundHeight(position.X, position.Z) + _settings.MinClearance;
        if (position.Y < minimumHeight)
        {
            position.Y = minimumHeight;
        }

        return new CameraPose(position, target);
    }

    /// <summary>Converts view-relative axes (right, forward) into a ground direction for the current yaw.</summary>
    private Vector2 ViewToGround(Vector2 axes)
    {
        var forward = new Vector2(-MathF.Sin(Yaw), -MathF.Cos(Yaw));
        var right = new Vector2(MathF.Cos(Yaw), -MathF.Sin(Yaw));
        return (right * axes.X) + (forward * axes.Y);
    }

    /// <summary>Pitch rises smoothly with log distance: low and cinematic up close, near top-down when far.</summary>
    private float ComputePitch(float distance)
    {
        var logMin = MathF.Log(_settings.MinDistance);
        var t = (MathF.Log(distance) - logMin) / (MathF.Log(MaxDistance) - logMin);
        t = Math.Clamp(t, 0f, 1f);
        var eased = t * t * (3f - (2f * t));
        var degrees = _settings.MinPitchDegrees + ((_settings.MaxPitchDegrees - _settings.MinPitchDegrees) * eased);
        return degrees * (MathF.PI / 180f);
    }

    /// <summary>Clamps a ground point into the focus bounds.</summary>
    private Vector2 ClampToBounds(Vector2 point) => Vector2.Clamp(point, _boundsMin, _boundsMax);

    /// <summary>Keeps yaw values small by removing whole turns from both the current and target yaw.</summary>
    private void WrapYaw()
    {
        if (MathF.Abs(_targetYaw) <= MathF.Tau)
        {
            return;
        }

        var turns = MathF.Truncate(_targetYaw / MathF.Tau) * MathF.Tau;
        _targetYaw -= turns;
        Yaw -= turns;
    }
}
