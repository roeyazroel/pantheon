using System;
using System.Collections.Generic;
using Pantheon.Domain.Politics;

namespace Pantheon.Game.Launch;

/// <summary>The launch options of Milestone 15's later parts: society, culture and politics (ADR-023 to ADR-025).</summary>
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

    /// <summary>The names this part adds to <see cref="KnownOptions"/>.</summary>
    private static string[] ExpansionOptions() => ["priority", "settlement-panel", "doctrine", "council-panel", "summon-council", "treaty"];

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
            default:
                return null;
        }
    }
}
