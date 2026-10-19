using System;
using System.Collections.Generic;
using Pantheon.Domain.Culture;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Culture;

/// <summary>
/// What a god does, as the world remembers it, and the traits that emerge (spec §3.2, §11,
/// ADR-024): no good or evil score, only acts that build up and fade, and how they compare.
/// </summary>
public static class ConductModel
{
    /// <summary>What the world notes of an act.</summary>
    public enum Act
    {
        /// <summary>Help to people in need.</summary>
        Help,

        /// <summary>A kind miracle no one needed.</summary>
        Gift,

        /// <summary>Harm to people.</summary>
        Harm,

        /// <summary>Harm to the god's own followers.</summary>
        Cruelty,

        /// <summary>Harm to the followers of gods that wronged it.</summary>
        Vengeance,

        /// <summary>A fire, quake or flood set loose.</summary>
        Upheaval,

        /// <summary>An alliance broken or discord sown.</summary>
        Deceit,

        /// <summary>Peace made or an alliance formed.</summary>
        Faithfulness,

        /// <summary>A war declared.</summary>
        Aggression,
    }

    /// <summary>Adds an act to a god's conduct.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="act">The act.</param>
    /// <param name="weight">How much it weighs.</param>
    public static void Note(WorldState world, int godId, Act act, float weight = 1f)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (GodStanding.Find(world, godId)?.Conduct is not { } conduct || weight <= 0f)
        {
            return;
        }

        switch (act)
        {
            case Act.Help: conduct.Help += weight; break;
            case Act.Gift: conduct.Gifts += weight; break;
            case Act.Harm: conduct.Harm += weight; break;
            case Act.Cruelty: conduct.Cruelty += weight; break;
            case Act.Vengeance: conduct.Vengeance += weight; break;
            case Act.Upheaval: conduct.Upheaval += weight; break;
            case Act.Deceit: conduct.Deceit += weight; break;
            case Act.Faithfulness: conduct.Faithfulness += weight; break;
            case Act.Aggression: conduct.Aggression += weight; break;
            default: throw new ArgumentOutOfRangeException(nameof(act), act, "Unknown act.");
        }
    }

    /// <summary>Every weight of a god's conduct fades by a share: the world forgets slowly.</summary>
    /// <param name="conduct">The conduct.</param>
    /// <param name="share">Share to forget (0..1).</param>
    public static void Fade(GodConduct conduct, float share)
    {
        ArgumentNullException.ThrowIfNull(conduct);
        var keep = 1f - Math.Clamp(share, 0f, 1f);
        conduct.Help *= keep;
        conduct.Gifts *= keep;
        conduct.Harm *= keep;
        conduct.Cruelty *= keep;
        conduct.Vengeance *= keep;
        conduct.Upheaval *= keep;
        conduct.Deceit *= keep;
        conduct.Faithfulness *= keep;
        conduct.Aggression *= keep;
    }

    /// <summary>The traits a god's conduct shows, in the order of <see cref="GodTrait"/>.</summary>
    /// <param name="rules">Culture rules.</param>
    /// <param name="conduct">The conduct.</param>
    /// <returns>The traits.</returns>
    public static List<GodTrait> Traits(CultureRules rules, GodConduct conduct)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(conduct);
        var t = rules.TraitThreshold;
        var traits = new List<GodTrait>();
        void Add(GodTrait trait, bool shows)
        {
            if (shows)
            {
                traits.Add(trait);
            }
        }

        Add(GodTrait.Compassionate, conduct.Help >= t && conduct.Help >= 2f * conduct.Harm);
        Add(GodTrait.Cruel, conduct.Cruelty >= 0.5f * t || (conduct.Harm >= t && conduct.Harm >= 2f * conduct.Help));
        Add(GodTrait.Vengeful, conduct.Vengeance >= t);
        Add(GodTrait.Merciful, conduct.Faithfulness >= t && conduct.Aggression < conduct.Faithfulness);
        Add(GodTrait.Generous, conduct.Gifts >= t && conduct.Help + conduct.Gifts >= 3f * t);
        Add(GodTrait.Deceptive, conduct.Deceit >= 0.6f * t);
        Add(GodTrait.Honest, conduct.Faithfulness >= t && conduct.Deceit < 0.1f * t);
        Add(GodTrait.Chaotic, conduct.Upheaval >= t);
        Add(GodTrait.Ambitious, conduct.Aggression >= t);
        return traits;
    }
}
