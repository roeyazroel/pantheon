using System;
using System.Collections.Generic;
using Pantheon.Game.Angel;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of the player's angel (ADR-074).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>Whether the angel is kept this run (<c>--angel=on|off</c>, or a stage); the settings' choice when <c>null</c>.</summary>
    public bool? Angel { get; init; }

    /// <summary>A stage the angel's look is held at, and it is called at the start (<c>--angel=radiant|kind|unformed|torn|stern|fallen</c>).</summary>
    public AngelStage? AngelLook { get; init; }

    /// <summary>A question the player's god asks its angel at the start (<c>--angel-ask=&lt;words&gt;</c>).</summary>
    public string? AngelAsk { get; init; }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] AngelOptions() => ["angel", "angel-ask"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyAngel(LaunchOptions options, string name, string value, List<string> warnings) => name switch
    {
        "angel" => value.Trim().ToLowerInvariant() switch
        {
            "on" => options with { Angel = true },
            "off" => options with { Angel = false },
            var stage when Enum.TryParse<AngelStage>(stage, ignoreCase: true, out var held) && Enum.IsDefined(held) => options with { Angel = true, AngelLook = held },
            _ => Warn(options, warnings, $"'--angel' needs on, off or a stage (radiant, kind, unformed, torn, stern, fallen), got '{value}'."),
        },
        "angel-ask" => options with { AngelAsk = value.Trim() },
        _ => null,
    };
}
