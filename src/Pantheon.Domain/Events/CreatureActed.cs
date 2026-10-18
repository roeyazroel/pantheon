using Pantheon.Domain.Creatures;

namespace Pantheon.Domain.Events;

/// <summary>A Creature finished an action.</summary>
public sealed record CreatureActed : WorldEvent
{
    /// <summary>The Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>The action.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>The settlement it acted on, if any.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>People it killed.</summary>
    public required int Deaths { get; init; }

    /// <summary>Food it gave (positive) or took (negative).</summary>
    public required float Food { get; init; }
}
