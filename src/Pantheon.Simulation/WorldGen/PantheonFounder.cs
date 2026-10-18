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
    /// Creates one god per slot. God <c>i</c> is followed in settlement <c>i</c> (the best sites come
    /// first) with the setup's home belief, and, given Creature rules, its Creature lives there.
    /// When there are fewer settlements than gods, the extra gods start without followers or a
    /// Creature.
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
        var gods = new List<GodState>(setup.Gods.Count);
        var creatures = new List<CreatureState>();
        for (var i = 0; i < setup.Gods.Count; i++)
        {
            var slot = setup.Gods[i];
            var id = FirstGodId + i;
            var hasHome = i < settlements.Count;
            var god = new GodState
            {
                Id = id,
                Name = slot.Name,
                Controller = slot.Controller,
                DivinePower = setup.StartingDivinePower,
                Rank = DivineRank.MinorSpirit,
                CreatureId = hasHome && creatureRules is not null ? id : null,
                Cooldowns = [],
            };
            gods.Add(god);
            if (hasHome && creatureRules is not null)
            {
                creatures.Add(CreatureFounder.Found(creatureRules, seed, god, slot.CreatureName, settlements[i], grid));
            }

            if (hasHome)
            {
                var home = setup.HomeBelief;
                settlements[i].Beliefs.Add(new GodBelief
                {
                    GodId = god.Id,
                    Adherence = home.Adherence,
                    Faith = home.Faith,
                    Love = home.Love,
                    Fear = home.Fear,
                    Doubt = home.Doubt,
                });
            }
        }

        return (gods, creatures);
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
