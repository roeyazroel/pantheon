using System.Collections.Generic;

namespace Pantheon.Domain.Creatures;

/// <summary>
/// How one Creature regards another (spec §27, ADR-017): friendship, fear, rivalry, respect and
/// trust, each from 0 to 1, and what it remembers of their meetings. Each Creature keeps its own
/// view, so the two sides of a pair can differ.
/// </summary>
public sealed class CreatureRelation
{
    /// <summary>The Creature it regards.</summary>
    public required int OtherCreatureId { get; init; }

    /// <summary>How much it likes the other: from playing together, being spared and allied gods.</summary>
    public required float Friendship { get; set; }

    /// <summary>How much it fears the other: from defeats and flight.</summary>
    public required float Fear { get; set; }

    /// <summary>How much it wants to beat the other: from fights, attacks on its god's people and war.</summary>
    public required float Rivalry { get; set; }

    /// <summary>How highly it rates the other: from being beaten or spared by it, or a hard fight.</summary>
    public required float Respect { get; set; }

    /// <summary>How much it trusts the other not to turn on it: from play and mercy; betrayal breaks it.</summary>
    public required float Trust { get; set; }

    /// <summary>Tick of their last meeting, or -1 before they have met.</summary>
    public required long LastMetTick { get; set; }

    /// <summary>What it remembers of the other, oldest first.</summary>
    public required List<CreatureEncounter> Encounters { get; init; }
}

/// <summary>Something that happened between two Creatures, as one of them remembers it.</summary>
public sealed class CreatureEncounter
{
    /// <summary>Tick at which it happened.</summary>
    public required long Tick { get; init; }

    /// <summary>What happened.</summary>
    public required EncounterKind Kind { get; init; }
}

/// <summary>What a Creature remembers of another (spec §27: "Creatures remember each other").</summary>
public enum EncounterKind
{
    /// <summary>They met and went their ways.</summary>
    Met,

    /// <summary>They greeted each other.</summary>
    Greeted,

    /// <summary>They played together.</summary>
    Played,

    /// <summary>It beat the other, who was banished.</summary>
    Won,

    /// <summary>The other beat it, and it was banished.</summary>
    Lost,

    /// <summary>It beat the other and let it go.</summary>
    Spared,

    /// <summary>The other beat it and let it go.</summary>
    WasSpared,

    /// <summary>They fought until neither could win.</summary>
    Drew,

    /// <summary>It fled from the other.</summary>
    Fled,

    /// <summary>The other fled from it.</summary>
    ChasedOff,

    /// <summary>The other, a friend, attacked it.</summary>
    Betrayed,

    /// <summary>The other attacked its god's people.</summary>
    AttackedOurPeople,
}
