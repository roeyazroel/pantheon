using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.World;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of the New World page's choices (ADR-082), for worlds made from the command line and for captures.</summary>
public sealed partial record LaunchOptions
{
    /// <summary>The player's god's name in a new world (<c>--god-name</c>); <c>gods.json</c>'s when <c>null</c>.</summary>
    public string? GodName { get; init; }

    /// <summary>The player's Creature's name in a new world (<c>--creature-name</c>).</summary>
    public string? CreatureName { get; init; }

    /// <summary>How many gods a new world holds (<c>--gods</c>); the size's own when <c>null</c>.</summary>
    public int? Gods { get; init; }

    /// <summary>How many villages a new island is founded with for its size (<c>--villages</c>).</summary>
    public VillageCount? Villages { get; init; }

    /// <summary>The player's god's colour, from 1 for <c>divine_view.json</c>'s first (<c>--color</c>); its id's when <c>null</c>.</summary>
    public int? Color { get; init; }

    /// <summary>Which personalities a new world's rivals may be born with (<c>--rival-temper</c>).</summary>
    public RivalTemper? RivalTemper { get; init; }

    /// <summary>How strongly a new world's AI gods play (<c>--rival-strength</c>).</summary>
    public RivalStrength? RivalStrength { get; init; }

    /// <summary>How often calamities strike a new world (<c>--calamities</c>).</summary>
    public CalamityLevel? Calamities { get; init; }

    /// <summary>How many minutes a new world's victory must be held (<c>--hold</c>).</summary>
    public int? Hold { get; init; }

    /// <summary>A new world's options with this part's choices.</summary>
    /// <param name="world">The options of the other parts.</param>
    /// <returns>The options.</returns>
    private NewWorldOptions WithSetup(NewWorldOptions world) => world with
    {
        GodName = WorldChoices.CleanName(GodName),
        CreatureName = WorldChoices.CleanName(CreatureName),
        Gods = Gods,
        Villages = Villages ?? VillageCount.Normal,
        Color = Color is { } color ? color - 1 : null,
        Temper = RivalTemper ?? Domain.World.RivalTemper.Mixed,
        Rivals = RivalStrength ?? Domain.World.RivalStrength.Even,
        Calamities = Calamities ?? CalamityLevel.Normal,
        HoldMinutes = Hold,
    };

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] SetupOptions() => ["god-name", "creature-name", "gods", "villages", "color", "rival-temper", "rival-strength", "calamities", "hold"];

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplySetup(LaunchOptions options, string name, string value, List<string> warnings)
    {
        var trimmed = value.Trim();
        return name switch
        {
            "god-name" => WorldChoices.CleanName(trimmed) is { } god ? options with { GodName = god } : Warn(options, warnings, $"'--god-name' needs 1 to {WorldChoices.NameLength} characters, got '{value}'."),
            "creature-name" => WorldChoices.CleanName(trimmed) is { } creature ? options with { CreatureName = creature } : Warn(options, warnings, $"'--creature-name' needs 1 to {WorldChoices.NameLength} characters, got '{value}'."),
            "gods" => Whole(trimmed) is { } gods && gods >= WorldChoices.MinGods ? options with { Gods = gods } : Warn(options, warnings, $"'--gods' needs a number of gods, at least {WorldChoices.MinGods}, got '{value}'."),
            "villages" => Named<VillageCount>(trimmed) is { } villages ? options with { Villages = villages } : Warn(options, warnings, $"'--villages' needs few, normal or many, got '{value}'."),
            "color" => Whole(trimmed) is { } color && color >= 1 ? options with { Color = color } : Warn(options, warnings, $"'--color' needs a colour's number, from 1, got '{value}'."),
            "rival-temper" => Named<RivalTemper>(trimmed) is { } temper ? options with { RivalTemper = temper } : Warn(options, warnings, $"'--rival-temper' needs mixed, peaceful or warlike, got '{value}'."),
            "rival-strength" => Named<RivalStrength>(trimmed) is { } strength ? options with { RivalStrength = strength } : Warn(options, warnings, $"'--rival-strength' needs weak, even or strong, got '{value}'."),
            "calamities" => Named<CalamityLevel>(trimmed) is { } calamities ? options with { Calamities = calamities } : Warn(options, warnings, $"'--calamities' needs gentle, normal or harsh, got '{value}'."),
            "hold" => Whole(trimmed) is { } minutes && minutes is >= 1 and <= 600 ? options with { Hold = minutes } : Warn(options, warnings, $"'--hold' needs the minutes a victory is held, 1 to 600, got '{value}'."),
            _ => null,
        };
    }

    /// <summary>A whole number in plain digits, or <c>null</c>.</summary>
    private static int? Whole(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>A member of an enum by its name, ignoring case; never by a number.</summary>
    private static T? Named<T>(string text)
        where T : struct, Enum => !int.TryParse(text, out _) && Enum.TryParse<T>(text, ignoreCase: true, out var member) && Enum.IsDefined(member) ? member : null;
}
