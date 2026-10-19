namespace Pantheon.Domain.Creatures;

/// <summary>Something a Creature did and remembers, with what its god made of it.</summary>
public sealed class CreatureMemory
{
    /// <summary>Tick at which it finished the action.</summary>
    public required long Tick { get; init; }

    /// <summary>The action.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>The situation it acted in.</summary>
    public required CreatureSituation Situation { get; init; }

    /// <summary>The settlement it acted on, if any.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>The Creature it acted towards, if any (ADR-017).</summary>
    public required int? OtherCreatureId { get; init; }

    /// <summary>The god's reward or punishment.</summary>
    public required CreatureFeedback Feedback { get; set; }
}
