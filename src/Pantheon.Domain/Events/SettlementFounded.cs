namespace Pantheon.Domain.Events;

/// <summary>Settlers founded a new village (spec §29's "expand territory", ADR-023).</summary>
public sealed record SettlementFounded : WorldEvent
{
    /// <summary>The new settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The settlement the settlers came from.</summary>
    public required int FromSettlementId { get; init; }

    /// <summary>How many came.</summary>
    public required int Settlers { get; init; }
}
