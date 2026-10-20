using System;
using System.Collections.Generic;
using Pantheon.Game.Minds;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of the gods' minds (ADR-065).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>Who gives the rival gods their minds in this run, over the settings' choice (not kept); the settings' when <c>null</c>.</summary>
    public MindProvider? Minds { get; init; }

    /// <summary>Whether the world's gods are born anew at the start, by the minds chosen, and their page shown (ADR-065).</summary>
    public bool PantheonPage { get; init; }

    /// <summary>Whether the chosen minds are tested at the start, their answer printed (ADR-065).</summary>
    public bool MindCheck { get; init; }

    /// <summary>The god, by id, the player speaks with from the start (ADR-066); none when <c>null</c>.</summary>
    public int? Talk { get; init; }

    /// <summary>What the player says to that god at the start, through the chosen model (ADR-066).</summary>
    public string? TalkSay { get; init; }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] MindOptions() => ["minds", "pantheon", "mind-check", "talk", "talk-say"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyMinds(LaunchOptions options, string name, string value, List<string> warnings)
    {
        var trimmed = value.Trim();
        switch (name)
        {
            case "minds":
                return Enum.TryParse<MindProvider>(trimmed, ignoreCase: true, out var minds) && Enum.IsDefined(minds) && !int.TryParse(trimmed, out _)
                    ? options with { Minds = minds }
                    : Warn(options, warnings, $"'--minds' needs scribes, claude or local, got '{value}'.");
            case "pantheon":
                return OnOff(value) is { } open ? options with { PantheonPage = open } : Warn(options, warnings, $"'--pantheon' needs on or off, got '{value}'.");
            case "mind-check":
                return OnOff(value) is { } check ? options with { MindCheck = check } : Warn(options, warnings, $"'--mind-check' needs on or off, got '{value}'.");
            case "talk":
                return int.TryParse(trimmed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var god) && god >= 2
                    ? options with { Talk = god }
                    : Warn(options, warnings, $"'--talk' needs a rival god's number, 2 or more, got '{value}'.");
            case "talk-say":
                return options with { TalkSay = value.Trim() };
            default:
                return null;
        }
    }
}
