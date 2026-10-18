using Pantheon.Domain.World;

namespace Pantheon.Domain.Settlements;

/// <summary>
/// Aggregate (statistical-tier) state of one settlement. Individual villagers are not stored;
/// see ADR-004.
/// </summary>
public sealed class SettlementState
{
    /// <summary>Unique settlement id within the world.</summary>
    public required int Id { get; init; }

    /// <summary>Display name of the settlement.</summary>
    public required string Name { get; init; }

    /// <summary>Id of the region the settlement stands in.</summary>
    public required int RegionId { get; init; }

    /// <summary>Position of the settlement centre on the ground plane.</summary>
    public required GroundPosition Position { get; init; }

    /// <summary>Number of inhabitants.</summary>
    public required int Population { get; set; }

    /// <summary>Stored food in abstract food units (rates are defined by <see cref="SettlementRules"/>).</summary>
    public required float Food { get; set; }

    /// <summary>Number of people the settlement's dwellings can house.</summary>
    public required int Housing { get; set; }
}
