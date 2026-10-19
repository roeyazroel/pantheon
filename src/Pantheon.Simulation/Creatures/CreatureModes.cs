using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Creatures;

/// <summary>The top level of a Creature's hierarchical state machine (spec §86, ADR-047).</summary>
public enum CreatureMode
{
    /// <summary>Free: deciding, travelling or performing an action of its own, which its behaviour tree runs.</summary>
    Free,

    /// <summary>Engaged with another Creature: fighting it, or playing with it.</summary>
    Engaged,

    /// <summary>In its god's hand.</summary>
    Held,

    /// <summary>Banished, away from the world until it returns.</summary>
    Banished,
}

/// <summary>
/// A Creature's hierarchical state machine (spec §86, ADR-047): the mode it is in, and within the
/// free mode the phase of its action (deciding, travelling, performing). The Creature system steps
/// each mode its own way; the behaviour tree runs the free mode's actions.
/// </summary>
public static class CreatureModes
{
    /// <summary>The mode a Creature is in.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The mode.</returns>
    public static CreatureMode ModeOf(WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        var activity = creature.Activity;
        return CreatureModel.IsHeld(world, creature) ? CreatureMode.Held
            : activity.Phase == CreaturePhase.Banished ? CreatureMode.Banished
            : CreatureCombat.IsFighting(creature) || (activity.Phase == CreaturePhase.Performing && activity.Action == CreatureAction.Befriend) ? CreatureMode.Engaged
            : CreatureMode.Free;
    }

    /// <summary>The state's path, for the debug panel.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example "Free › Travelling".</returns>
    public static string Describe(WorldState world, CreatureState creature)
    {
        var mode = ModeOf(world, creature);
        return mode switch
        {
            CreatureMode.Free => "Free › " + creature.Activity.Phase,
            CreatureMode.Engaged => CreatureCombat.IsFighting(creature) ? "Engaged › Fighting" : "Engaged › Playing",
            CreatureMode.Held => "Held",
            CreatureMode.Banished => "Banished",
        };
    }
}
