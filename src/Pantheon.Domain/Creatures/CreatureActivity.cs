using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Creatures;

/// <summary>What a Creature is doing now: its chosen action, its target, and how far it has got.</summary>
public sealed class CreatureActivity
{
    /// <summary>The action.</summary>
    public required CreatureAction Action { get; set; }

    /// <summary>The situation in which it chose the action.</summary>
    public required CreatureSituation Situation { get; set; }

    /// <summary>Choosing, travelling, performing or banished.</summary>
    public required CreaturePhase Phase { get; set; }

    /// <summary>The settlement the action concerns, if any.</summary>
    public required int? TargetSettlementId { get; set; }

    /// <summary>Where the action takes place.</summary>
    public required GroundPosition Target { get; set; }

    /// <summary>Waypoints still to walk to, in order; the last is <see cref="Target"/>.</summary>
    public required List<GroundPosition> Path { get; init; }

    /// <summary>Ticks left in the current phase while performing or banished.</summary>
    public required int TicksLeft { get; set; }
}
