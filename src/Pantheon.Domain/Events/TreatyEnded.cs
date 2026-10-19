using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>A treaty ended: its term ran out, or a god broke it (spec §40, ADR-025).</summary>
public sealed record TreatyEnded : WorldEvent
{
    /// <summary>The treaty.</summary>
    public required int TreatyId { get; init; }

    /// <summary>Its kind.</summary>
    public required TreatyKind Kind { get; init; }

    /// <summary>The god that proposed it.</summary>
    public required int FirstGodId { get; init; }

    /// <summary>The god that accepted it.</summary>
    public required int SecondGodId { get; init; }

    /// <summary>The god that broke it; <c>null</c> when it ran its term.</summary>
    public required int? BrokenByGodId { get; init; }

    /// <summary>Whether it was still secret.</summary>
    public required bool Secret { get; init; }
}
