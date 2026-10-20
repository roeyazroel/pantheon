using System;
using System.Collections.Generic;
using Pantheon.Game.Controls;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of the commands in view (ADR-068).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>The god's bar's button pressed at the start, for captures (<c>--bar</c>): for example the key map or the map's chooser; none when <c>null</c>.</summary>
    public BarCommand? Bar { get; init; }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] ControlOptions() => ["bar"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyControls(LaunchOptions options, string name, string value, List<string> warnings) => name switch
    {
        "bar" => Enum.TryParse<BarCommand>(value.Trim(), ignoreCase: true, out var button) && Enum.IsDefined(button) && !int.TryParse(value.Trim(), out _)
            ? options with { Bar = button }
            : Warn(options, warnings, $"'--bar' needs one of the god's bar's buttons ({string.Join(", ", Enum.GetNames<BarCommand>()).ToLowerInvariant()}), got '{value}'."),
        _ => null,
    };
}
