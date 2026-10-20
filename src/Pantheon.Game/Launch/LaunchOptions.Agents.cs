using System.Collections.Generic;
using System.Globalization;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of the gods' and Creatures' agents (ADR-072).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>Whether the agents think for the gods and Creatures this run (<c>--agents=on|off</c>); the settings' choice when <c>null</c>.</summary>
    public bool? Agents { get; init; }

    /// <summary>An AI god woken at the start to look about it (<c>--wake=&lt;god id&gt;</c>, or 0 for every AI god); none when <c>null</c>.</summary>
    public int? Wake { get; init; }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] AgentOptions() => ["agents", "wake"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyAgents(LaunchOptions options, string name, string value, List<string> warnings) => name switch
    {
        "agents" => value.Trim().ToLowerInvariant() switch
        {
            "on" => options with { Agents = true },
            "off" => options with { Agents = false },
            _ => Warn(options, warnings, $"'--agents' needs on or off, got '{value}'."),
        },
        "wake" => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var god) && god >= 0
            ? options with { Wake = god }
            : Warn(options, warnings, $"'--wake' needs a god's id, or 0 for every AI god, got '{value}'."),
        _ => null,
    };
}
