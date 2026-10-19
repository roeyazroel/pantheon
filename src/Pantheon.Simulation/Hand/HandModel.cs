using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Hand;

/// <summary>What the hand's deeds do to belief (spec §55, ADR-030). Pure apart from the beliefs it changes.</summary>
public static class HandModel
{
    /// <summary>Danger to a settlement (fire, shaking, flood) from which lifting its people counts as a rescue.</summary>
    public const float Danger = 0.25f;

    /// <summary>A copy of a population's beliefs, for villagers lifted from it.</summary>
    /// <param name="beliefs">The beliefs.</param>
    /// <returns>New beliefs with the same values.</returns>
    public static List<GodBelief> Copy(IReadOnlyList<GodBelief> beliefs)
    {
        ArgumentNullException.ThrowIfNull(beliefs);
        var copy = new List<GodBelief>(beliefs.Count);
        foreach (var belief in beliefs)
        {
            copy.Add(new GodBelief { GodId = belief.GodId, Adherence = belief.Adherence, Faith = belief.Faith, Love = belief.Love, Fear = belief.Fear, Doubt = belief.Doubt });
        }

        return copy;
    }

    /// <summary>A deed's impulse on a population's belief in the god, if they know it.</summary>
    /// <param name="beliefs">The population's beliefs.</param>
    /// <param name="godId">The god.</param>
    /// <param name="impulse">What the deed makes them feel.</param>
    /// <param name="weight">How closely they saw it (0..1).</param>
    public static void Impress(IReadOnlyList<GodBelief> beliefs, int godId, BeliefImpulse impulse, float weight)
    {
        if (BeliefModel.Find(beliefs, godId) is { } belief)
        {
            BeliefModel.ApplyImpulse(belief, impulse, weight, 1f);
        }
    }

    /// <summary>Every settlement within a distance of a deed sees it, the nearer the more.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="point">Where the deed was done.</param>
    /// <param name="radius">How far it is seen, in metres.</param>
    /// <param name="impulse">What it makes them feel.</param>
    public static void Witness(WorldState world, int godId, GroundPosition point, float radius, BeliefImpulse impulse)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var settlement in world.Settlements)
        {
            var weight = Disasters.HazardModel.Falloff(settlement.Position, point, radius);
            if (weight > 0f && settlement.Population > 0)
            {
                Impress(settlement.Beliefs, godId, impulse, weight);
            }
        }
    }
}
