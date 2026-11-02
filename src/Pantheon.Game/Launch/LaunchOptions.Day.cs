using System.Collections.Generic;
using System.Globalization;

namespace Pantheon.Game.Launch;

/// <summary>The launch option of the day and night (ADR-079).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>An hour to hold for the whole run, 0 up to 24 (<c>--hour</c>, for captures); the world's clock when <c>null</c>.</summary>
    public float? Hour { get; init; }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] DayOptions() => ["hour"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyDay(LaunchOptions options, string name, string value, List<string> warnings)
    {
        if (name != "hour")
        {
            return null;
        }

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hour) && hour is >= 0f and < 24f
            ? options with { Hour = hour }
            : Warn(options, warnings, $"'--hour' needs an hour from 0 up to 24, got '{value}'.");
    }
}
