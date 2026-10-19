using System;
using System.Collections.Generic;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the gods' acquaintances and plans (ADR-036; part of <see cref="SaveIntegrity"/>).</summary>
internal static class PlanIntegrity
{
    /// <summary>Records every inconsistency of the gods' known gods and goals.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        foreach (var god in world.Gods)
        {
            if (god is not null)
            {
                Validate(world, god, godIds, errors);
            }
        }
    }

    /// <summary>Records every inconsistency of one god's known gods and goals.</summary>
    private static void Validate(WorldState world, GodState god, HashSet<int> godIds, List<string> errors)
    {
        var known = god.KnownGods;
        for (var i = 0; i < known.Count; i++)
        {
            if (known[i] == god.Id || !godIds.Contains(known[i]) || (i > 0 && known[i] <= known[i - 1]))
            {
                errors.Add($"God {god.Id} knows a missing god, itself, or its known gods are repeated or unsorted.");
                break;
            }
        }

        foreach (var goal in god.Goals)
        {
            if (goal is null || !Enum.IsDefined(goal.Kind) || goal.SinceTick < 0 || goal.SinceTick > world.Tick || !(goal.Weight is >= 0f and <= 1f)
                || (goal.GodId is { } other && (other == god.Id || !godIds.Contains(other)))
                || (goal.SettlementId is { } place && !world.Settlements.Exists(s => s.Id == place)))
            {
                errors.Add($"God {god.Id} has a goal that is null, of an unknown kind, from an impossible tick, of a weight outside 0..1, or about a missing god or village.");
            }
        }

        if (god.NextPlanTick < 0)
        {
            errors.Add($"God {god.Id} plans next at a negative tick.");
        }
    }
}
