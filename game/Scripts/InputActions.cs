using System.Collections.Generic;
using Godot;

namespace Pantheon.Client;

/// <summary>Input action names. They must match the <c>[input]</c> section of <c>project.godot</c>.</summary>
public static class InputActions
{
    /// <summary>Pan towards the top of the screen (W, Up).</summary>
    public static readonly StringName CameraPanForward = "camera_pan_forward";

    /// <summary>Pan towards the bottom of the screen (S, Down).</summary>
    public static readonly StringName CameraPanBack = "camera_pan_back";

    /// <summary>Pan left (A, Left).</summary>
    public static readonly StringName CameraPanLeft = "camera_pan_left";

    /// <summary>Pan right (D, Right).</summary>
    public static readonly StringName CameraPanRight = "camera_pan_right";

    /// <summary>Turn the view left (Q).</summary>
    public static readonly StringName CameraRotateLeft = "camera_rotate_left";

    /// <summary>Turn the view right (E).</summary>
    public static readonly StringName CameraRotateRight = "camera_rotate_right";

    /// <summary>Zoom in (wheel up, =, keypad +).</summary>
    public static readonly StringName CameraZoomIn = "camera_zoom_in";

    /// <summary>Zoom out (wheel down, -, keypad -).</summary>
    public static readonly StringName CameraZoomOut = "camera_zoom_out";

    /// <summary>Hold to grab and drag the ground (right mouse button).</summary>
    public static readonly StringName CameraDragPan = "camera_drag_pan";

    /// <summary>Hold to rotate by dragging (middle mouse button).</summary>
    public static readonly StringName CameraDragRotate = "camera_drag_rotate";

    /// <summary>Glide to the settlement (F, Home).</summary>
    public static readonly StringName CameraFocusSettlement = "camera_focus_settlement";

    /// <summary>Show or hide the region grid (B).</summary>
    public static readonly StringName ToggleRegionBoundaries = "toggle_region_boundaries";

    /// <summary>Show or hide the debug panel (F1).</summary>
    public static readonly StringName ToggleDebugOverlay = "toggle_debug_overlay";

    /// <summary>Show the region debug map's next layer, or hide it after the last one (F3).</summary>
    public static readonly StringName ToggleRegionMap = "toggle_region_map";

    /// <summary>Generate a new world from a random seed (F2).</summary>
    public static readonly StringName WorldNew = "world_new";

    /// <summary>Quicksave (F5).</summary>
    public static readonly StringName WorldQuickSave = "world_quicksave";

    /// <summary>Quickload (F9).</summary>
    public static readonly StringName WorldQuickLoad = "world_quickload";

    /// <summary>Pause or resume the simulation (Space).</summary>
    public static readonly StringName SimTogglePause = "sim_toggle_pause";

    /// <summary>Simulation speed presets (1, 2, 3), in the order of <c>SessionCommands.SpeedPresets</c>.</summary>
    public static readonly IReadOnlyList<StringName> SimSpeedPresets = ["sim_speed_1", "sim_speed_2", "sim_speed_3"];

    /// <summary>Pause and run exactly one simulation tick (.).</summary>
    public static readonly StringName SimStepTick = "sim_step_tick";

    /// <summary>Every action, used to check the input map at start-up.</summary>
    public static readonly IReadOnlyList<StringName> All =
    [
        CameraPanForward, CameraPanBack, CameraPanLeft, CameraPanRight, CameraRotateLeft, CameraRotateRight,
        CameraZoomIn, CameraZoomOut, CameraDragPan, CameraDragRotate, CameraFocusSettlement,
        ToggleRegionBoundaries, ToggleDebugOverlay, ToggleRegionMap, WorldNew, WorldQuickSave, WorldQuickLoad, SimTogglePause,
        .. SimSpeedPresets, SimStepTick,
    ];
}
