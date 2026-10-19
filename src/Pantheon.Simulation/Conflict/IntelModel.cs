using System;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Conflict;

/// <summary>
/// How much one god knows of another (spec §43, ADR-026), from 0 to 1, derived from the world and
/// never stored: what an ally shares, what treaties let it see, what its own followers among the
/// other's people tell it (spies and defectors), what its Creature saw lately in the other's
/// villages, and, for a Knowledge god, its learning.
/// </summary>
public static class IntelModel
{
    /// <summary>What an observer knows of a subject.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="observerGodId">The god that looks.</param>
    /// <param name="subjectGodId">The god it looks at.</param>
    /// <returns>Intelligence (0..1); 1 of itself.</returns>
    public static float Level(ConflictRules rules, WorldState world, int observerGodId, int subjectGodId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (observerGodId == subjectGodId)
        {
            return 1f;
        }

        var intel = DiplomacyModel.AreAllied(world, observerGodId, subjectGodId) ? rules.AllyIntel : 0f;
        intel += world.Diplomacy.Treaties.Exists(t => t.Binds(observerGodId) && t.Binds(subjectGodId)) ? rules.TreatyIntel : 0f;
        var presence = 0f;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0 && TreatyModel.Leader(settlement) == subjectGodId)
            {
                presence = Math.Max(presence, BeliefModel.Find(settlement, observerGodId)?.Adherence ?? 0f);
            }
        }

        intel += rules.PresenceIntel * presence;
        intel += Scouted(rules, world, observerGodId, subjectGodId) ? rules.ScoutIntel : 0f;
        intel += GodStanding.Find(world, observerGodId) is { } observer && observer.Domains.Contains(GodDomain.Knowledge) ? rules.KnowledgeIntel : 0f;
        return SimMath.Clamp01(intel);
    }

    /// <summary>Whether the observer's Creature was lately in a village the subject leads.</summary>
    private static bool Scouted(ConflictRules rules, WorldState world, int observerGodId, int subjectGodId)
    {
        var since = world.Tick - (long)(rules.ScoutSeconds * SimulationTime.TicksPerSecond);
        foreach (var creature in world.Creatures)
        {
            if (creature.GodId != observerGodId)
            {
                continue;
            }

            foreach (var memory in creature.Memories)
            {
                if (memory.Tick >= since && memory.SettlementId is { } id && world.Settlements.Find(s => s.Id == id) is { } place && TreatyModel.Leader(place) == subjectGodId)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
