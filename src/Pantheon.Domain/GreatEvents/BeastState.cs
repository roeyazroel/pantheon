using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.GreatEvents;

/// <summary>Whether the ancient beast sleeps, ravages or is dead (ADR-035).</summary>
public enum BeastPhase
{
    /// <summary>Asleep in its lair.</summary>
    Sleeping,

    /// <summary>Awake, walking to a village or ravaging it.</summary>
    Awake,

    /// <summary>Going back to its lair to sleep.</summary>
    Returning,

    /// <summary>Slain, for good.</summary>
    Slain,
}

/// <summary>
/// The world's ancient beast (spec §52's ancient monster, ADR-035): its lair, where it is, what it
/// hunts, its wounds, and the harm each god has done it.
/// </summary>
public sealed class BeastState
{
    /// <summary>Its name, for example "Gharos the Ancient".</summary>
    public required string Name { get; init; }

    /// <summary>The region it sleeps in.</summary>
    public required int LairRegionId { get; init; }

    /// <summary>Asleep, awake, going home or slain.</summary>
    public BeastPhase Phase { get; set; }

    /// <summary>Where it is.</summary>
    public required GroundPosition Position { get; set; }

    /// <summary>Its health, from <see cref="MaxHealth"/> down to 0.</summary>
    public required float Health { get; set; }

    /// <summary>Its health when whole.</summary>
    public required float MaxHealth { get; init; }

    /// <summary>The village it hunts or ravages, if any.</summary>
    public int? TargetSettlementId { get; set; }

    /// <summary>The way it walks.</summary>
    public List<GroundPosition> Path { get; init; } = [];

    /// <summary>Ticks it has ravaged its village so far.</summary>
    public int RavagedTicks { get; set; }

    /// <summary>The village it ravaged last, which it leaves for another.</summary>
    public int? LastSettlementId { get; set; }

    /// <summary>When it last woke.</summary>
    public long WokeTick { get; set; }

    /// <summary>The harm each god has done it while awake, by god id: their share of the glory when it falls.</summary>
    public Dictionary<int, float> HarmByGod { get; init; } = [];
}
