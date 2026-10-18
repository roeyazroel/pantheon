using Pantheon.Domain.Disasters;

namespace Pantheon.Domain.Events;

/// <summary>A disaster destroyed houses or killed people in a settlement (reported once per disaster step).</summary>
public sealed record SettlementDamaged : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>What did it.</summary>
    public required DisasterKind Cause { get; init; }

    /// <summary>Houses destroyed in this step.</summary>
    public required int HousesLost { get; init; }

    /// <summary>People killed in this step.</summary>
    public required int Deaths { get; init; }
}
