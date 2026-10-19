namespace Pantheon.Domain.Events;

/// <summary>A settlement's wells ran dry and its people went thirsty (ADR-022).</summary>
public sealed record ThirstStarted : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }
}
