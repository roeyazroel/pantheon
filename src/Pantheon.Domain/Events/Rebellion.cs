namespace Pantheon.Domain.Events;

/// <summary>A settlement rose against a god it fears and resents (spec §8, §3.3, ADR-024).</summary>
public sealed record Rebellion : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god it rose against.</summary>
    public required int GodId { get; init; }

    /// <summary>People who renounced the god.</summary>
    public required int People { get; init; }
}
