using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Creates the gods of a new world, each followed in its own home settlement.</summary>
public static class PantheonFounder
{
    /// <summary>Id of the first god; the others follow in order.</summary>
    public const int FirstGodId = 1;

    /// <summary>
    /// Creates one god per slot, each followed in its own home settlement with the setup's home
    /// belief, and, given Creature rules, with its Creature living there. The first god's home is
    /// the best site (the first settlement); every later god's home is the free settlement furthest
    /// from the homes already taken, so rivals start apart. When there are fewer settlements than
    /// gods, the extra gods start without followers or a Creature.
    /// </summary>
    /// <param name="setup">The pantheon to create.</param>
    /// <param name="settlements">The founded settlements, best site first; their beliefs are set.</param>
    /// <param name="creatureRules">Creature rules, or <c>null</c> for a world without Creatures.</param>
    /// <param name="seed">World seed, for the Creatures' personalities.</param>
    /// <param name="grid">Region grid, to place the Creatures on land.</param>
    /// <returns>The gods and their Creatures, ordered by id.</returns>
    public static (List<GodState> Gods, List<CreatureState> Creatures) Found(
        PantheonSetup setup,
        IReadOnlyList<SettlementState> settlements,
        CreatureRules? creatureRules,
        WorldSeed seed,
        RegionGrid grid)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentNullException.ThrowIfNull(grid);
        var homes = ChooseHomes(settlements, setup.Gods.Count);
        var gods = new List<GodState>(setup.Gods.Count);
        var creatures = new List<CreatureState>();
        for (var i = 0; i < setup.Gods.Count; i++)
        {
            var slot = setup.Gods[i];
            var id = FirstGodId + i;
            var home = i < homes.Count ? homes[i] : null;
            var god = new GodState
            {
                Id = id,
                Name = slot.Name,
                Controller = slot.Controller,
                Personality = slot.Personality,
                DivinePower = setup.StartingDivinePower,
                Rank = DivineRank.MinorSpirit,
                DominantSinceTick = null,
                CreatureId = home is not null && creatureRules is not null ? id : null,
                Cooldowns = [],
            };
            gods.Add(god);
            if (home is null)
            {
                continue;
            }

            if (creatureRules is not null)
            {
                creatures.Add(CreatureFounder.Found(creatureRules, seed, god, slot.CreatureName, home, grid));
            }

            var belief = setup.HomeBelief;
            home.Beliefs.Add(new GodBelief
            {
                GodId = god.Id,
                Adherence = belief.Adherence,
                Faith = belief.Faith,
                Love = belief.Love,
                Fear = belief.Fear,
                Doubt = belief.Doubt,
            });
        }

        return (gods, creatures);
    }

    /// <summary>The first settlement, then each time the free settlement furthest from the homes already chosen.</summary>
    /// <param name="settlements">Settlements, best site first.</param>
    /// <param name="count">Homes wanted.</param>
    /// <returns>Up to <paramref name="count"/> homes, in god order.</returns>
    public static List<SettlementState> ChooseHomes(IReadOnlyList<SettlementState> settlements, int count)
    {
        ArgumentNullException.ThrowIfNull(settlements);
        var homes = new List<SettlementState>();
        while (homes.Count < count && homes.Count < settlements.Count)
        {
            SettlementState? best = null;
            var bestDistance = -1f;
            foreach (var settlement in settlements)
            {
                if (homes.Contains(settlement))
                {
                    continue;
                }

                var nearest = homes.Count == 0 ? 0f : float.PositiveInfinity;
                foreach (var home in homes)
                {
                    var dx = settlement.Position.X - home.Position.X;
                    var dz = settlement.Position.Z - home.Position.Z;
                    nearest = MathF.Min(nearest, MathF.Sqrt((dx * dx) + (dz * dz)));
                }

                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = settlement;
                }
            }

            homes.Add(best!);
        }

        return homes;
    }

    /// <summary>Throws when the setup cannot create a playable pantheon.</summary>
    /// <param name="setup">Setup to check.</param>
    /// <exception cref="ArgumentException">The setup is invalid; the message lists every problem.</exception>
    public static void Validate(PantheonSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var errors = new Validation.RuleErrors();
        errors.RequireNonNegative(setup.StartingDivinePower, nameof(setup.StartingDivinePower));
        errors.RequireShare(setup.HomeBelief.Adherence, "HomeBelief.Adherence");
        errors.RequireShare(setup.HomeBelief.Faith, "HomeBelief.Faith");
        errors.RequireShare(setup.HomeBelief.Love, "HomeBelief.Love");
        errors.RequireShare(setup.HomeBelief.Fear, "HomeBelief.Fear");
        errors.RequireShare(setup.HomeBelief.Doubt, "HomeBelief.Doubt");
        if (setup.Gods.Count == 0)
        {
            errors.Add("Gods must list at least one god.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in setup.Gods)
        {
            errors.RequireDefined(slot.Controller, "Gods.Controller");
            errors.RequireDefined(slot.Personality, "Gods.Personality");
            if ((slot.Controller == GodController.Ai) != (slot.Personality != GodPersonality.None))
            {
                errors.Add($"God '{slot.Name}' must have a personality if and only if the AI controls it.");
            }

            if (string.IsNullOrWhiteSpace(slot.CreatureName))
            {
                errors.Add($"God '{slot.Name}' needs a CreatureName.");
            }

            if (string.IsNullOrWhiteSpace(slot.Name) || !names.Add(slot.Name))
            {
                errors.Add($"God name '{slot.Name}' is blank or used more than once.");
            }
        }

        errors.ThrowIfAny("pantheon setup", nameof(setup));
    }
}
