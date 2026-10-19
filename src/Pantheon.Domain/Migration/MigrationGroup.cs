using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Migration;

/// <summary>
/// People on the road from one settlement to another (spec §82, ADR-016): how many, what they
/// carry and believe, how they fare and the way still to walk.
/// </summary>
public sealed class MigrationGroup
{
    /// <summary>Unique id.</summary>
    public required int Id { get; init; }

    /// <summary>The settlement they left.</summary>
    public required int OriginSettlementId { get; init; }

    /// <summary>The settlement they are heading for (their origin when they go home).</summary>
    public required int DestinationSettlementId { get; set; }

    /// <summary>Why they left.</summary>
    public required MigrationCause Cause { get; init; }

    /// <summary>How many they are.</summary>
    public required int Population { get; set; }

    /// <summary>The food they carry.</summary>
    public required float Food { get; set; }

    /// <summary>Their health (0..1).</summary>
    public required float Health { get; set; }

    /// <summary>Where they are.</summary>
    public required GroundPosition Position { get; set; }

    /// <summary>Waypoints still to walk, in order; the last is their destination.</summary>
    public required List<GroundPosition> Path { get; init; }

    /// <summary>Their beliefs, as a settlement holds them: shares of the group and its feelings for each god.</summary>
    public required List<GodBelief> Beliefs { get; init; }

    /// <summary>Settlements that turned them away, so they do not try those again.</summary>
    public required List<int> TurnedAwayFrom { get; init; }

    /// <summary>The tick they set out.</summary>
    public required long DepartedTick { get; init; }
}
