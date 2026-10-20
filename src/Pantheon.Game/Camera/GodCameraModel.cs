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

    // Points between the camera and its focus where the land is checked against the line of sight.
    private const int SightSamples = 10;

    // How far to one side of straight ahead the camera looks at a face from, in radians: a three-quarter view.
    private const float ThreeQuarter = 0.55f;

    private readonly GodCameraSettings _settings;
    private Vector2 _targetFocus;
    private float _targetYaw;
    private float _targetDistance;
    private Vector2 _boundsMin = new(float.NegativeInfinity);
    private Vector2 _boundsMax = new(float.PositiveInfinity);
    private (Vector2 Focus, float Distance)? _beforeOverview;
    private bool _hasFocusHeight;
    private float _targetTilt;
    private Vector2 _fling;

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

    /// <summary>Current tilt the player has added to the pitch the zoom gives, in radians (positive looks more steeply down).</summary>
    public float Tilt { get; private set; }

    /// <summary>Tilt the camera is easing towards.</summary>
    public float TargetTilt => _targetTilt;

    /// <summary>Current pitch below the horizon, in radians: the zoom's pitch and the player's tilt, within the limits.</summary>
    public float Pitch => Math.Clamp(ComputePitch(Distance) + Tilt, Radians(_settings.LowestPitchDegrees), Radians(_settings.SteepestPitchDegrees));

    /// <summary>
    /// How far above the ground under the focus the camera aims, in metres: a followed Creature's
    /// body rather than its feet. The aim eases towards it.
    /// </summary>
    public float AimAbove { get; set; }

    /// <summary>The eased height above the ground the camera aims at now.</summary>
    public float Aim { get; private set; }

    /// <summary>Ground velocity, in metres per second, of a view flung as the land was let go; zero when still.</summary>
    public Vector2 FlingVelocity => _fling;

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
        _fling = Vector2.Zero;
        _targetFocus = ClampToBounds(_targetFocus + (ViewToGround(axes) * (speed * deltaSeconds)));
    }

    /// <summary>
    /// Pans by a share of the view, at once: a trackpad's two-finger scroll. (1, 0) moves the focus a
    /// view's height to the right, (0, 1) a view's height forward.
    /// </summary>
    /// <param name="views">X = right, Y = forward, in view heights.</param>
    /// <param name="verticalFovRadians">The camera's vertical field of view.</param>
    public void PanViews(Vector2 views, float verticalFovRadians)
    {
        var span = 2f * Distance * MathF.Tan(Math.Clamp(verticalFovRadians, 0.01f, 3f) / 2f);
        _fling = Vector2.Zero;
        PanWorld(ViewToGround(views) * span);
    }

    /// <summary>
    /// Lets the view glide on after the land is let go mid-drag, slowing at
    /// <see cref="GodCameraSettings.FlingDamping"/>; any other pan or focus stops it.
    /// </summary>
    /// <param name="velocity">Ground velocity in metres per second, capped by the distance.</param>
    public void Fling(Vector2 velocity)
    {
        var most = _settings.MaxFlingSpeedPerDistance * Distance;
        var speed = velocity.Length();
        _fling = !float.IsFinite(speed) || speed < 0.01f ? Vector2.Zero : speed > most ? velocity * (most / speed) : velocity;
    }

    /// <summary>Stops a flung view where it is.</summary>
    public void StopFling() => _fling = Vector2.Zero;

    /// <summary>Tilts the view: positive looks more steeply down, within <see cref="GodCameraSettings.MaxTiltDegrees"/> either way.</summary>
    /// <param name="radians">Tilt change.</param>
    public void TiltBy(float radians)
    {
        var limit = Radians(_settings.MaxTiltDegrees);
        _targetTilt = Math.Clamp(_targetTilt + radians, -limit, limit);
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

    /// <summary>
    /// Turns the view, the short way round, to look at the front of something facing a heading from
    /// a little to one side (ADR-055: so a Creature's face shows).
    /// </summary>
    /// <param name="heading">The heading it faces, in radians about the vertical: 0 faces +Z.</param>
    public void FaceFront(float heading) =>
        _targetYaw += MathF.IEEERemainder(heading + ThreeQuarter - _targetYaw, MathF.Tau);

    /// <summary>Zooms by a number of steps; positive steps zoom in.</summary>
    /// <param name="steps">Zoom steps (one mouse-wheel notch is one step).</param>
    public void Zoom(float steps) =>
        _targetDistance = Math.Clamp(_targetDistance * MathF.Pow(_settings.ZoomStepFactor, steps), _settings.MinDistance, MaxDistance);

    /// <summary>Zooms by a factor, as a trackpad's pinch does: 2 brings the camera twice as close.</summary>
    /// <param name="factor">How much closer; below 1 pulls back. Not a positive number does nothing.</param>
    /// <param name="groundPoint">World (X, Z) point to zoom about, or <c>null</c> for the focus.</param>
    public void ZoomByFactor(float factor, Vector2? groundPoint = null)
    {
        if (!(factor > 0f) || !float.IsFinite(factor))
        {
            return;
        }

        var steps = MathF.Log(factor) / -MathF.Log(_settings.ZoomStepFactor);
        if (groundPoint is { } point)
        {
            ZoomAt(steps, point);
        }
        else
        {
            Zoom(steps);
        }
    }

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
        _fling = Vector2.Zero;
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
        Tilt = _targetTilt;
        _hasFocusHeight = false;
    }

    /// <summary>Eases current values towards their targets.</summary>
    /// <param name="deltaSeconds">Frame time.</param>
    /// <param name="groundHeight">Ground height at a world (X, Z) position.</param>
    public void Update(float deltaSeconds, Func<float, float, float> groundHeight)
    {
        ArgumentNullException.ThrowIfNull(groundHeight);
        var blend = 1f - MathF.Exp(-_settings.Smoothing * MathF.Max(0f, deltaSeconds));

        if (_fling != Vector2.Zero)
        {
            var seconds = MathF.Max(0f, deltaSeconds);
            PanWorld(_fling * seconds);
            _fling *= MathF.Exp(-_settings.FlingDamping * seconds);
            if (_fling.LengthSquared() < 0.25f)
            {
                _fling = Vector2.Zero;
            }
        }

        Focus = Vector2.Lerp(Focus, _targetFocus, blend);
        Yaw += (_targetYaw - Yaw) * blend;
        Tilt += (_targetTilt - Tilt) * blend;
        Aim += (MathF.Max(0f, AimAbove) - Aim) * blend;

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
        var target = new Vector3(Focus.X, FocusHeight + Aim, Focus.Y);
        var position = target + new Vector3(MathF.Sin(Yaw) * horizontal, Distance * MathF.Sin(pitch), MathF.Cos(Yaw) * horizontal);

        var minimumHeight = groundHeight(position.X, position.Z) + _settings.MinClearance;
        if (position.Y < minimumHeight)
        {
            position.Y = minimumHeight;
        }

        // The camera rises until its line of sight to the focus clears the land between them, so a
        // hill or a cliff never fills the view of what it looks at. A focus not yet settled on its
        // ground (still easing to a new height) is left alone.
        var settled = groundHeight(target.X, target.Z) <= target.Y + _settings.MinClearance;
        for (var i = 2; settled && i < SightSamples; i++)
        {
            var t = i / (float)SightSamples;
            var x = target.X + ((position.X - target.X) * t);
            var z = target.Z + ((position.Z - target.Z) * t);
            var needed = target.Y + ((groundHeight(x, z) + _settings.MinClearance - target.Y) / t);
            if (position.Y < needed)
            {
                position.Y = needed;
            }
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
        return Radians(degrees);
    }

    /// <summary>Degrees in radians.</summary>
    private static float Radians(float degrees) => degrees * (MathF.PI / 180f);

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
