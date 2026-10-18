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

    /// <summary>Glide to the next settlement (F, Home).</summary>
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

    /// <summary>Next slower simulation speed preset ([).</summary>
    public static readonly StringName SimSpeedDown = "sim_speed_down";

    /// <summary>Next faster simulation speed preset (]).</summary>
    public static readonly StringName SimSpeedUp = "sim_speed_up";

    /// <summary>Select the miracles of the miracle bar (1–9), in order.</summary>
    public static readonly IReadOnlyList<StringName> MiracleSlots =
    [
        "miracle_1", "miracle_2", "miracle_3", "miracle_4", "miracle_5", "miracle_6", "miracle_7", "miracle_8", "miracle_9",
    ];

    /// <summary>Cast the selected miracle at the cursor (left mouse button).</summary>
    public static readonly StringName MiracleCast = "miracle_cast";

    /// <summary>Deselect the miracle (Esc).</summary>
    public static readonly StringName MiracleCancel = "miracle_cancel";

    /// <summary>Show the next map overlay, or hide it after the last one (O).</summary>
    public static readonly StringName ToggleMapOverlay = "toggle_map_overlay";

    /// <summary>Glide to the player's Creature (C).</summary>
    public static readonly StringName CreatureFocus = "creature_focus";

    /// <summary>Reward the Creature for what it is doing or just did (Z).</summary>
    public static readonly StringName CreatureReward = "creature_reward";

    /// <summary>Punish the Creature for what it is doing or just did (X).</summary>
    public static readonly StringName CreaturePunish = "creature_punish";

    /// <summary>Send the Creature to the ground under the cursor (V).</summary>
    public static readonly StringName CreatureSend = "creature_send";

    /// <summary>Debug: grant the player's god 500 Divine Power (F4).</summary>
    public static readonly StringName DebugGrantPower = "debug_grant_power";

    /// <summary>Pause and run exactly one simulation tick (.).</summary>
    public static readonly StringName SimStepTick = "sim_step_tick";

    /// <summary>Pause and run ticks up to and including the next settlement step (,).</summary>
    public static readonly StringName SimStepSettlement = "sim_step_settlement";

    /// <summary>Debug famine: spoil every settlement's food stores (F6).</summary>
    public static readonly StringName DebugSpoilFood = "debug_spoil_food";

    /// <summary>Debug migration: settlers arrive in every settlement (F7).</summary>
    public static readonly StringName DebugAddSettlers = "debug_add_settlers";

    /// <summary>Every action, used to check the input map at start-up.</summary>
    public static readonly IReadOnlyList<StringName> All =
    [
        CameraPanForward, CameraPanBack, CameraPanLeft, CameraPanRight, CameraRotateLeft, CameraRotateRight,
        CameraZoomIn, CameraZoomOut, CameraDragPan, CameraDragRotate, CameraFocusSettlement,
        ToggleRegionBoundaries, ToggleDebugOverlay, ToggleRegionMap, WorldNew, WorldQuickSave, WorldQuickLoad, SimTogglePause,
        SimSpeedDown, SimSpeedUp, SimStepTick, SimStepSettlement, DebugSpoilFood, DebugAddSettlers,
        .. MiracleSlots, MiracleCast, MiracleCancel, ToggleMapOverlay, DebugGrantPower,
        CreatureFocus, CreatureReward, CreaturePunish, CreatureSend,
    ];
}
