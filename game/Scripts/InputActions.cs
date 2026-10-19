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

    /// <summary>Select the miracles of the miracle bar (1–9, then 0), in order; with Shift, 1–3 select the domains' miracles after the tenth (ADR-021, ADR-022).</summary>
    public static readonly IReadOnlyList<StringName> MiracleSlots =
    [
        "miracle_1", "miracle_2", "miracle_3", "miracle_4", "miracle_5", "miracle_6", "miracle_7", "miracle_8", "miracle_9", "miracle_10",
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

    /// <summary>Show or hide the diplomacy panel (P).</summary>
    public static readonly StringName ToggleDiplomacy = "toggle_diplomacy";

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

    /// <summary>Debug weather: start the next spell, normal, dry or wet (F8).</summary>
    public static readonly StringName DebugWeatherSpell = "debug_weather_spell";

    /// <summary>Debug weather: call a storm at the cursor (F10).</summary>
    public static readonly StringName DebugStorm = "debug_storm";

    /// <summary>Debug disaster: an earthquake at the cursor (F11).</summary>
    public static readonly StringName DebugQuake = "debug_quake";

    /// <summary>Debug disaster: a fire at the cursor (F12).</summary>
    public static readonly StringName DebugFire = "debug_fire";

    /// <summary>Debug: the nearest river floods, or a wave floods the land at the cursor (L, ADR-022).</summary>
    public static readonly StringName DebugFlood = "debug_flood";

    /// <summary>Debug: send people out of the village under the cursor (M).</summary>
    public static readonly StringName DebugMigrate = "debug_migrate";

    /// <summary>Debug: bring the nearest other Creature to yours, so they meet (K).</summary>
    public static readonly StringName DebugMeet = "debug_meet";

    /// <summary>Debug: the next season begins now (Y, ADR-028).</summary>
    public static readonly StringName DebugSeason = "debug_season";

    /// <summary>Debug: the next hazard of the cycle starts at the cursor (I, ADR-029).</summary>
    public static readonly StringName DebugHazard = "debug_hazard";

    /// <summary>The hand picks up what is at the cursor, or sets it down; with Shift, throws it (R, ADR-030).</summary>
    public static readonly StringName HandGrab = "hand_grab";

    /// <summary>Opens or closes the Book of the World (Tab, ADR-031).</summary>
    public static readonly StringName ToggleBook = "toggle_book";

    /// <summary>The camera shows the whole island, or goes back (`, ADR-031).</summary>
    public static readonly StringName CameraOverview = "camera_overview";

    /// <summary>The pause menu (Esc, when no miracle is selected).</summary>
    public static readonly StringName GameMenu = "game_menu";

    /// <summary>Show or hide the world's history (H).</summary>
    public static readonly StringName ToggleHistory = "toggle_history";

    /// <summary>Show or hide the Domains panel with the holy sites (G, ADR-021).</summary>
    public static readonly StringName ToggleDomains = "toggle_domains";

    /// <summary>Show the Settlements panel for the village under the cursor, or hide it (N, ADR-023).</summary>
    public static readonly StringName ToggleSettlements = "toggle_settlements";

    /// <summary>Show or hide the Divine Council (U, ADR-025).</summary>
    public static readonly StringName ToggleCouncil = "toggle_council";

    /// <summary>Debug: the Divine Council meets now (J, ADR-025).</summary>
    public static readonly StringName DebugCouncil = "debug_council";

    /// <summary>Cast miracles veiled, or in the open (T, ADR-026).</summary>
    public static readonly StringName ToggleVeil = "toggle_veil";

    /// <summary>Every action, used to check the input map at start-up.</summary>
    public static readonly IReadOnlyList<StringName> All =
    [
        CameraPanForward, CameraPanBack, CameraPanLeft, CameraPanRight, CameraRotateLeft, CameraRotateRight,
        CameraZoomIn, CameraZoomOut, CameraDragPan, CameraDragRotate, CameraFocusSettlement,
        ToggleRegionBoundaries, ToggleDebugOverlay, ToggleRegionMap, WorldNew, WorldQuickSave, WorldQuickLoad, SimTogglePause,
        SimSpeedDown, SimSpeedUp, SimStepTick, SimStepSettlement, DebugSpoilFood, DebugAddSettlers, DebugWeatherSpell, DebugStorm, DebugQuake, DebugFire, DebugFlood, DebugMigrate, DebugMeet, DebugSeason, DebugHazard, HandGrab, ToggleBook, CameraOverview, GameMenu, ToggleHistory, ToggleDomains, ToggleSettlements, ToggleCouncil, DebugCouncil, ToggleVeil,
        .. MiracleSlots, MiracleCast, MiracleCancel, ToggleMapOverlay, DebugGrantPower,
        CreatureFocus, CreatureReward, CreaturePunish, CreatureSend, ToggleDiplomacy,
    ];
}
