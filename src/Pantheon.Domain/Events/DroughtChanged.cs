namespace Pantheon.Domain.Events;

/// <summary>A settlement's farmland fell into drought, or recovered from it.</summary>
public sealed record DroughtChanged : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary><c>true</c> when the drought began, <c>false</c> when it ended.</summary>
    public required bool Started { get; init; }

    /// <summary>The drought now (0..1).</summary>
    public required float Drought { get; init; }
}
