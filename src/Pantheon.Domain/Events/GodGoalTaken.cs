using Pantheon.Domain.Gods;

namespace Pantheon.Domain.Events;

/// <summary>An AI god set itself a new goal in its major plan (spec §47, §87, ADR-036).</summary>
public sealed record GodGoalTaken : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>What it set out to do.</summary>
    public required GodGoalKind Kind { get; init; }

    /// <summary>The village it concerns, if any.</summary>
    public int? SettlementId { get; init; }

    /// <summary>The god it concerns, if any.</summary>
    public int? OtherGodId { get; init; }
}
