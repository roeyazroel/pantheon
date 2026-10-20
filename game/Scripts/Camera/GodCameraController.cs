using System;
using Godot;
using Pantheon.Domain.World;
using Pantheon.Game.Camera;
using Pantheon.Simulation.Terrain;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Camera;

/// <summary>
/// Turns player input into <see cref="GodCameraModel"/> commands and applies the model's pose to
/// the child camera. The camera maths lives in the engine-agnostic model; this node only reads
/// input, picks the ground under the cursor and moves the <see cref="Camera3D"/>.
/// </summary>
/// <remarks>
/// The controls (ADR-058): the left button grabs the land and drags it, and flings it on release;
/// the right or middle button orbits (turn and tilt); the wheel zooms towards the cursor. On a
/// Mac trackpad two fingers pan (or zoom, by the settings), a pinch zooms, and Option or Shift with
/// two fingers turns and tilts. The camera can follow something, such as the Creature, until the
/// player pans away.
/// </remarks>
public partial class GodCameraController : Node3D
{
    private const float KeyZoomStepsPerSecond = 5f;
    private const float KeyTiltRadiansPerSecond = 0.9f;

    // A trackpad's two-finger scroll: share of the view per unit of scroll, zoom steps per unit, and turn and tilt per unit.
    private const float GesturePanViews = 0.06f;
    private const float GestureZoomSteps = 0.35f;
    private const float GestureRotateRadians = 0.05f;
    private const float GestureTiltRadians = 0.03f;

    // Grab-panning ignores rays that are nearly horizontal or that meet the grab plane past this
    // distance, where a pixel of mouse movement would throw the camera across the world.
    private const float MinDragRayDownward = 0.02f;
    private const float MaxDragDistance = 6000f;

    private Camera3D _camera = null!;
    private TerrainField? _terrain;
    private Func<float, float, float> _groundHeight = static (_, _) => 0f;
    private bool _mouseInWindow;
    private readonly LandGrab _grab = new();
    private Func<Vector3?>? _follow;
    private (ulong Frame, Vector2 Position, Vector2 Amount)? _lastGesture;
    private float _followAbove;

    /// <summary>Engine-agnostic camera state.</summary>
    public GodCameraModel Model { get; } = new(GodCameraSettings.Default);

    /// <summary>An offset the camera is drawn at, for earthquakes' shaking (presentation only).</summary>
    public Vector3 ShakeOffset { get; set; }

    /// <summary>The rendering camera.</summary>
    public Camera3D ViewCamera => _camera;

    /// <summary>Whether a mouse at the window border pans the view (a setting, off by default; ADR-058).</summary>
    public bool EdgeScrollEnabled { get; set; }

    /// <summary>Whether a trackpad's two-finger scroll zooms rather than pans (a setting).</summary>
    public bool ScrollZooms { get; set; }

    /// <summary>How fast the keys, the edge and trackpad gestures move and turn the view (a setting, 1 by default).</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>
    /// Whether the left button may grab the land now: not while a miracle is aimed or the Creature
    /// is being sent, when a left click means something else.
    /// </summary>
    public Func<bool> LeftGrabAllowed { get; set; } = static () => true;

    /// <summary>Whether the camera follows something (<see cref="Follow"/>).</summary>
    public bool IsFollowing => _follow is not null;

    /// <summary>Raised when following ends because the player moved the view away.</summary>
    public event Action? FollowEnded;

    /// <summary>Ground (or sea surface) point under the mouse, updated every frame.</summary>
    public NVector3? CursorGroundPoint { get; private set; }

    /// <summary>The ground a command acts on (ADR-068): under the mouse, or the view's centre while the mouse is over the interface.</summary>
    public NVector3? CommandGroundPoint { get; private set; }

    /// <summary>Whether the cursor point is the screen centre whatever the mouse does (scripted runs, so captures can show targeting and the hand).</summary>
    public bool CenterCursorWhenAway { get; set; }

    /// <summary>Finds the camera node.</summary>
    public override void _Ready() => _camera = GetNode<Camera3D>("Camera3D");

    /// <summary>Uses a new terrain for picking and ground clearance, and keeps the focus over it.</summary>
    /// <param name="terrain">Terrain of the current world.</param>
    public void SetTerrain(TerrainField terrain)
    {
        _terrain = terrain;

        // The sea surface counts as ground, so the camera never dips under the water.
        _groundHeight = (x, z) => MathF.Max(terrain.SampleHeight(x, z), 0f);
        Model.SetBounds(NVector2.Zero, new NVector2(terrain.MaxCoordinate));
        _grab.Cancel();
    }

    /// <summary>Moves the focus to a ground position.</summary>
    /// <param name="point">New focus point.</param>
    /// <param name="distance">New camera distance, or <c>null</c> to keep the current one.</param>
    /// <param name="snap">Jump there immediately instead of gliding.</param>
    public void FocusOn(GroundPosition point, float? distance, bool snap)
    {
        Model.FocusOn(new NVector2(point.X, point.Z), distance);
        if (snap)
        {
            Model.SnapToTarget();
            Model.Update(0f, _groundHeight);
            ApplyPose();
        }
    }

    /// <summary>
    /// Follows a moving thing: each frame the focus goes to where it is drawn, and the camera aims a
    /// little above the ground there. Panning away, or another focus, ends it.
    /// </summary>
    /// <param name="target">Where the thing is drawn now, or <c>null</c> when it is gone.</param>
    /// <param name="aimAbove">How far above its feet to aim, in metres.</param>
    public void Follow(Func<Vector3?> target, float aimAbove)
    {
        _follow = target;
        _followAbove = aimAbove;
    }

    /// <summary>Stops following without raising <see cref="FollowEnded"/>.</summary>
    public void StopFollowing()
    {
        _follow = null;
        _followAbove = 0f;
    }

    /// <summary>Applies held keys, edge scrolling and grab-panning, eases the model and moves the camera.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        var deltaSeconds = (float)delta;
        ApplyKeys(deltaSeconds);
        ApplyEdgeScroll(deltaSeconds);
        ApplyGrabPan();
        ApplyFollow();
        Model.Update(deltaSeconds, _groundHeight);
        ApplyPose();
        // Scripted runs aim at the screen centre whatever the mouse does, so captures are the same every time.
        var screenPoint = CenterCursorWhenAway ? GetViewport().GetVisibleRect().Size / 2f
            : _mouseInWindow ? GetViewport().GetMousePosition()
            : (Vector2?)null;
        CursorGroundPoint = screenPoint is { } point && TryPickGround(point, out var hit) ? hit : null;
        CommandGroundPoint = _mouseInWindow && GetViewport().GuiGetHoveredControl() is not null && TryPickGround(GetViewport().GetVisibleRect().Size / 2f, out var centre) ? centre : CursorGroundPoint;
    }

    /// <summary>Handles mouse buttons (zoom, grab), motion (drag-rotate) and the strategic overview (`, ADR-031).</summary>
    /// <param name="event">Input event not consumed by the UI.</param>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.CameraOverview))
        {
            Model.ToggleOverview();
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton button)
        {
            HandleMouseButton(button);
        }
        else if (@event is InputEventMouseMotion motion)
        {
            HandleMouseMotion(motion);
        }
        else if (HandleGesture(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Tracks whether the mouse is inside a focused window, which gates edge scrolling and picking.</summary>
    /// <param name="what">Notification id.</param>
    public override void _Notification(int what)
    {
        // Godot declares notification ids as long constants.
        long notification = what;
        if (notification == NotificationWMMouseEnter)
        {
            _mouseInWindow = true;
        }
        else if (notification is NotificationWMMouseExit or NotificationWMWindowFocusOut)
        {
            _mouseInWindow = false;
        }
    }

    /// <summary>
    /// Whether a gesture repeats the last one in the same frame: Godot 4.7 on macOS pushes each
    /// trackpad gesture through the viewport twice, which would double every pan and pinch.
    /// </summary>
    private bool IsRepeat(InputEventGesture gesture)
    {
        var amount = gesture switch
        {
            InputEventPanGesture pan => pan.Delta,
            InputEventMagnifyGesture magnify => new Vector2(magnify.Factor, -1f),
            _ => Vector2.Zero,
        };
        var key = (Engine.GetProcessFrames(), gesture.Position, amount);
        var repeat = _lastGesture == key;
        _lastGesture = repeat ? null : key;
        return repeat;
    }

    /// <summary>
    /// Wheel zoom towards the cursor; the left button grabs the land once the mouse moves, and flings
    /// it on release; double-clicking the land flies there.
    /// </summary>
    private void HandleMouseButton(InputEventMouseButton button)
    {
        if (button.IsAction(InputActions.CameraDragPan))
        {
            HandleGrabButton(button);
        }
        else if (button.Pressed && button.IsAction(InputActions.CameraZoomIn))
        {
            ZoomAtScreenPoint(WheelSteps(button), button.Position);
        }
        else if (button.Pressed && button.IsAction(InputActions.CameraZoomOut))
        {
            ZoomAtScreenPoint(-WheelSteps(button), button.Position);
        }
    }

    /// <summary>The left button on the land: a press may become a grab, a release flings, a double click flies there.</summary>
    private void HandleGrabButton(InputEventMouseButton button)
    {
        if (button.Pressed && button.DoubleClick && LeftGrabAllowed() && TryPickGround(button.Position, out var point))
        {
            // Double-clicking the land flies there and closes in, as the press before it grabbed nothing.
            _grab.Cancel();
            BreakFollow();
            Model.FocusOn(new NVector2(point.X, point.Z), Model.TargetDistance * 0.6f);
            GetViewport().SetInputAsHandled();
        }
        else if (button.Pressed)
        {
            _grab.Press(button.Position, LeftGrabAllowed() && TryPickGround(button.Position, out var ground) ? ground : null);
        }
        else if (_grab.Release() is { } fling)
        {
            Model.Fling(fling);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// The right or middle button orbits: dragging sideways turns the view, dragging up and down tilts
    /// it; a left press past the threshold starts grabbing the land.
    /// </summary>
    private void HandleMouseMotion(InputEventMouseMotion motion)
    {
        _mouseInWindow = true;
        if (Input.IsActionPressed(InputActions.CameraDragRotate))
        {
            Model.Rotate(-motion.Relative.X * Model.Settings.DragRotateRadiansPerPixel);
            Model.TiltBy(motion.Relative.Y * Model.Settings.DragTiltRadiansPerPixel);
        }

        if (_grab.Move(motion.Position))
        {
            BreakFollow();
            Model.StopFling();
        }
    }

    /// <summary>
    /// A trackpad: two fingers pan the view (or zoom, by the settings), with Option or Shift they turn
    /// and tilt it, and a pinch zooms about the fingers.
    /// </summary>
    /// <param name="event">The gesture.</param>
    /// <returns>Whether it was a gesture the camera used.</returns>
    private bool HandleGesture(InputEvent @event)
    {
        if (@event is InputEventGesture gesture && IsRepeat(gesture))
        {
            return true;
        }

        switch (@event)
        {
            case InputEventPanGesture pan when pan.AltPressed || pan.ShiftPressed:
                Model.Rotate(-pan.Delta.X * GestureRotateRadians * Speed);
                Model.TiltBy(pan.Delta.Y * GestureTiltRadians * Speed);
                return true;
            case InputEventPanGesture pan when ScrollZooms || pan.CtrlPressed || pan.MetaPressed:
                ZoomAtScreenPoint(-pan.Delta.Y * GestureZoomSteps, pan.Position);
                return true;
            case InputEventPanGesture pan:
                BreakFollow();
                Model.PanViews(new NVector2(pan.Delta.X, -pan.Delta.Y) * (GesturePanViews * Speed), Mathf.DegToRad(_camera.Fov));
                return true;
            case InputEventMagnifyGesture magnify:
                if (TryPickGround(magnify.Position, out var hit))
                {
                    Model.ZoomByFactor(magnify.Factor, new NVector2(hit.X, hit.Z));
                }
                else
                {
                    Model.ZoomByFactor(magnify.Factor);
                }

                return true;
            default:
                return false;
        }
    }

    /// <summary>WASD/arrow pan, Q/E rotation and keyboard zoom, scaled by frame time.</summary>
    private void ApplyKeys(float deltaSeconds)
    {
        // Letters typed into a text field (a world's name, a god's words) move nothing.
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
        {
            return;
        }

        var pan = Input.GetVector(InputActions.CameraPanLeft, InputActions.CameraPanRight, InputActions.CameraPanBack, InputActions.CameraPanForward);
        if (pan != Vector2.Zero)
        {
            BreakFollow();
            Model.Pan(pan.ToNumerics(), deltaSeconds * Speed);
        }

        var rotate = Input.GetAxis(InputActions.CameraRotateRight, InputActions.CameraRotateLeft);
        if (rotate != 0f)
        {
            Model.Rotate(rotate * Model.Settings.KeyRotateSpeed * deltaSeconds * Speed);
        }

        var tilt = Input.GetAxis(InputActions.CameraTiltUp, InputActions.CameraTiltDown);
        if (tilt != 0f)
        {
            Model.TiltBy(tilt * KeyTiltRadiansPerSecond * deltaSeconds * Speed);
        }

        // Wheel notches are handled as events; by now their press and release have both arrived,
        // so this axis only sees held zoom keys.
        var zoom = Input.GetAxis(InputActions.CameraZoomOut, InputActions.CameraZoomIn);
        if (zoom != 0f)
        {
            Model.Zoom(zoom * KeyZoomStepsPerSecond * deltaSeconds);
        }
    }

    /// <summary>Pans while the mouse rests at the window border.</summary>
    private void ApplyEdgeScroll(float deltaSeconds)
    {
        if (!EdgeScrollEnabled || !_mouseInWindow || _grab.Active || !GetWindow().HasFocus() || Input.IsActionPressed(InputActions.CameraDragRotate))
        {
            return;
        }

        var viewport = GetViewport();
        var axes = EdgeScroll.ComputeAxes(
            viewport.GetMousePosition().ToNumerics(),
            viewport.GetVisibleRect().Size.ToNumerics(),
            Model.Settings.EdgeScrollMarginPixels);
        if (axes != NVector2.Zero)
        {
            BreakFollow();
            Model.Pan(axes, deltaSeconds * Speed);
        }
    }

    /// <summary>
    /// Keeps the grabbed ground point under the cursor: the cursor ray is intersected with a
    /// horizontal plane through the grab point (steadier than the terrain itself), and the focus
    /// moves by the difference.
    /// </summary>
    private void ApplyGrabPan()
    {
        if (!_grab.Active)
        {
            return;
        }

        if (!Input.IsActionPressed(InputActions.CameraDragPan))
        {
            _grab.Cancel();
            return;
        }

        var point = _grab.Point;
        if (TryIntersectHorizontalPlane(GetViewport().GetMousePosition(), point.Y, out var current))
        {
            Model.PanWorld(new NVector2(point.X - current.X, point.Z - current.Z));
        }

        _grab.Track(Model.Focus);
    }

    /// <summary>Keeps the focus on what the camera follows, aiming a little above its feet.</summary>
    private void ApplyFollow()
    {
        if (_follow is null)
        {
            Model.AimAbove = 0f;
            return;
        }

        if (_follow() is not { } point)
        {
            StopFollowing();
            return;
        }

        Model.FocusOn(new NVector2(point.X, point.Z));
        Model.AimAbove = _followAbove;
    }

    /// <summary>Ends following because the player moved the view, and says so.</summary>
    private void BreakFollow()
    {
        if (_follow is null)
        {
            return;
        }

        StopFollowing();
        FollowEnded?.Invoke();
    }

    /// <summary>Zooms about the ground under a screen point, or about the focus when there is no ground there.</summary>
    private void ZoomAtScreenPoint(float steps, Vector2 screenPoint)
    {
        // While following, zoom about what is followed, so it stays in view.
        if (_follow is not null)
        {
            Model.Zoom(steps);
        }
        else if (TryPickGround(screenPoint, out var hit))
        {
            Model.ZoomAt(steps, new NVector2(hit.X, hit.Z));
        }
        else
        {
            Model.Zoom(steps);
        }
    }

    /// <summary>
    /// The highest roof near a ground point, if any (ADR-057): the camera keeps above roofs as it
    /// does above the ground, so it never looks out from inside a house on a steep village's edge.
    /// </summary>
    public Func<float, float, float?>? Roofs { get; set; }

    /// <summary>Places the camera at the model's pose, above the ground and the roofs.</summary>
    private void ApplyPose()
    {
        var pose = Model.ComputePose((x, z) => Roofs?.Invoke(x, z) is { } roof ? MathF.Max(_groundHeight(x, z), roof) : _groundHeight(x, z));
        _camera.LookAtFromPosition(pose.Position.ToGodot() + ShakeOffset, pose.Target.ToGodot() + ShakeOffset, Vector3.Up);
    }

    /// <summary>Raycasts the terrain (with the sea surface as a floor) under a screen point.</summary>
    private bool TryPickGround(Vector2 screenPoint, out NVector3 hit)
    {
        hit = default;
        if (_terrain is null)
        {
            return false;
        }

        var origin = _camera.ProjectRayOrigin(screenPoint).ToNumerics();
        var direction = _camera.ProjectRayNormal(screenPoint).ToNumerics();
        return TerrainRaycaster.TryRaycast(_terrain, origin, direction, _camera.Far, 0f, out hit);
    }

    /// <summary>Intersects the ray under a screen point with the horizontal plane <c>y = height</c>.</summary>
    private bool TryIntersectHorizontalPlane(Vector2 screenPoint, float height, out NVector3 hit)
    {
        hit = default;
        var origin = _camera.ProjectRayOrigin(screenPoint);
        var direction = _camera.ProjectRayNormal(screenPoint);
        if (direction.Y > -MinDragRayDownward)
        {
            return false;
        }

        var distance = (height - origin.Y) / direction.Y;
        if (distance is < 0f or > MaxDragDistance)
        {
            return false;
        }

        hit = (origin + (direction * distance)).ToNumerics();
        return true;
    }

    /// <summary>Zoom steps of one wheel event; precise touchpads report fractional factors.</summary>
    private static float WheelSteps(InputEventMouseButton button) => button.Factor > 0f ? button.Factor : 1f;
}
