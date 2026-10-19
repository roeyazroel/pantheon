using Pantheon.Domain.Culture;

namespace Pantheon.Domain.Events;

/// <summary>A settlement came to hold a doctrine (spec §33, ADR-024).</summary>
public sealed record DoctrineEmbraced : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The doctrine.</summary>
    public required Doctrine Doctrine { get; init; }
}
