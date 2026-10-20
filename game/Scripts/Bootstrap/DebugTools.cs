using System;
using Godot;
using Pantheon.Game.Settings;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Whether the debug tools are on (ADR-068): the debug keys, the god's bar's Debug button and the
/// debug overlays. The settings decide; when they leave it to the build, the tools are on when the
/// game runs from the project and off in an exported game.
/// </summary>
internal static class DebugTools
{
    /// <summary>Whether the debug tools are on now.</summary>
    public static bool On { get; private set; } = OS.IsDebugBuild();

    /// <summary>Raised when the tools are turned on or off.</summary>
    public static event Action? Changed;

    /// <summary>Whether the settings turn the tools on, or the build does when they leave it to it.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns><c>true</c> when the tools are on.</returns>
    public static bool Of(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.DebugTools ?? OS.IsDebugBuild();
    }

    /// <summary>Follows the settings.</summary>
    /// <param name="settings">The settings.</param>
    public static void Apply(GameSettings settings)
    {
        var on = Of(settings);
        if (on != On)
        {
            On = on;
            Changed?.Invoke();
        }
    }
}
