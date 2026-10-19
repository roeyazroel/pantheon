namespace Pantheon.Domain.Events;

/// <summary>Storm winds reached a settlement.</summary>
public sealed record StormStruck : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The weather cell.</summary>
    public required int CellId { get; init; }

    /// <summary>Wind at the settlement (0..1).</summary>
    public required float Wind { get; init; }

    /// <summary>Whether it is a hurricane, whose winds tear houses down (ADR-028).</summary>
    public bool Hurricane { get; init; }
}
