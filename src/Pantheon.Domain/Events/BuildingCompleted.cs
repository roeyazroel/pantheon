using Pantheon.Domain.Society;

namespace Pantheon.Domain.Events;

/// <summary>A settlement finished a building (ADR-023).</summary>
public sealed record BuildingCompleted : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>What it built.</summary>
    public required BuildingKind Kind { get; init; }

    /// <summary>The god a temple is raised to; <c>null</c> for any other building.</summary>
    public required int? GodId { get; init; }
}
