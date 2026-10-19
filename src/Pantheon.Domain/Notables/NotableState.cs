using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Notables;

/// <summary>What kind of individual a notable is (spec §80's Tier 3, ADR-032).</summary>
public enum NotableKind
{
    /// <summary>A prophet: walks from village to village preaching their god, and carries its influence.</summary>
    Prophet,

    /// <summary>A village's elder: keeps its people's faith in their god, until they die or turn.</summary>
    Elder,
}

/// <summary>What a notable remembers doing or living through.</summary>
public enum NotableDeed
{
    /// <summary>They rose: a prophet called or anointed, an elder chosen.</summary>
    Rose,

    /// <summary>They preached in a village and won people to their god.</summary>
    Preached,

    /// <summary>They lived through danger in a village.</summary>
    Endured,

    /// <summary>An elder turned from their god.</summary>
    Turned,
}

/// <summary>One thing a notable remembers (spec §80's individual memory).</summary>
public sealed class NotableMemory
{
    /// <summary>When.</summary>
    public required long Tick { get; init; }

    /// <summary>What.</summary>
    public required NotableDeed Deed { get; init; }

    /// <summary>Where, if in a village.</summary>
    public required int? SettlementId { get; init; }

    /// <summary>How many people it concerned, if any.</summary>
    public required int Count { get; init; }
}

/// <summary>
/// One individual simulated in full (spec §80's Tier 3, ADR-032): a prophet or an elder, with a
/// name, a god, a place, a way across the land, a lifespan and a memory of their own.
/// </summary>
public sealed class NotableState
{
    /// <summary>Unique id, from 1.</summary>
    public required int Id { get; init; }

    /// <summary>A prophet or an elder.</summary>
    public required NotableKind Kind { get; init; }

    /// <summary>Their name.</summary>
    public required string Name { get; init; }

    /// <summary>The god they serve (an elder may turn to another).</summary>
    public required int GodId { get; set; }

    /// <summary>The village they are in, or last left: an elder's home.</summary>
    public required int SettlementId { get; set; }

    /// <summary>Where they stand.</summary>
    public required GroundPosition Position { get; set; }

    /// <summary>The village a prophet walks to; <c>null</c> while preaching, and for an elder.</summary>
    public int? TargetSettlementId { get; set; }

    /// <summary>The way still to walk.</summary>
    public List<GroundPosition> Path { get; init; } = [];

    /// <summary>Ticks of preaching left, while in a village.</summary>
    public int TicksLeft { get; set; }

    /// <summary>When they rose.</summary>
    public required long BornTick { get; init; }

    /// <summary>When age takes them.</summary>
    public required long DiesAtTick { get; init; }

    /// <summary>What they remember, oldest first.</summary>
    public List<NotableMemory> Memories { get; init; } = [];
}
