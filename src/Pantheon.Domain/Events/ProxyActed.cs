using Pantheon.Domain.Conflict;

namespace Pantheon.Domain.Events;

/// <summary>A god acted through mortals against another god (spec §45, ADR-026); only the actor knows unless it is exposed.</summary>
public sealed record ProxyActed : WorldEvent
{
    /// <summary>The acting god.</summary>
    public required int GodId { get; init; }

    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god worked against.</summary>
    public required int TargetGodId { get; init; }

    /// <summary>What it did.</summary>
    public required ProxyKind Kind { get; init; }
}
