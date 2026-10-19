using Pantheon.Domain.Society;

namespace Pantheon.Domain.Events;

/// <summary>A god set what a settlement puts first (spec §29, ADR-023).</summary>
public sealed record PriorityChanged : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god that set it.</summary>
    public required int GodId { get; init; }

    /// <summary>The new priority.</summary>
    public required SettlementPriority Priority { get; init; }
}
