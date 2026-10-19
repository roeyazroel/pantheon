namespace Pantheon.Domain.Events;

/// <summary>A protective, brave Creature set out by itself to defend its god's village from the beast (ADR-037).</summary>
public sealed record CreatureDefending : WorldEvent
{
    /// <summary>The Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>The village it defends.</summary>
    public required int SettlementId { get; init; }
}
