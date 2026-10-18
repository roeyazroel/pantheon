using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Launch;

/// <summary>
/// Command-line options, passed to Godot after <c>--</c>, for example
/// <c>godot --path game -- --seed=1234 --terrain=fallback</c>. Both <c>--name=value</c> and
/// <c>--name value</c> are accepted. Invalid input produces a warning rather than an error.
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>Option names (without the leading <c>--</c>) that <see cref="Parse"/> understands.</summary>
    public static IReadOnlySet<string> KnownOptions { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "seed",
        "load",
        "terrain",
        "advance-ticks",
        "camera-distance",
        "quit-after-frames",
        "screenshot",
        "divine-power",
        "cast",
        "overlay",
        "focus",
        "debug-panel",
        "select",
        "judge",
        "diplomacy-panel",
    };

    /// <summary>Seed used when no <c>--seed</c> or <c>--load</c> option is given.</summary>
    public static WorldSeed DefaultSeed { get; } = new(1234);

    /// <summary>Seed of the world to generate, or <c>null</c> to use <see cref="DefaultSeed"/>.</summary>
    public WorldSeed? Seed { get; init; }

    /// <summary>Save file to load at startup instead of generating a world.</summary>
    public string? LoadPath { get; init; }

    /// <summary>Terrain renderer preference.</summary>
    public TerrainBackendPreference Terrain { get; init; } = TerrainBackendPreference.Auto;

    /// <summary>Simulation ticks to run before the first frame, for example to capture a grown settlement.</summary>
    public int? AdvanceTicks { get; init; }

    /// <summary>Starting camera distance in metres; the camera clamps it to its zoom range.</summary>
    public float? CameraDistance { get; init; }

    /// <summary>Quit automatically after this many frames (smoke tests and captures).</summary>
    public int? QuitAfterFrames { get; init; }

    /// <summary>Save a screenshot to this path just before the automatic quit.</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>Divine Power the player's god starts with, replacing the generated value (debug).</summary>
    public float? DivinePower { get; init; }

    /// <summary>Miracles the player's god casts, in order, on the focused settlement once the world is ready (debug).</summary>
    public IReadOnlyList<string> CastAtStart { get; init; } = [];

    /// <summary>Map overlay shown at the start.</summary>
    public OverlayKind? Overlay { get; init; }

    /// <summary>Settlement (1 for the first) the camera starts on and <see cref="CastAtStart"/> targets.</summary>
    public int? FocusSettlement { get; init; }

    /// <summary>Whether the camera starts on the player's Creature (<c>--focus=creature</c>).</summary>
    public bool FocusCreature { get; init; }

    /// <summary>Miracle selected at the start, so a capture shows its targeting ring at the screen centre.</summary>
    public string? SelectMiracle { get; init; }

    /// <summary>Reward or punishment given to the player's Creature on the first frame, so a capture shows the hand's gesture.</summary>
    public CreatureCommandKind? Judge { get; init; }

    /// <summary>Whether the diplomacy panel starts visible.</summary>
    public bool DiplomacyPanel { get; init; }

    /// <summary>Whether the F1 debug panel starts visible.</summary>
    public bool? DebugPanel { get; init; }

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

    /// <summary>Applies one option, recording a warning when it is unknown or its value is missing or invalid.</summary>
    private static LaunchOptions Apply(LaunchOptions options, string name, string? value, List<string> warnings)
    {
        if (!KnownOptions.Contains(name))
        {
            return Warn(options, warnings, $"Ignoring unknown option '--{name}'.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return Warn(options, warnings, $"Option '--{name}' needs a value.");
        }

        switch (name)
        {
            case "seed":
                return options with { Seed = WorldSeed.Parse(value) };
            case "load":
                return options with { LoadPath = value };
            case "terrain":
                return TryParseTerrain(value, out var terrain)
                    ? options with { Terrain = terrain }
                    : Warn(options, warnings, $"Unknown terrain backend '{value}'; expected auto, terrain3d or fallback.");
            case "advance-ticks":
                return TryParsePositiveInt(value, out var ticks)
                    ? options with { AdvanceTicks = ticks }
                    : Warn(options, warnings, $"'--advance-ticks' needs a positive integer, got '{value}'.");
            case "camera-distance":
                return TryParsePositiveFloat(value, out var distance)
                    ? options with { CameraDistance = distance }
                    : Warn(options, warnings, $"'--camera-distance' needs a positive number of metres, got '{value}'.");
            case "quit-after-frames":
                return TryParsePositiveInt(value, out var frames)
                    ? options with { QuitAfterFrames = frames }
                    : Warn(options, warnings, $"'--quit-after-frames' needs a positive integer, got '{value}'.");
            case "screenshot":
                return options with { ScreenshotPath = value };
            case "divine-power":
                return TryParsePositiveFloat(value, out var power)
                    ? options with { DivinePower = power }
                    : Warn(options, warnings, $"'--divine-power' needs a positive number, got '{value}'.");
            case "cast":
                return options with { CastAtStart = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
            case "overlay":
                return Enum.TryParse<OverlayKind>(value, ignoreCase: true, out var overlay) && Enum.IsDefined(overlay)
                    ? options with { Overlay = overlay }
                    : Warn(options, warnings, $"Unknown overlay '{value}'; expected influence, faith or food.");
            case "focus":
                if (string.Equals(value.Trim(), "creature", StringComparison.OrdinalIgnoreCase))
                {
                    return options with { FocusCreature = true };
                }

                return TryParsePositiveInt(value, out var focus)
                    ? options with { FocusSettlement = focus }
                    : Warn(options, warnings, $"'--focus' needs a settlement number from 1 or 'creature', got '{value}'.");
            case "judge":
                return value.Trim().ToLowerInvariant() switch
                {
                    "reward" => options with { Judge = CreatureCommandKind.Reward },
                    "punish" => options with { Judge = CreatureCommandKind.Punish },
                    _ => Warn(options, warnings, $"'--judge' needs reward or punish, got '{value}'."),
                };
            case "select":
                return options with { SelectMiracle = value.Trim() };
            case "diplomacy-panel":
                return value.Trim().ToLowerInvariant() switch
                {
                    "on" => options with { DiplomacyPanel = true },
                    "off" => options with { DiplomacyPanel = false },
                    _ => Warn(options, warnings, $"'--diplomacy-panel' needs on or off, got '{value}'."),
                };
            case "debug-panel":
                return value.Trim().ToLowerInvariant() switch
                {
                    "on" => options with { DebugPanel = true },
                    "off" => options with { DebugPanel = false },
                    _ => Warn(options, warnings, $"'--debug-panel' needs on or off, got '{value}'."),
                };
            default:
                throw new UnreachableException($"Option '--{name}' is listed in {nameof(KnownOptions)} but not handled.");
        }
    }

    /// <summary>Records a warning and returns the options unchanged.</summary>
    private static LaunchOptions Warn(LaunchOptions options, List<string> warnings, string warning)
    {
        warnings.Add(warning);
        return options;
    }

    /// <summary>Parses a positive whole number written with digits only.</summary>
    private static bool TryParsePositiveInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;

    /// <summary>Parses a positive, finite number in the invariant culture (a dot as the decimal separator).</summary>
    private static bool TryParsePositiveFloat(string value, out float result) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result > 0f && float.IsFinite(result);

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
