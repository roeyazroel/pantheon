using Pantheon.Domain.Migration;

namespace Pantheon.Domain.Events;

/// <summary>A group's journey ended: taken in, converted, home again or dead on the road (ADR-016).</summary>
public sealed record MigrationEnded : WorldEvent
{
    /// <summary>The group.</summary>
    public required int GroupId { get; init; }

    /// <summary>The settlement they left.</summary>
    public required int OriginSettlementId { get; init; }

    /// <summary>Where they ended up; their destination when they perished.</summary>
    public required int SettlementId { get; init; }

    /// <summary>How many arrived (or died, when they perished).</summary>
    public required int People { get; init; }

    /// <summary>How it ended.</summary>
    public required MigrationOutcome Outcome { get; init; }

    /// <summary>The god most of them followed on the road, if any.</summary>
    public required int? GodId { get; init; }

    /// <summary>The god followed most where they arrived, if any.</summary>
    public required int? HostGodId { get; init; }
}
