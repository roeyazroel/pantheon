using System;
using System.IO;
using Godot;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Settings;
using Pantheon.Persistence;
using Pantheon.Persistence.Profile;

namespace Pantheon.Client.Bootstrap;

/// <summary>What the client does before the world is shown: content, settings, and checks of the options and the input map.</summary>
internal static class Startup
{
    /// <summary>Where the player's settings were kept before the profile (ADR-060); read once to bring them in.</summary>
    public const string SettingsPath = "user://settings.json";

    // The profile's key for the settings.
    private const string SettingsKey = "settings";

    // The profile's keys for the player's own model keys (ADR-065, ADR-067), kept apart from the settings so they are never written with them.
    private const string AnthropicKeyKey = "anthropic_key";
    private const string OpenAIKeyKey = "openai_key";
    private const string GeminiKeyKey = "gemini_key";

    private static IProfileStore? _profile;

    /// <summary>
    /// The player's profile (ADR-060): one SQLite file in the game's user directory, or plain files
    /// beside it when SQLite cannot be loaded. It is opened once for the process and closed as it ends.
    /// </summary>
    public static IProfileStore Profile
    {
        get
        {
            if (_profile is null)
            {
                _profile = ProfileStores.Open(ProjectSettings.GlobalizePath("user://"), out var problem);
                if (problem is not null)
                {
                    GD.PushWarning($"The profile is kept in files, as SQLite could not be opened: {problem}");
                }

                AppDomain.CurrentDomain.ProcessExit += (_, _) => _profile?.Dispose();
            }

            return _profile;
        }
    }

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

    /// <summary>The player's settings from the profile, brought in from the old settings file the first time; the defaults when there are none.</summary>
    /// <returns>The settings.</returns>
    public static GameSettings LoadSettings()
    {
        if (Profile.ReadValue(SettingsKey) is { } kept)
        {
            return GameSettings.Parse(kept);
        }

        var path = ProjectSettings.GlobalizePath(SettingsPath);
        try
        {
            var settings = GameSettings.Parse(File.Exists(path) ? File.ReadAllText(path) : null);
            SaveSettings(settings);
            return settings;
        }
        catch (IOException exception)
        {
            GD.PushWarning($"Settings could not be read, using the defaults: {exception.Message}");
            return new GameSettings();
        }
    }

    /// <summary>Keeps the player's settings for the next run, in the profile.</summary>
    /// <param name="settings">The settings.</param>
    public static void SaveSettings(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            Profile.WriteValue(SettingsKey, settings.Serialize());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            GD.PushWarning($"Settings could not be saved: {exception.Message}");
        }
    }

    /// <summary>
    /// The player's own keys for Claude, GPT and Gemini (ADR-065, ADR-067), kept in the profile on this
    /// computer only, apart from the settings; each empty when they gave none. A key goes nowhere but
    /// to its provider, with the gods' questions.
    /// </summary>
    public static Pantheon.Game.Minds.MindKeys Keys => new(Profile.ReadValue(AnthropicKeyKey), Profile.ReadValue(OpenAIKeyKey), Profile.ReadValue(GeminiKeyKey));

    /// <summary>Keeps a provider's key in the profile.</summary>
    /// <param name="provider">Claude, GPT or Gemini.</param>
    /// <param name="key">The key; empty to forget it.</param>
    public static void KeepKey(Pantheon.Game.Minds.MindProvider provider, string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var name = provider switch
        {
            Pantheon.Game.Minds.MindProvider.Claude => AnthropicKeyKey,
            Pantheon.Game.Minds.MindProvider.OpenAI => OpenAIKeyKey,
            Pantheon.Game.Minds.MindProvider.Gemini => GeminiKeyKey,
            Pantheon.Game.Minds.MindProvider.Scribes or Pantheon.Game.Minds.MindProvider.Local => null,
        };
        if (name is null)
        {
            return;
        }

        try
        {
            Profile.WriteValue(name, key.Trim());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            GD.PushWarning($"The key could not be kept: {exception.Message}");
        }
    }

    /// <summary>The gods' minds' content (ADR-065), logging the reason when it is broken.</summary>
    /// <returns>The content, or <c>null</c> when it is broken: worlds are then made with the content's gods.</returns>
    public static Pantheon.Game.Minds.MindContent? TryLoadMinds()
    {
        try
        {
            return GodotContentLoader.LoadMindContent();
        }
        catch (Exception exception) when (exception is ContentFormatException or IOException)
        {
            GD.PushError($"The gods' minds could not be loaded: {exception.Message}");
            return null;
        }
    }

    /// <summary>The orchestra's music (ADR-071), or <c>null</c> when it cannot be read: the game then plays without music.</summary>
    /// <returns>The music, or <c>null</c>.</returns>
    public static Pantheon.Game.Audio.Orchestra.MusicContent? TryLoadMusic()
    {
        try
        {
            return GodotContentLoader.LoadMusicContent();
        }
        catch (Exception exception) when (exception is ContentFormatException or IOException)
        {
            GD.PushError($"The music could not be loaded: {exception.Message}");
            return null;
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
