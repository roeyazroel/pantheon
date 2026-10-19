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
public partial class GodCameraController : Node3D
{
    private const float KeyZoomStepsPerSecond = 5f;

    // Grab-panning ignores rays that are nearly horizontal or that meet the grab plane past this
    // distance, where a pixel of mouse movement would throw the camera across the world.
    private const float MinDragRayDownward = 0.02f;
    private const float MaxDragDistance = 6000f;

    private Camera3D _camera = null!;
    private TerrainField? _terrain;
    private Func<float, float, float> _groundHeight = static (_, _) => 0f;
    private bool _mouseInWindow;
    private bool _grabbing;
    private NVector3 _grabPoint;

    /// <summary>Engine-agnostic camera state.</summary>
    public GodCameraModel Model { get; } = new(GodCameraSettings.Default);

    /// <summary>An offset the camera is drawn at, for earthquakes' shaking (presentation only).</summary>
    public Vector3 ShakeOffset { get; set; }

    /// <summary>The rendering camera.</summary>
    public Camera3D ViewCamera => _camera;

    /// <summary>Whether a mouse at the window border pans the view. Off for unattended runs.</summary>
    public bool EdgeScrollEnabled { get; set; } = true;

    /// <summary>Ground (or sea surface) point under the mouse, updated every frame.</summary>
    public NVector3? CursorGroundPoint { get; private set; }

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
        _grabbing = false;
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

    /// <summary>Applies held keys, edge scrolling and grab-panning, eases the model and moves the camera.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        var deltaSeconds = (float)delta;
        ApplyKeys(deltaSeconds);
        ApplyEdgeScroll(deltaSeconds);
        ApplyGrabPan();
        Model.Update(deltaSeconds, _groundHeight);
        ApplyPose();
        // Scripted runs aim at the screen centre whatever the mouse does, so captures are the same every time.
        var screenPoint = CenterCursorWhenAway ? GetViewport().GetVisibleRect().Size / 2f
            : _mouseInWindow ? GetViewport().GetMousePosition()
            : (Vector2?)null;
        CursorGroundPoint = screenPoint is { } point && TryPickGround(point, out var hit) ? hit : null;
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

    /// <summary>Wheel zoom towards the cursor; right button starts and ends grab-panning.</summary>
    private void HandleMouseButton(InputEventMouseButton button)
    {
        if (button.IsAction(InputActions.CameraDragPan))
        {
            _grabbing = button.Pressed && TryPickGround(button.Position, out _grabPoint);
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

    /// <summary>Middle-drag rotates: dragging right turns the view to the right.</summary>
    private void HandleMouseMotion(InputEventMouseMotion motion)
    {
        _mouseInWindow = true;
        if (Input.IsActionPressed(InputActions.CameraDragRotate))
        {
            Model.Rotate(-motion.Relative.X * Model.Settings.DragRotateRadiansPerPixel);
        }
    }

    /// <summary>WASD/arrow pan, Q/E rotation and keyboard zoom, scaled by frame time.</summary>
    private void ApplyKeys(float deltaSeconds)
    {
        var pan = Input.GetVector(InputActions.CameraPanLeft, InputActions.CameraPanRight, InputActions.CameraPanBack, InputActions.CameraPanForward);
        if (pan != Vector2.Zero)
        {
            Model.Pan(pan.ToNumerics(), deltaSeconds);
        }

        var rotate = Input.GetAxis(InputActions.CameraRotateRight, InputActions.CameraRotateLeft);
        if (rotate != 0f)
        {
            Model.Rotate(rotate * Model.Settings.KeyRotateSpeed * deltaSeconds);
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
        if (!EdgeScrollEnabled || !_mouseInWindow || _grabbing || !GetWindow().HasFocus() || Input.IsActionPressed(InputActions.CameraDragRotate))
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
            Model.Pan(axes, deltaSeconds);
        }
    }

    /// <summary>
    /// Keeps the grabbed ground point under the cursor: the cursor ray is intersected with a
    /// horizontal plane through the grab point (steadier than the terrain itself), and the focus
    /// moves by the difference.
    /// </summary>
    private void ApplyGrabPan()
    {
        if (!_grabbing)
        {
            return;
        }

        if (!Input.IsActionPressed(InputActions.CameraDragPan))
        {
            _grabbing = false;
            return;
        }

        if (TryIntersectHorizontalPlane(GetViewport().GetMousePosition(), _grabPoint.Y, out var current))
        {
            Model.PanWorld(new NVector2(_grabPoint.X - current.X, _grabPoint.Z - current.Z));
        }
    }

    /// <summary>Zooms about the ground under a screen point, or about the focus when there is no ground there.</summary>
    private void ZoomAtScreenPoint(float steps, Vector2 screenPoint)
    {
        if (TryPickGround(screenPoint, out var hit))
        {
            Model.ZoomAt(steps, new NVector2(hit.X, hit.Z));
        }
        else
        {
            Model.Zoom(steps);
        }
    }

    /// <summary>Places the camera at the model's pose.</summary>
    private void ApplyPose()
    {
        var pose = Model.ComputePose(_groundHeight);
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
