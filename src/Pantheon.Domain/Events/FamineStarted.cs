namespace Pantheon.Domain.Events;

/// <summary>A settlement's stores ran out and its people went hungry.</summary>
public sealed record FamineStarted : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }
}
