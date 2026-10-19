using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>A secret treaty came to light (spec §39, §43, ADR-025).</summary>
public sealed record TreatyRevealed : WorldEvent
{
    /// <summary>The treaty.</summary>
    public required int TreatyId { get; init; }

    /// <summary>Its kind.</summary>
    public required TreatyKind Kind { get; init; }

    /// <summary>The god that proposed it.</summary>
    public required int FirstGodId { get; init; }

    /// <summary>The god that accepted it.</summary>
    public required int SecondGodId { get; init; }

    /// <summary>The god it was aimed at, if any.</summary>
    public required int? TargetGodId { get; init; }
}
