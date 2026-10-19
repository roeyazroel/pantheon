namespace Pantheon.Domain.Events;

/// <summary>What came of two Creatures meeting (ADR-017).</summary>
public enum MeetingOutcome
{
    /// <summary>They went their ways.</summary>
    PassedBy,

    /// <summary>One greeted the other.</summary>
    Greeted,

    /// <summary>They played together.</summary>
    Played,

    /// <summary>One fled from the other.</summary>
    Fled,

    /// <summary>One attacked the other, and they fight.</summary>
    Fought,
}

/// <summary>Two Creatures met (spec §27).</summary>
public sealed record CreaturesMet : WorldEvent
{
    /// <summary>The Creature that acted: the one that greeted, fled or attacked; the lower id otherwise.</summary>
    public required int CreatureId { get; init; }

    /// <summary>The other Creature.</summary>
    public required int OtherCreatureId { get; init; }

    /// <summary>What came of it.</summary>
    public required MeetingOutcome Outcome { get; init; }

    /// <summary>Whether the attacked Creature counted its attacker as a friend.</summary>
    public required bool Betrayal { get; init; }

    /// <summary>Whether their gods are at war: friends playing, or refusing to fight, then defy their gods.</summary>
    public required bool GodsAtWar { get; init; }

    /// <summary>Whether each counted the other as a friend when they met.</summary>
    public required bool Friends { get; init; }
}
