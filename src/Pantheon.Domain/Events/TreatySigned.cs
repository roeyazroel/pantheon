using Pantheon.Domain.Politics;

namespace Pantheon.Domain.Events;

/// <summary>Two gods signed a treaty (spec §38, ADR-025).</summary>
public sealed record TreatySigned : WorldEvent
{
    /// <summary>The treaty.</summary>
    public required int TreatyId { get; init; }

    /// <summary>Its kind.</summary>
    public required TreatyKind Kind { get; init; }

    /// <summary>The god that proposed it.</summary>
    public required int FirstGodId { get; init; }

    /// <summary>The god that accepted it.</summary>
    public required int SecondGodId { get; init; }

    /// <summary>The god a joint war or defensive pact is aimed at, if any.</summary>
    public required int? TargetGodId { get; init; }

    /// <summary>Whether only the two gods know of it.</summary>
    public required bool Secret { get; init; }
}
