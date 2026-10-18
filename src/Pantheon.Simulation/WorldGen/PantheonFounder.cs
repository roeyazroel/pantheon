using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Creates the gods of a new world, each followed in its own home settlement.</summary>
public static class PantheonFounder
{
    /// <summary>Id of the first god; the others follow in order.</summary>
    public const int FirstGodId = 1;

    /// <summary>
    /// Creates one god per slot. God <c>i</c> is followed in settlement <c>i</c> (the best sites come
    /// first) with the setup's home belief. When there are fewer settlements than gods, the extra
    /// gods start without followers.
    /// </summary>
    /// <param name="setup">The pantheon to create.</param>
    /// <param name="settlements">The founded settlements, best site first; their beliefs are set.</param>
    /// <returns>The gods, ordered by id.</returns>
    public static List<GodState> Found(PantheonSetup setup, IReadOnlyList<SettlementState> settlements)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(settlements);
        var gods = new List<GodState>(setup.Gods.Count);
        for (var i = 0; i < setup.Gods.Count; i++)
        {
            var slot = setup.Gods[i];
            var god = new GodState
            {
                Id = FirstGodId + i,
                Name = slot.Name,
                Controller = slot.Controller,
                DivinePower = setup.StartingDivinePower,
                Rank = DivineRank.MinorSpirit,
                Cooldowns = [],
            };
            gods.Add(god);
            if (i < settlements.Count)
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

        return gods;
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
            if (string.IsNullOrWhiteSpace(slot.Name) || !names.Add(slot.Name))
            {
                errors.Add($"God name '{slot.Name}' is blank or used more than once.");
            }
        }

        errors.ThrowIfAny("pantheon setup", nameof(setup));
    }
}
