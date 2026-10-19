using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pantheon.Game.Settings;

/// <summary>
/// The player's settings (spec §105's graphics scaling, §60's audio; ADR-021), kept between runs in
/// <c>user://settings.json</c>. Unlike content, the file is the player's: a missing or broken file
/// gives the defaults, unknown members are ignored and values out of range are brought into range.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Lowest render scale offered.</summary>
    public const float MinRenderScale = 0.5f;

    /// <summary>Lowest share of the citizens offered.</summary>
    public const float MinCitizens = 0.2f;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };

    /// <summary>Whether the game fills the screen.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>Whether frames wait for the display's refresh.</summary>
    public bool VSync { get; init; } = true;

    /// <summary>Whether the sun casts shadows.</summary>
    public bool Shadows { get; init; } = true;

    /// <summary>Share of the window's resolution the world is drawn at (0.5..1), upscaled.</summary>
    public float RenderScale { get; init; } = 1f;

    /// <summary>Share of the representative citizens shown (0.2..1); it applies to the next world shown.</summary>
    public float Citizens { get; init; } = 1f;

    /// <summary>Loudness of everything (0..1).</summary>
    public float MasterVolume { get; init; } = 0.8f;

    /// <summary>Loudness of the music (0..1).</summary>
    public float MusicVolume { get; init; } = 0.55f;

    /// <summary>Loudness of miracles, Creatures, disasters and alerts (0..1).</summary>
    public float EffectsVolume { get; init; } = 0.8f;

    /// <summary>Loudness of the wind and the rain (0..1).</summary>
    public float AmbienceVolume { get; init; } = 0.6f;

    /// <summary>The same settings with every value in range.</summary>
    /// <returns>The settings.</returns>
    public GameSettings Clamped() => this with
    {
        RenderScale = Range(RenderScale, MinRenderScale, 1f, 1f),
        Citizens = Range(Citizens, MinCitizens, 1f, 1f),
        MasterVolume = Range(MasterVolume, 0f, 1f, 0.8f),
        MusicVolume = Range(MusicVolume, 0f, 1f, 0.55f),
        EffectsVolume = Range(EffectsVolume, 0f, 1f, 0.8f),
        AmbienceVolume = Range(AmbienceVolume, 0f, 1f, 0.6f),
    };

    /// <summary>Reads settings from a file's text: the defaults when it is missing or broken, every value in range.</summary>
    /// <param name="json">The file's text, or <c>null</c> when there is none.</param>
    /// <returns>The settings.</returns>
    public static GameSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new GameSettings();
        }

        try
        {
            return (JsonSerializer.Deserialize<GameSettings>(json, Options) ?? new GameSettings()).Clamped();
        }
        catch (JsonException)
        {
            return new GameSettings();
        }
    }

    /// <summary>The settings as a file's text.</summary>
    /// <returns>Indented JSON.</returns>
    public string Serialize() => JsonSerializer.Serialize(Clamped(), Options);

    /// <summary>A value within a range; a value that is not a number gives the fallback.</summary>
    private static float Range(float value, float min, float max, float fallback) => float.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
