namespace Pantheon.Domain.Events;

/// <summary>A god's hand gave an offering from its people's stores (spec §55, ADR-039).</summary>
public sealed record OfferingGiven : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The village it came from, if it still exists.</summary>
    public int? FromSettlementId { get; init; }

    /// <summary>The village it was given to, if any.</summary>
    public int? ToSettlementId { get; init; }

    /// <summary>The god's Creature it fed, if any.</summary>
    public int? ToCreatureId { get; init; }

    /// <summary>The food given.</summary>
    public required float Food { get; init; }

    /// <summary>The wood given.</summary>
    public required float Wood { get; init; }

    /// <summary>The stone given.</summary>
    public required float Stone { get; init; }
}
