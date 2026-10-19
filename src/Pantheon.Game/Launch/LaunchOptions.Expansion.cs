using System;
using System.Collections.Generic;
using Pantheon.Domain.Politics;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of Milestone 15's later parts: society, culture, politics, conflict and world sizes (ADR-023 to ADR-027).</summary>
public sealed partial record LaunchOptions
{
    /// <summary>A priority the player's god asks of the focused settlement at the start (ADR-023).</summary>
    public Domain.Society.SettlementPriority? Priority { get; init; }

    /// <summary>Whether the Settlements panel is open at the start, on the focused settlement.</summary>
    public bool SettlementPanel { get; init; }

    /// <summary>A doctrine, or secularism, the focused settlement holds at the start (ADR-024).</summary>
    public CultureAtStart? Doctrine { get; init; }

    /// <summary>Whether the council panel (U) is open at the start (ADR-025).</summary>
    public bool CouncilPanel { get; init; }

    /// <summary>Whether the Divine Council meets at the start, for captures.</summary>
    public bool SummonCouncil { get; init; }

    /// <summary>A treaty the player's god signs with its strongest rival at the start (ADR-025).</summary>
    public TreatyKind? Treaty { get; init; }

    /// <summary>A war the player's god declares on its strongest rival at the start, for this goal (ADR-026).</summary>
    public Domain.Conflict.WarGoal? WarGoal { get; init; }

    /// <summary>Whether the focused settlement goes to holy war on a neighbour at the start (ADR-026).</summary>
    public bool HolyWar { get; init; }

    /// <summary>Whether the player casts veiled miracles from the start (ADR-026).</summary>
    public bool Veil { get; init; }

    /// <summary>The size of a new world, by its name in <c>world_sizes.json</c> (ADR-027); the first when <c>null</c>.</summary>
    public string? WorldSize { get; init; }

    /// <summary>How a new world may be won (ADR-027); ascendancy only when <c>null</c>.</summary>
    public Domain.World.VictorySettings? Victory { get; init; }

    /// <summary>Whether a new world turns through four seasons (ADR-028); it does when <c>null</c>.</summary>
    public bool? Seasons { get; init; }

    /// <summary>The season that begins at the start (ADR-028), for captures.</summary>
    public Domain.Weather.Season? Season { get; init; }

    /// <summary>What the player's hand lifts at the start (ADR-030), for captures.</summary>
    public HandAtStart? Hand { get; init; }

    /// <summary>How to make the world these options ask for, with names the content knows.</summary>
    /// <param name="content">Validated content.</param>
    /// <returns>The species, size and victories; an unknown species or size is left to the content's own.</returns>
    public NewWorldOptions NewWorld(GameContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new NewWorldOptions(
            Simulation.Creatures.CreatureModel.FindSpecies(content.Creatures, Species)?.Name,
            WorldMaker.Find(content, WorldSize)?.Name,
            Victory,
            Seasons ?? true);
    }

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] ExpansionOptions() => ["priority", "settlement-panel", "doctrine", "council-panel", "summon-council", "treaty", "war-goal", "holy-war", "veil", "world-size", "victory", "seasons", "season", "hand"];

    /// <summary>
    /// Victories from <c>all</c>, <c>sandbox</c> or a list of <c>ascendancy</c>, <c>conquest</c> and
    /// <c>cultural</c> separated by commas; <c>null</c> when the text is none of these.
    /// </summary>
    private static Domain.World.VictorySettings? ParseVictory(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && parts[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.World.VictorySettings.All;
        }

        if (parts.Length == 1 && parts[0].Equals("sandbox", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.World.VictorySettings.Sandbox;
        }

        var victory = Domain.World.VictorySettings.Sandbox;
        foreach (var part in parts)
        {
            if (part.Equals("ascendancy", StringComparison.OrdinalIgnoreCase))
            {
                victory = victory with { Ascendancy = true };
            }
            else if (part.Equals("conquest", StringComparison.OrdinalIgnoreCase))
            {
                victory = victory with { Conquest = true };
            }
            else if (part.Equals("cultural", StringComparison.OrdinalIgnoreCase))
            {
                victory = victory with { Cultural = true };
            }
            else
            {
                return null;
            }
        }

        return parts.Length > 0 ? victory : null;
    }

    /// <summary>Applies one of this part's options; <c>null</c> when the option is not one of them.</summary>
    private static LaunchOptions? ApplyExpansion(LaunchOptions options, string name, string value, List<string> warnings)
    {
        var trimmed = value.Trim();
        switch (name)
        {
            case "priority":
                return Enum.TryParse<Domain.Society.SettlementPriority>(trimmed, ignoreCase: true, out var priority) && Enum.IsDefined(priority) && !int.TryParse(trimmed, out _)
                    ? options with { Priority = priority }
                    : Warn(options, warnings, $"'--priority' needs a priority such as Temple or Preparation, got '{value}'.");
            case "doctrine":
                return Enum.TryParse<CultureAtStart>(trimmed, ignoreCase: true, out var doctrine) && Enum.IsDefined(doctrine) && !int.TryParse(trimmed, out _)
                    ? options with { Doctrine = doctrine }
                    : Warn(options, warnings, $"'--doctrine' needs a doctrine such as Charity or Militarism, or Secular, got '{value}'.");
            case "settlement-panel":
                return OnOff(value) is { } open ? options with { SettlementPanel = open } : Warn(options, warnings, $"'--settlement-panel' needs on or off, got '{value}'.");
            case "council-panel":
                return OnOff(value) is { } council ? options with { CouncilPanel = council } : Warn(options, warnings, $"'--council-panel' needs on or off, got '{value}'.");
            case "summon-council":
                return OnOff(value) is { } summon ? options with { SummonCouncil = summon } : Warn(options, warnings, $"'--summon-council' needs on or off, got '{value}'.");
            case "treaty":
                return Enum.TryParse<TreatyKind>(trimmed, ignoreCase: true, out var treaty) && Enum.IsDefined(treaty) && !int.TryParse(trimmed, out _)
                    ? options with { Treaty = treaty }
                    : Warn(options, warnings, $"'--treaty' needs a treaty such as NonAggression or MutualDefense, got '{value}'.");
            case "war-goal":
                return Enum.TryParse<Domain.Conflict.WarGoal>(trimmed, ignoreCase: true, out var goal) && Enum.IsDefined(goal) && !int.TryParse(trimmed, out _)
                    ? options with { WarGoal = goal }
                    : Warn(options, warnings, $"'--war-goal' needs Conquest, HolySite, Tribute or Humiliation, got '{value}'.");
            case "holy-war":
                return OnOff(value) is { } holy ? options with { HolyWar = holy } : Warn(options, warnings, $"'--holy-war' needs on or off, got '{value}'.");
            case "veil":
                return OnOff(value) is { } veil ? options with { Veil = veil } : Warn(options, warnings, $"'--veil' needs on or off, got '{value}'.");
            case "world-size":
                return trimmed.Length > 0 ? options with { WorldSize = trimmed } : Warn(options, warnings, "'--world-size' needs a size such as Small, Large or Huge.");
            case "seasons":
                return OnOff(value) is { } seasons ? options with { Seasons = seasons } : Warn(options, warnings, $"'--seasons' needs on or off, got '{value}'.");
            case "season":
                return Enum.TryParse<Domain.Weather.Season>(trimmed, ignoreCase: true, out var season) && Enum.IsDefined(season) && !int.TryParse(trimmed, out _)
                    ? options with { Season = season }
                    : Warn(options, warnings, $"'--season' needs spring, summer, autumn or winter, got '{value}'.");
            case "hand":
                return Enum.TryParse<HandAtStart>(trimmed, ignoreCase: true, out var hand) && Enum.IsDefined(hand) && !int.TryParse(trimmed, out _)
                    ? options with { Hand = hand }
                    : Warn(options, warnings, $"'--hand' needs villagers or creature, got '{value}'.");
            case "victory":
                return ParseVictory(trimmed) is { } victory
                    ? options with { Victory = victory }
                    : Warn(options, warnings, $"'--victory' needs all, sandbox, or ascendancy, conquest and cultural separated by commas, got '{value}'.");
            default:
                return null;
        }
    }
}
