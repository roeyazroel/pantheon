using System;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// The attributes of spec §23 that grow with a Creature's level (ADR-030), derived and never stored:
/// intelligence (how fast it learns), resilience (how little harm it takes) and divine affinity (how
/// far it carries its god's influence). Strength and speed are <see cref="CreatureModel"/>'s.
/// </summary>
public static class CreatureAttributes
{
    /// <summary>How fast it learns: its species' learning, growing with level.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The factor on what rewards and punishments teach it.</returns>
    public static float Intelligence(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return CreatureModel.SpeciesOf(rules, creature).Learning * (1f + (rules.Callings.IntelligencePerLevel * (creature.Level - 1)));
    }

    /// <summary>How little harm it takes: its species' toughness, growing with level.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The divisor of the harm that reaches it.</returns>
    public static float Resilience(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return CreatureModel.SpeciesOf(rules, creature).Toughness * (1f + (rules.Callings.ResiliencePerLevel * (creature.Level - 1)));
    }

    /// <summary>How far it carries its god's influence: 1 at level 1, growing with level.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The factor on the reach of the influence it carries.</returns>
    public static float Affinity(DivineRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        return 1f + (rules.CreatureAffinityPerLevel * (creature.Level - 1));
    }
}
