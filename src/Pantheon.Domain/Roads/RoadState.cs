using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Roads;

/// <summary>
/// A road between two villages (spec §29, ADR-033), built from one end along the way the
/// pathfinder found. Once whole it speeds walkers, carries trade and food, and disasters wear it.
/// </summary>
public sealed class RoadState
{
    /// <summary>Unique id, below <see cref="WorldState.NextRoadId"/>.</summary>
    public required int Id { get; init; }

    /// <summary>The village that builds it.</summary>
    public required int FromSettlementId { get; init; }

    /// <summary>The village it leads to.</summary>
    public required int ToSettlementId { get; init; }

    /// <summary>The way, from the first village to the second.</summary>
    public List<GroundPosition> Path { get; init; } = [];

    /// <summary>Its length in metres along <see cref="Path"/>.</summary>
    public required float Length { get; init; }

    /// <summary>The metres built so far, from the first village; it is whole at <see cref="Length"/>.</summary>
    public float Built { get; set; }

    /// <summary>Wear from floods, fires and rubble (0..1); a road worn to the rules' limit is cut until mended.</summary>
    public float Wear { get; set; }
}
