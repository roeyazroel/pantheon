namespace Pantheon.Domain.Creatures;

/// <summary>Where a Creature is in carrying out its activity: the states of its state machine.</summary>
public enum CreaturePhase
{
    /// <summary>Choosing what to do next.</summary>
    Deciding,

    /// <summary>Walking to the target.</summary>
    Travelling,

    /// <summary>Doing the action at the target.</summary>
    Performing,

    /// <summary>Defeated and away for a while (spec §28); it returns home.</summary>
    Banished,
}
