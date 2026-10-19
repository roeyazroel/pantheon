using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// The paths a Creature takes up (spec §24, ADR-030): its deeds, fights and rewards call it towards
/// each, and at each calling level it takes up the one it has walked most. Pure apart from the
/// Creature it changes.
/// </summary>
public static class CreatureCalling
{
    /// <summary>How far a Creature has been called to a path.</summary>
    /// <param name="callings">Its callings.</param>
    /// <param name="path">The path.</param>
    /// <returns>The calling.</returns>
    public static float Get(CreatureCallings callings, CreatureSpecialisation path)
    {
        ArgumentNullException.ThrowIfNull(callings);
        return path switch
        {
            CreatureSpecialisation.Guardian => callings.Guardian,
            CreatureSpecialisation.Warrior => callings.Warrior,
            CreatureSpecialisation.Destroyer => callings.Destroyer,
            CreatureSpecialisation.Healer => callings.Healer,
            CreatureSpecialisation.NatureKeeper => callings.NatureKeeper,
            CreatureSpecialisation.Missionary => callings.Missionary,
            CreatureSpecialisation.Trickster => callings.Trickster,
            CreatureSpecialisation.Commander => callings.Commander,
        };
    }

    /// <summary>Calls a Creature further towards a path.</summary>
    /// <param name="callings">Its callings.</param>
    /// <param name="path">The path.</param>
    /// <param name="weight">How far.</param>
    public static void Add(CreatureCallings callings, CreatureSpecialisation path, float weight)
    {
        ArgumentNullException.ThrowIfNull(callings);
        switch (path)
        {
            case CreatureSpecialisation.Guardian: callings.Guardian += weight; break;
            case CreatureSpecialisation.Warrior: callings.Warrior += weight; break;
            case CreatureSpecialisation.Destroyer: callings.Destroyer += weight; break;
            case CreatureSpecialisation.Healer: callings.Healer += weight; break;
            case CreatureSpecialisation.NatureKeeper: callings.NatureKeeper += weight; break;
            case CreatureSpecialisation.Missionary: callings.Missionary += weight; break;
            case CreatureSpecialisation.Trickster: callings.Trickster += weight; break;
            case CreatureSpecialisation.Commander: callings.Commander += weight; break;
            default: throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown path.");
        }
    }

    /// <summary>A deed done calls the Creature towards the paths it belongs to.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="action">The deed.</param>
    /// <param name="scale">How much of each deed's weight counts.</param>
    public static void Deed(CallingRules rules, CreatureState creature, CreatureAction action, float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        foreach (var deed in rules.Deeds)
        {
            if (deed.Action == action)
            {
                Add(creature.Callings, deed.Specialisation, deed.Weight * scale);
            }
        }
    }

    /// <summary>Whether a Creature has taken up a path.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="path">The path.</param>
    /// <returns><c>true</c> when it has.</returns>
    public static bool Has(CreatureState creature, CreatureSpecialisation path)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.Specialisations.Contains(path);
    }

    /// <summary>A Creature takes up a path at every calling level it has reached and not yet answered.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="tick">The current tick.</param>
    /// <param name="events">Receives <see cref="CreatureSpecialised"/>.</param>
    public static void Grow(CallingRules rules, CreatureState creature, long tick, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(events);
        var due = 0;
        foreach (var level in rules.Levels)
        {
            due += creature.Level >= level ? 1 : 0;
        }

        while (creature.Specialisations.Count < Math.Min(due, Enum.GetValues<CreatureSpecialisation>().Length))
        {
            var path = Next(creature);
            creature.Specialisations.Add(path);
            events.Add(new CreatureSpecialised { Tick = tick, CreatureId = creature.Id, GodId = creature.GodId, Specialisation = path });
        }
    }

    /// <summary>
    /// The path a Creature takes up next: the one it has been called to most, of those it has not yet
    /// taken; with no calling at all, the one its strongest trait leans to.
    /// </summary>
    /// <param name="creature">The Creature.</param>
    /// <returns>The path.</returns>
    public static CreatureSpecialisation Next(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        CreatureSpecialisation? best = null;
        var bestCalling = 0f;
        foreach (var path in Enum.GetValues<CreatureSpecialisation>())
        {
            var calling = Get(creature.Callings, path);
            if (!Has(creature, path) && calling > bestCalling)
            {
                (best, bestCalling) = (path, calling);
            }
        }

        return best ?? Leaning(creature);
    }

    /// <summary>The god's Creature, when it has taken up a path, is not banished and stands within reach of a point.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="path">The path.</param>
    /// <param name="point">The point.</param>
    /// <param name="radius">The reach in metres.</param>
    /// <returns>The Creature, or <c>null</c>.</returns>
    public static CreatureState? Near(WorldState world, int godId, CreatureSpecialisation path, GroundPosition point, float radius)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var creature in world.Creatures)
        {
            if (creature.GodId == godId && Has(creature, path) && creature.Activity.Phase != CreaturePhase.Banished)
            {
                var (dx, dz) = (creature.Position.X - point.X, creature.Position.Z - point.Z);
                if ((dx * dx) + (dz * dz) <= radius * radius)
                {
                    return creature;
                }
            }
        }

        return null;
    }

    /// <summary>The path a Creature's strongest trait leans to, of those it has not taken; the first free one otherwise.</summary>
    private static CreatureSpecialisation Leaning(CreatureState creature)
    {
        var p = creature.Personality;
        (float Trait, CreatureSpecialisation Path)[] leanings =
        [
            (p.Kindness, CreatureSpecialisation.Healer),
            (p.Aggression, CreatureSpecialisation.Warrior),
            (p.Curiosity, CreatureSpecialisation.NatureKeeper),
            (p.Laziness, CreatureSpecialisation.Guardian),
            (p.Protectiveness, CreatureSpecialisation.Guardian),
            (p.Courage, CreatureSpecialisation.Commander),
            (p.Playfulness, CreatureSpecialisation.Missionary),
            (p.Jealousy, CreatureSpecialisation.Trickster),
        ];
        Array.Sort(leanings, (a, b) => b.Trait != a.Trait ? b.Trait.CompareTo(a.Trait) : a.Path.CompareTo(b.Path));
        foreach (var (_, path) in leanings)
        {
            if (!Has(creature, path))
            {
                return path;
            }
        }

        foreach (var path in Enum.GetValues<CreatureSpecialisation>())
        {
            if (!Has(creature, path))
            {
                return path;
            }
        }

        return CreatureSpecialisation.Guardian;
    }
}
