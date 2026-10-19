namespace Pantheon.Domain.Events;

/// <summary>An Old Faith ended: its people returned to the god or fell away from it (ADR-021).</summary>
public sealed record SchismEnded : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god they had broken away from.</summary>
    public required int GodId { get; init; }

    /// <summary>Whether more of them returned to the god than fell into unbelief.</summary>
    public required bool Reconciled { get; init; }
}
