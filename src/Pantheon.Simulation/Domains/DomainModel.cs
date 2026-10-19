using System;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Simulation.Domains;

/// <summary>What a god's domains allow and change (spec §12, ADR-021). Pure functions of the rules and the world.</summary>
public static class DomainModel
{
    /// <summary>How many domains a god of a rank may hold.</summary>
    /// <param name="rules">Domain rules.</param>
    /// <param name="rank">The rank.</param>
    /// <returns>The number of domains; at least the first slot's.</returns>
    public static int SlotsFor(DomainRules rules, DivineRank rank)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var slots = 0;
        foreach (var slot in rules.Slots)
        {
            if (slot.Rank <= rank)
            {
                slots = Math.Max(slots, slot.Domains);
            }
        }

        return slots;
    }

    /// <summary>How many more domains a god may take up now.</summary>
    /// <param name="rules">Domain rules.</param>
    /// <param name="god">The god.</param>
    /// <returns>Free slots (0 and up).</returns>
    public static int FreeSlots(DomainRules rules, GodState god)
    {
        ArgumentNullException.ThrowIfNull(god);
        return Math.Max(0, SlotsFor(rules, god.Rank) - god.Domains.Count);
    }

    /// <summary>Whether a god holds a domain.</summary>
    /// <param name="god">The god, if any.</param>
    /// <param name="domain">The domain.</param>
    /// <returns><c>true</c> when it does.</returns>
    public static bool Holds(GodState? god, GodDomain domain) => god is not null && god.Domains.Contains(domain);

    /// <summary>Whether a god of the world holds a domain.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god, if any.</param>
    /// <param name="domain">The domain.</param>
    /// <returns><c>true</c> when it does.</returns>
    public static bool Holds(WorldState world, int? godId, GodDomain domain)
    {
        ArgumentNullException.ThrowIfNull(world);
        return godId is { } id && Holds(GodStanding.Find(world, id), domain);
    }

    /// <summary>The god most of a settlement's people follow, if one holds the majority.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Its id, or <c>null</c>.</returns>
    public static int? MajorityGodId(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        foreach (var belief in settlement.Beliefs)
        {
            if (belief.Adherence >= BeliefModel.MajorityShare)
            {
                return belief.GodId;
            }
        }

        return null;
    }

    /// <summary>Whether the god most of a settlement's people follow holds a domain: its boons reach that settlement.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="domain">The domain.</param>
    /// <returns><c>true</c> when it does.</returns>
    public static bool MajorityHolds(WorldState world, SettlementState settlement, GodDomain domain) => Holds(world, MajorityGodId(settlement), domain);

    /// <summary>The share of a miracle's cost a god pays: less for its own domains' miracles.</summary>
    /// <param name="rules">Domain rules, or <c>null</c> where gods have no domains.</param>
    /// <param name="god">The god.</param>
    /// <param name="miracle">The miracle.</param>
    /// <returns>The factor (0..1].</returns>
    public static float CostFactor(DomainRules? rules, GodState god, MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        return rules is not null && Holds(god, miracle.Domain) ? rules.DomainCostFactor : 1f;
    }

    /// <summary>The definition of a domain, if gods may choose it.</summary>
    /// <param name="rules">Domain rules.</param>
    /// <param name="domain">The domain.</param>
    /// <returns>The definition, or <c>null</c>.</returns>
    public static DomainDefinition? Find(DomainRules rules, GodDomain domain)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var definition in rules.Domains)
        {
            if (definition.Domain == domain)
            {
                return definition;
            }
        }

        return null;
    }
}
