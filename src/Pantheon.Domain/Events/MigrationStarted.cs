using Pantheon.Domain.Migration;

namespace Pantheon.Domain.Events;

/// <summary>A group of people left a settlement for another (spec §31, §91 "RefugeesCreated", ADR-016).</summary>
public sealed record MigrationStarted : WorldEvent
{
    /// <summary>The group.</summary>
    public required int GroupId { get; init; }

    /// <summary>The settlement they left.</summary>
    public required int OriginSettlementId { get; init; }

    /// <summary>Where they are heading.</summary>
    public required int DestinationSettlementId { get; init; }

    /// <summary>How many left.</summary>
    public required int People { get; init; }

    /// <summary>Why they left.</summary>
    public required MigrationCause Cause { get; init; }

    /// <summary>The god most of them followed when they left, if any.</summary>
    public required int? GodId { get; init; }

    /// <summary>The god most of them gave up following on leaving, for fear or doubt of it, if any.</summary>
    public required int? AbandonedGodId { get; init; }
}
