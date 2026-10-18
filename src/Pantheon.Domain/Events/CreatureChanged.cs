namespace Pantheon.Domain.Events;

/// <summary>What changed about a Creature.</summary>
public enum CreatureChange
{
    /// <summary>It reached a new level.</summary>
    LevelledUp,

    /// <summary>It was defeated and banished for a while.</summary>
    Banished,

    /// <summary>It came back from banishment.</summary>
    Returned,

    /// <summary>Its god rewarded it.</summary>
    Rewarded,

    /// <summary>Its god punished it.</summary>
    Punished,
}

/// <summary>A Creature levelled up, was banished, came back, or was rewarded or punished.</summary>
public sealed record CreatureChanged : WorldEvent
{
    /// <summary>The Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>What changed.</summary>
    public required CreatureChange Change { get; init; }
}
