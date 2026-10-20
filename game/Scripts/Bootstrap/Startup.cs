using System;
using System.IO;
using Godot;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Settings;
using Pantheon.Persistence;

namespace Pantheon.Client.Bootstrap;

/// <summary>What the client does before the world is shown: content, settings, and checks of the options and the input map.</summary>
internal static class Startup
{
    /// <summary>Where the player's settings are kept.</summary>
    public const string SettingsPath = "user://settings.json";

    /// <summary>
    /// Loads the simulation and presentation content, logging the reason on failure; the settings'
    /// citizens, vegetation and terrain quality apply (ADR-056).
    /// </summary>
    /// <param name="settings">The player's settings.</param>
    /// <returns>The content, or <c>null</c> when it is broken.</returns>
    public static (GameContent Content, PresentationContent Presentation)? TryLoadContent(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            Presentation.GraphicsScale.Terrain = settings.TerrainQuality;
            return (GodotContentLoader.LoadGameContent(), GraphicsScaling.Apply(GodotContentLoader.LoadPresentationContent(), settings));
        }
        catch (Exception exception) when (exception is ContentFormatException or IOException)
        {
            GD.PushError($"Game content could not be loaded: {exception.Message}");
            return null;
        }
    }

    /// <summary>The player's settings, or the defaults when there are none.</summary>
    /// <returns>The settings.</returns>
    public static GameSettings LoadSettings()
    {
        var path = ProjectSettings.GlobalizePath(SettingsPath);
        try
        {
            return GameSettings.Parse(File.Exists(path) ? File.ReadAllText(path) : null);
        }
        catch (IOException exception)
        {
            GD.PushWarning($"Settings could not be read, using the defaults: {exception.Message}");
            return new GameSettings();
        }
    }

    /// <summary>Keeps the player's settings for the next run.</summary>
    /// <param name="settings">The settings.</param>
    public static void SaveSettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            File.WriteAllText(ProjectSettings.GlobalizePath(SettingsPath), settings.Serialize());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Settings could not be saved: {exception.Message}");
        }
    }

    /// <summary>Logs problems found in the command-line options.</summary>
    /// <param name="options">The options.</param>
    public static void ReportLaunchWarnings(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (var warning in options.Warnings)
        {
            GD.PushWarning($"Launch options: {warning}");
        }
    }

    /// <summary>Reports actions that are referenced in code but missing from <c>project.godot</c>.</summary>
    public static void CheckInputMap()
    {
        foreach (var action in InputActions.All)
        {
            if (!InputMap.HasAction(action))
            {
                GD.PushError($"Input action '{action}' is missing from project.godot.");
            }
        }
    }
}
