using System;
using System.Collections.Generic;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Hand;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks what a god's Divine Hand holds in a loaded save (ADR-030).</summary>
internal static class HandIntegrity
{
    /// <summary>Records every inconsistency of what the god's hand holds.</summary>
    /// <param name="world">The world.</param>
    /// <param name="god">The god.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, GodState god, List<string> errors)
    {
        if (god.Held is not { } held)
        {
            return;
        }

        var villagers = held.Kind == HeldKind.Villagers;
        if (!Enum.IsDefined(held.Kind) || held.Amount < 1 || (!villagers && held.Amount != 1) || held.Beliefs is null
            || !(held.Health is >= 0f and <= 1f) || held.Tick < 0 || held.Tick > world.Tick
            || (villagers && (held.SettlementId is not { } from || !world.Settlements.Exists(s => s.Id == from)))
            || (held.Kind == HeldKind.Creature && world.Creatures.Find(c => c.GodId == god.Id) is null))
        {
            errors.Add($"God {god.Id}'s hand holds something unknown, an amount out of range, villagers from no settlement, or a Creature it does not have.");
        }
    }
}
