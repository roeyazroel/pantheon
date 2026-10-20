using System;
using System.Collections.Generic;
using Pantheon.Game.Audio;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of the orchestra (ADR-071).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>The music's mood for the whole run, whatever happens (<c>--music</c>); the game's when <c>null</c>.</summary>
    public MusicMood? Music { get; init; }

    /// <summary>A folder the music is written to as WAV files once made, after which the game quits (<c>--music-export</c>); none when <c>null</c>.</summary>
    public string? MusicExport { get; init; }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] MusicOptions() => ["music", "music-export"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyMusic(LaunchOptions options, string name, string value, List<string> warnings) => name switch
    {
        "music" => Enum.TryParse<MusicMood>(value.Trim(), ignoreCase: true, out var mood) && Enum.IsDefined(mood) && !int.TryParse(value.Trim(), out _)
            ? options with { Music = mood }
            : Warn(options, warnings, $"'--music' needs a mood ({string.Join(", ", Enum.GetNames<MusicMood>()).ToLowerInvariant()}), got '{value}'."),
        "music-export" => value.Trim().Length > 0 ? options with { MusicExport = value.Trim() } : Warn(options, warnings, "'--music-export' needs a folder."),
        _ => null,
    };
}
