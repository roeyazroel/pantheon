namespace Pantheon.Domain.Events;

/// <summary>Followers of a god broke away to keep its Old Faith (spec §34, ADR-021).</summary>
public sealed record SchismStarted : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god they broke away from.</summary>
    public required int GodId { get; init; }

    /// <summary>How many people broke away.</summary>
    public required int People { get; init; }
}
