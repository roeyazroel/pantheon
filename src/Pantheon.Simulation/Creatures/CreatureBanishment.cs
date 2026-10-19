using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Creatures;

/// <summary>Defeat's absence (spec §28): a banished Creature is away for <c>BanishedSeconds</c>, then returns home.</summary>
public static class CreatureBanishment
{
    /// <summary>Sends the Creature away: it stops what it was doing and is gone for the banishment.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="creature">The Creature.</param>
    public static void Banish(CreatureRules rules, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creature);
        var activity = creature.Activity;
        activity.Phase = CreaturePhase.Banished;
        activity.Path.Clear();
        activity.TargetSettlementId = null;
        activity.TargetCreatureId = null;
        activity.TicksLeft = Math.Max(1, (int)MathF.Round(rules.BanishedSeconds * SimulationTime.TicksPerSecond));
    }
}
