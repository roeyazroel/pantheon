using System;
using System.Collections.Generic;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Miracles;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Conflict;

/// <summary>
/// Veiled miracles (spec §45: secretly create disasters, anonymously provide miracles; ADR-026). A
/// veiled miracle does what it does, but its witnesses cannot tell who worked it: a kindness earns
/// no faith or love, and a strike makes the people doubt their own god for not protecting them. The
/// god whose people it reached may trace it.
/// </summary>
public static class Veil
{
    /// <summary>What a settlement's people take from a veiled miracle: a strike plants doubt in their own god.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="settlement">The settlement it reached.</param>
    /// <param name="casterGodId">The caster, whose own followers are not deceived.</param>
    /// <param name="miracle">The miracle.</param>
    public static void Witnessed(ConflictRules rules, SettlementState settlement, int casterGodId, MiracleDefinition miracle)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(miracle);
        if (Culture.CultureModel.Harms(miracle) && TreatyModel.Leader(settlement) is { } leader && leader != casterGodId && BeliefModel.Find(settlement, leader) is { } belief)
        {
            belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, rules.VeiledDoubt);
        }
    }

    /// <summary>The god whose people a veiled miracle reached most: the god of the first settlement it reached led by another.</summary>
    /// <param name="world">World state.</param>
    /// <param name="casterGodId">The caster.</param>
    /// <param name="affected">The settlements it reached.</param>
    /// <returns>The god, or <c>null</c>.</returns>
    public static int? Victim(WorldState world, int casterGodId, IReadOnlyList<int> affected)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(affected);
        foreach (var id in affected)
        {
            if (world.Settlements.Find(s => s.Id == id) is { } settlement && TreatyModel.Leader(settlement) is { } leader && leader != casterGodId)
            {
                return leader;
            }
        }

        return null;
    }

    /// <summary>Whether the god whose people a veiled miracle reached traces it to the caster.</summary>
    /// <param name="rules">Conflict rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="casterGodId">The caster.</param>
    /// <param name="miracle">The miracle.</param>
    /// <param name="affected">The settlements it reached.</param>
    /// <returns><c>true</c> when exposed.</returns>
    public static bool Exposed(ConflictRules rules, WorldState world, int casterGodId, MiracleDefinition miracle, IReadOnlyList<int> affected)
    {
        ArgumentNullException.ThrowIfNull(miracle);
        return Victim(world, casterGodId, affected) is { } victim && Covert.IsExposed(rules, world, casterGodId, victim, miracle.Id);
    }
}
