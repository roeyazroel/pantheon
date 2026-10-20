using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Creatures;

/// <summary>Why a Creature cannot make the choice its own mind made, or <see cref="None"/> when it does.</summary>
public enum WillRejection
{
    /// <summary>It does what it chose.</summary>
    None,

    /// <summary>No Creature has the id.</summary>
    UnknownCreature,

    /// <summary>It is fighting or playing, held in a hand or banished, and cannot turn to anything else.</summary>
    NotFree,

    /// <summary>It is on its god's errand, which its own mind does not overrule.</summary>
    Obeying,

    /// <summary>The choice is not among what it could do now, or no longer.</summary>
    NotAnOption,

    /// <summary>It cannot get there over land.</summary>
    Unreachable,
}

/// <summary>
/// A Creature's own will at the moments that matter to it (ADR-072): when it meets another Creature, ends a
/// fight, is rewarded or punished, grows, hungers or tires, or its people are struck, its mind may choose
/// among what its brain would weigh then, and the choice is carried out as the brain would carry out its
/// own. It never overrules its god's errand, and it cannot leave a fight or a hand. Like a command, it is
/// taken in between ticks; any randomness (where to wander or flee) is drawn from the seed and the tick.
/// </summary>
public sealed class CreatureWill
{
    /// <summary>The most choices a Creature's mind is given.</summary>
    public const int MaxOptions = 8;

    private readonly CreatureBrain _brain;

    /// <summary>Creates the will.</summary>
    /// <param name="brain">The brain whose options it chooses among and which carries the choice out.</param>
    public CreatureWill(CreatureBrain brain)
    {
        ArgumentNullException.ThrowIfNull(brain);
        _brain = brain;
    }

    /// <summary>Whether the Creature may choose for itself now, or why not.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns><see cref="WillRejection.None"/> when it may.</returns>
    public static WillRejection CanChoose(WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        if (CreatureModes.ModeOf(world, creature) != CreatureMode.Free)
        {
            return WillRejection.NotFree;
        }

        var activity = creature.Activity;
        return activity.Situation == CreatureSituation.Commanded && activity.Phase is CreaturePhase.Travelling or CreaturePhase.Performing
            ? WillRejection.Obeying
            : WillRejection.None;
    }

    /// <summary>What the Creature could choose now, the brain's likeliest first, each different: none when it may not choose.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>At most <see cref="MaxOptions"/> options.</returns>
    public List<CreatureOption> Options(WorldState world, CreatureState creature)
    {
        var options = new List<CreatureOption>();
        if (CanChoose(world, creature) != WillRejection.None)
        {
            return options;
        }

        foreach (var option in _brain.ListOptions(world, creature))
        {
            if (option.Action != CreatureAction.GoTo && !options.Exists(o => Same(o, option)))
            {
                options.Add(option);
                if (options.Count == MaxOptions)
                {
                    break;
                }
            }
        }

        return options;
    }

    /// <summary>Carries out the Creature's own choice, if it is still one of its options.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creatureId">The Creature.</param>
    /// <param name="action">What it chose to do.</param>
    /// <param name="settlementId">The village it chose to do it at, if any.</param>
    /// <param name="otherCreatureId">The Creature it chose to do it with, if any.</param>
    /// <returns>Why it could not, or <see cref="WillRejection.None"/>.</returns>
    public WillRejection Choose(WorldState world, int creatureId, CreatureAction action, int? settlementId, int? otherCreatureId)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Creatures.Find(c => c.Id == creatureId) is not { } creature)
        {
            return WillRejection.UnknownCreature;
        }

        if (CanChoose(world, creature) is not WillRejection.None and var refused)
        {
            return refused;
        }

        var wanted = new CreatureOption(default, action, settlementId, default, 0f, otherCreatureId);
        var options = Options(world, creature);
        var index = options.FindIndex(o => Same(o, wanted));
        if (index < 0)
        {
            return WillRejection.NotAnOption;
        }

        var option = options[index];
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, "creature-will", (world.Tick * 64) + creature.Id));
        return _brain.TryStart(world, creature, option, random) ? WillRejection.None : WillRejection.Unreachable;
    }

    /// <summary>Whether two options are the same choice: the same deed, at the same village, with the same Creature.</summary>
    private static bool Same(CreatureOption a, CreatureOption b) =>
        a.Action == b.Action && a.SettlementId == b.SettlementId && a.CreatureId == b.CreatureId;
}
