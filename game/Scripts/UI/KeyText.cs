using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Pantheon.Game.Controls;

namespace Pantheon.Client.UI;

/// <summary>
/// The keys of the game's input actions in words, for the key map, the god's bar and the debug
/// palette (ADR-068). They are read from the bindings, with the letters of the keyboard's layout.
/// </summary>
internal static class KeyText
{
    /// <summary>An action's first key, for a badge: for example "Tab" or "P"; empty when it has none.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The key.</returns>
    public static string Of(string? action) =>
        action is not null && InputMap.HasAction(action) && InputMap.ActionGetEvents(action).Select(Name).FirstOrDefault(n => n.Length > 0) is { } name ? Brief(name) : string.Empty;

    /// <summary>A key's name short enough for a badge: the backtick without its name.</summary>
    private static string Brief(string name) => name.EndsWith(" (backtick)", StringComparison.Ordinal) ? name[..^" (backtick)".Length] : name;

    /// <summary>
    /// A line of the key map's keys: each action's first keys together, then their other keys, then
    /// the gesture; for example "W A S D, or ↑ ← ↓ →, or two fingers on a trackpad".
    /// </summary>
    /// <param name="entry">The line.</param>
    /// <returns>The keys in words.</returns>
    public static string Of(KeyEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var keys = entry.Actions.Select(a => InputMap.HasAction(a) ? InputMap.ActionGetEvents(a).Select(Name).Where(n => n.Length > 0).ToList() : []).ToList();
        var rounds = new List<string>();
        for (var round = 0; round < keys.Select(k => k.Count).DefaultIfEmpty(0).Max(); round++)
        {
            var names = keys.Where(k => round < k.Count).Select(k => Held(entry.Modifier) + k[round]).Distinct(StringComparer.Ordinal).ToList();
            rounds.Add(Joined(names));
        }

        var text = string.Join(", or ", rounds.Distinct(StringComparer.Ordinal));
        return entry.Gesture is not { } gesture ? text
            : text.Length == 0 ? gesture
            : gesture.StartsWith("or ", StringComparison.Ordinal) ? $"{text}, {gesture}"
            : $"{text} {gesture}";
    }

    /// <summary>Keys pressed together: a run of digits as a range (1–0), two with a slash, more with spaces.</summary>
    private static string Joined(IReadOnlyList<string> names) =>
        names.Count >= 4 && names.All(n => n.Length > 0 && char.IsDigit(n[^1])) ? $"{names[0]}–{names[^1][^1]}"
        : names.Count == 2 ? $"{names[0]} / {names[1]}"
        : string.Join(" ", names);

    /// <summary>The key held with the action's, as it is written before it.</summary>
    private static string Held(KeyModifier modifier) => modifier switch
    {
        KeyModifier.None => string.Empty,
        KeyModifier.Shift => "Shift+",
        KeyModifier.Ctrl => "Ctrl+",
    };

    /// <summary>One binding in words; empty for a kind the map does not show.</summary>
    private static string Name(InputEvent binding) => binding switch
    {
        InputEventKey key => KeyName(key),
        InputEventMouseButton mouse => mouse.ButtonIndex switch
        {
            MouseButton.Left => "Left button",
            MouseButton.Right => "Right button",
            MouseButton.Middle => "Middle button",
            MouseButton.WheelUp or MouseButton.WheelDown => "Wheel",
            _ => string.Empty,
        },
        _ => string.Empty,
    };

    /// <summary>A key in words, with the letter the keyboard's layout prints on it.</summary>
    private static string KeyName(InputEventKey key)
    {
        var code = key.PhysicalKeycode != Key.None ? DisplayServer.KeyboardGetLabelFromPhysical(key.PhysicalKeycode) : key.Keycode;
        var named = code switch
        {
            Key.Escape => "Esc",
            Key.Tab => "Tab",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Home => "Home",
            Key.Pageup => "PgUp",
            Key.Pagedown => "PgDn",
            Key.Up => "↑",
            Key.Down => "↓",
            Key.Left => "←",
            Key.Right => "→",
            Key.KpAdd => "Keypad +",
            Key.KpSubtract => "Keypad −",
            Key.Quoteleft => "` (backtick)",
            _ => null,
        };
        var text = named ?? ((long)code is > 32 and < 127 ? char.ToUpperInvariant((char)(long)code).ToString() : OS.GetKeycodeString(code));
        return (key.ShiftPressed ? "Shift+" : string.Empty) + (key.CtrlPressed ? "Ctrl+" : string.Empty) + text;
    }
}
