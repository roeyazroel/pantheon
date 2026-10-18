using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.World;

namespace Pantheon.Game.Launch;

/// <summary>
/// Command-line options, passed to Godot after <c>--</c>, for example
/// <c>godot --path game -- --seed=1234 --terrain=fallback</c>. Both <c>--name=value</c> and
/// <c>--name value</c> are accepted. Invalid input produces a warning rather than an error.
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>Seed of the world to generate, or <c>null</c> to use the default seed.</summary>
    public WorldSeed? Seed { get; init; }

    /// <summary>Save file to load at startup instead of generating a world.</summary>
    public string? LoadPath { get; init; }

    /// <summary>Terrain renderer preference.</summary>
    public TerrainBackendPreference Terrain { get; init; } = TerrainBackendPreference.Auto;

    /// <summary>Quit automatically after this many frames (smoke tests and captures).</summary>
    public int? QuitAfterFrames { get; init; }

    /// <summary>Save a screenshot to this path just before the automatic quit.</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>Problems found while parsing.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Parses user command-line arguments.</summary>
    /// <param name="args">Arguments after Godot's <c>--</c> separator.</param>
    /// <returns>The parsed options.</returns>
    public static LaunchOptions Parse(IEnumerable<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var items = new List<string>(args);
        var warnings = new List<string>();
        var options = new LaunchOptions();

        for (var i = 0; i < items.Count; i++)
        {
            var argument = items[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal) || argument.Length == 2)
            {
                warnings.Add($"Ignoring unexpected argument '{argument}'.");
                continue;
            }

            var (name, value) = SplitOption(argument[2..]);
            if (value is null && i + 1 < items.Count && !items[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = items[++i];
            }

            options = Apply(options, name.ToLowerInvariant(), value, warnings);
        }

        return options with { Warnings = warnings };
    }

    /// <summary>Splits <c>name=value</c>; the value is <c>null</c> when there is no <c>=</c>.</summary>
    private static (string Name, string? Value) SplitOption(string body)
    {
        var separator = body.IndexOf('=', StringComparison.Ordinal);
        return separator < 0 ? (body, null) : (body[..separator], body[(separator + 1)..]);
    }

    /// <summary>Applies one option, recording a warning when its value is missing or invalid.</summary>
    private static LaunchOptions Apply(LaunchOptions options, string name, string? value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add($"Option '--{name}' needs a value.");
            return options;
        }

        switch (name)
        {
            case "seed":
                return options with { Seed = WorldSeed.Parse(value) };
            case "load":
                return options with { LoadPath = value };
            case "terrain":
                if (TryParseTerrain(value, out var terrain))
                {
                    return options with { Terrain = terrain };
                }

                warnings.Add($"Unknown terrain backend '{value}'; expected auto, terrain3d or fallback.");
                return options;
            case "quit-after-frames":
                if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var frames) && frames > 0)
                {
                    return options with { QuitAfterFrames = frames };
                }

                warnings.Add($"'--quit-after-frames' needs a positive integer, got '{value}'.");
                return options;
            case "screenshot":
                return options with { ScreenshotPath = value };
            default:
                warnings.Add($"Ignoring unknown option '--{name}'.");
                return options;
        }
    }

    /// <summary>Parses a terrain backend name, case-insensitively.</summary>
    private static bool TryParseTerrain(string value, out TerrainBackendPreference terrain)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "auto":
                terrain = TerrainBackendPreference.Auto;
                return true;
            case "terrain3d":
                terrain = TerrainBackendPreference.Terrain3D;
                return true;
            case "fallback":
                terrain = TerrainBackendPreference.Fallback;
                return true;
            default:
                terrain = TerrainBackendPreference.Auto;
                return false;
        }
    }
}
