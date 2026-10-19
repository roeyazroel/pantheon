using Pantheon.Domain.Notables;

namespace Pantheon.Domain.Events;

/// <summary>A prophet was called or anointed, or a village chose an elder (spec §80, ADR-032).</summary>
public sealed record NotableRose : WorldEvent
{
    /// <summary>The notable.</summary>
    public required int NotableId { get; init; }

    /// <summary>A prophet or an elder.</summary>
    public required NotableKind Kind { get; init; }

    /// <summary>Their god.</summary>
    public required int GodId { get; init; }

    /// <summary>Their village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>Whether their god anointed them with a miracle.</summary>
    public required bool Anointed { get; init; }
}

/// <summary>A prophet preached in a village (ADR-032).</summary>
public sealed record NotablePreached : WorldEvent
{
    /// <summary>The prophet.</summary>
    public required int NotableId { get; init; }

    /// <summary>Their god.</summary>
    public required int GodId { get; init; }

    /// <summary>The village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>People won to the god.</summary>
    public required int Converted { get; init; }
}

/// <summary>A notable died: of age, or in danger, a prophet then a martyr (ADR-032).</summary>
public sealed record NotableDied : WorldEvent
{
    /// <summary>The notable.</summary>
    public required int NotableId { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>A prophet or an elder.</summary>
    public required NotableKind Kind { get; init; }

    /// <summary>Their god.</summary>
    public required int GodId { get; init; }

    /// <summary>Where they died.</summary>
    public required int SettlementId { get; init; }

    /// <summary>Whether a prophet died in danger, a martyr.</summary>
    public required bool Martyred { get; init; }
}

/// <summary>A village's elder turned from their god to another, because the village resents their god or has left it (ADR-032).</summary>
public sealed record ElderTurned : WorldEvent
{
    /// <summary>The elder.</summary>
    public required int NotableId { get; init; }

    /// <summary>The village.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god they turned from.</summary>
    public required int FromGodId { get; init; }

    /// <summary>The god they turned to.</summary>
    public required int ToGodId { get; init; }

    /// <summary>Whether the village resented the god they left; otherwise it had left that god, and they followed it.</summary>
    public bool Resented { get; init; }
}
