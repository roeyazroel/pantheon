using System;
using System.Collections.Generic;
using System.Linq;

namespace Pantheon.Game.Controls;

/// <summary>A group of the key map (ADR-068).</summary>
public enum KeyGroup
{
    /// <summary>Moving, turning and zooming the view.</summary>
    View,

    /// <summary>Choosing and casting miracles.</summary>
    Miracles,

    /// <summary>The player's Creature.</summary>
    Creature,

    /// <summary>The hand and the villager the player follows.</summary>
    Hand,

    /// <summary>The panels, the map's overlays and the menu.</summary>
    Panels,

    /// <summary>The clock and the quicksave.</summary>
    Time,

    /// <summary>The debug tools: shown, and working, only when the settings turn them on.</summary>
    Debug,
}

/// <summary>A key held with an action's key for another command.</summary>
public enum KeyModifier
{
    /// <summary>The key alone.</summary>
    None,

    /// <summary>With Shift.</summary>
    Shift,

    /// <summary>With Ctrl.</summary>
    Ctrl,
}

/// <summary>One line of the key map: what the player does, and the input actions (or a gesture in words) that do it.</summary>
/// <param name="Group">Its group.</param>
/// <param name="Text">What it does, for the player.</param>
/// <param name="Actions">The input actions that do it, as <c>project.godot</c> names them; their keys are read from the game's bindings.</param>
/// <param name="Modifier">A key held with them.</param>
/// <param name="Gesture">A mouse or trackpad gesture in words, instead of or besides the actions' keys.</param>
public sealed record KeyEntry(KeyGroup Group, string Text, IReadOnlyList<string> Actions, KeyModifier Modifier = KeyModifier.None, string? Gesture = null);

/// <summary>
/// The key map (ADR-068): every key and gesture in play, in words and in groups. The client shows it
/// with the keys the game binds, and runs the debug group's actions only when the debug tools are on.
/// </summary>
public static class KeyGuide
{
    /// <summary>Every line of the key map, in the order shown.</summary>
    public static IReadOnlyList<KeyEntry> Entries { get; } =
    [
        new(KeyGroup.View, "Move the view", ["camera_pan_forward", "camera_pan_left", "camera_pan_back", "camera_pan_right"], Gesture: "or two fingers on a trackpad"),
        new(KeyGroup.View, "Grab the land and drag it; let go mid-drag to fling it", ["camera_drag_pan"], Gesture: "and drag"),
        new(KeyGroup.View, "Fly to a place and close in", [], Gesture: "Double-click the land"),
        new(KeyGroup.View, "Zoom towards the pointer", ["camera_zoom_in", "camera_zoom_out"], Gesture: "or a pinch"),
        new(KeyGroup.View, "Turn and tilt the view", ["camera_drag_rotate"], Gesture: "and drag; Option with two fingers on a trackpad"),
        new(KeyGroup.View, "Turn", ["camera_rotate_left", "camera_rotate_right"]),
        new(KeyGroup.View, "Tilt", ["camera_tilt_up", "camera_tilt_down"]),
        new(KeyGroup.View, "Look at the next village", ["camera_focus_settlement"]),
        new(KeyGroup.View, "The whole island from afar, and back", ["camera_overview"]),
        new(KeyGroup.Miracles, "Choose a miracle; again to put it down", MiracleSlots),
        new(KeyGroup.Miracles, "Anoint a Prophet, then your domains' miracles", ["miracle_1", "miracle_2", "miracle_3", "miracle_4"], KeyModifier.Shift),
        new(KeyGroup.Miracles, "Cast the chosen miracle where you point", ["miracle_cast"]),
        new(KeyGroup.Miracles, "Put the miracle down, or cancel an order", ["miracle_cancel"]),
        new(KeyGroup.Miracles, "Cast veiled: dearer, and not known as yours unless traced", ["toggle_veil"]),
        new(KeyGroup.Creature, "Look at your Creature and follow it", ["creature_focus"]),
        new(KeyGroup.Creature, "Reward or punish it for what it is doing", ["creature_reward", "creature_punish"]),
        new(KeyGroup.Creature, "Send it where you point", ["creature_send"]),
        new(KeyGroup.Creature, "Set it on the rival Creature nearest where you point", ["creature_send"], KeyModifier.Shift),
        new(KeyGroup.Creature, "Set it to work at the village or wood you point at", ["creature_work"]),
        new(KeyGroup.Hand, "Pick up what you point at, or set down what you hold", ["hand_grab"]),
        new(KeyGroup.Hand, "Throw what you hold", ["hand_grab"], KeyModifier.Shift),
        new(KeyGroup.Hand, "Take an offering from a village that follows you", ["hand_grab"], KeyModifier.Ctrl),
        new(KeyGroup.Hand, "Follow the villager you point at", ["villager_follow"]),
        new(KeyGroup.Hand, "Let the villager you follow go", ["villager_follow"], KeyModifier.Shift),
        new(KeyGroup.Panels, "The Book of the World", ["toggle_book"]),
        new(KeyGroup.Panels, "The village you point at: its people, stores and priority", ["toggle_settlements"]),
        new(KeyGroup.Panels, "The gods: meeting, treaties, war and talk", ["toggle_diplomacy"]),
        new(KeyGroup.Panels, "The Divine Council", ["toggle_council"]),
        new(KeyGroup.Panels, "Your domains and the holy sites", ["toggle_domains"]),
        new(KeyGroup.Panels, "The world's history", ["toggle_history"]),
        new(KeyGroup.Panels, "The next map overlay", ["toggle_map_overlay"]),
        new(KeyGroup.Panels, "The menu: save, load, settings, leave", ["game_menu"]),
        new(KeyGroup.Time, "Pause or resume", ["sim_toggle_pause"]),
        new(KeyGroup.Time, "Slower or faster: ×1, ×4, ×16", ["sim_speed_down", "sim_speed_up"]),
        new(KeyGroup.Time, "Quicksave, and load the quicksave", ["world_quicksave", "world_quickload"]),
        new(KeyGroup.Debug, "The debug panel", ["toggle_debug_overlay"]),
        new(KeyGroup.Debug, "The next layer of the region map", ["toggle_region_map"]),
        new(KeyGroup.Debug, "The regions' borders", ["toggle_region_boundaries"]),
        new(KeyGroup.Debug, "Step one tick", ["sim_step_tick"]),
        new(KeyGroup.Debug, "Run to the next settlement step", ["sim_step_settlement"]),
        new(KeyGroup.Debug, "A new world from a random seed", ["world_new"]),
        new(KeyGroup.Debug, "500 Divine Power", ["debug_grant_power"]),
        new(KeyGroup.Debug, "Bring the nearest Creature to meet yours", ["debug_meet"]),
        new(KeyGroup.Debug, "Summon the Divine Council now", ["debug_council"]),
        new(KeyGroup.Debug, "Spoil every village's food", ["debug_spoil_food"]),
        new(KeyGroup.Debug, "Settlers arrive in every village", ["debug_add_settlers"]),
        new(KeyGroup.Debug, "People leave the village you point at", ["debug_migrate"]),
        new(KeyGroup.Debug, "The next weather spell", ["debug_weather_spell"]),
        new(KeyGroup.Debug, "The next season", ["debug_season"]),
        new(KeyGroup.Debug, "A storm where you point", ["debug_storm"]),
        new(KeyGroup.Debug, "An earthquake where you point", ["debug_quake"]),
        new(KeyGroup.Debug, "A fire where you point", ["debug_fire"]),
        new(KeyGroup.Debug, "A flood, or a wave where you point", ["debug_flood"]),
        new(KeyGroup.Debug, "The next hazard where you point", ["debug_hazard"]),
        new(KeyGroup.Debug, "The next great event", ["debug_great_event"]),
    ];

    /// <summary>The actions that are debug tools: those of the debug group.</summary>
    public static IReadOnlySet<string> DebugActions { get; } = Entries.Where(e => e.Group == KeyGroup.Debug).SelectMany(e => e.Actions).ToHashSet(StringComparer.Ordinal);

    /// <summary>The miracle bar's ten keys.</summary>
    private static string[] MiracleSlots => ["miracle_1", "miracle_2", "miracle_3", "miracle_4", "miracle_5", "miracle_6", "miracle_7", "miracle_8", "miracle_9", "miracle_10"];

    /// <summary>Whether an action is a debug tool, which runs only when the debug tools are on.</summary>
    /// <param name="action">The action's name.</param>
    /// <returns><c>true</c> for a debug tool.</returns>
    public static bool IsDebug(string action) => DebugActions.Contains(action);

    /// <summary>The key map's groups and their lines, as shown: the debug group only with the debug tools on.</summary>
    /// <param name="debugTools">Whether the debug tools are on.</param>
    /// <returns>Each group with its lines, in order.</returns>
    public static IReadOnlyList<(KeyGroup Group, IReadOnlyList<KeyEntry> Entries)> Shown(bool debugTools) =>
        Enum.GetValues<KeyGroup>()
            .Where(group => debugTools || group != KeyGroup.Debug)
            .Select(group => (group, (IReadOnlyList<KeyEntry>)Entries.Where(e => e.Group == group).ToList()))
            .ToList();

    /// <summary>A group's heading.</summary>
    /// <param name="group">The group.</param>
    /// <returns>Its heading, for the player.</returns>
    public static string Heading(KeyGroup group) => group switch
    {
        KeyGroup.View => "The view",
        KeyGroup.Miracles => "Miracles",
        KeyGroup.Creature => "Your Creature",
        KeyGroup.Hand => "The hand and your people",
        KeyGroup.Panels => "Panels and the map",
        KeyGroup.Time => "Time and saves",
        KeyGroup.Debug => "Debug tools",
    };
}
