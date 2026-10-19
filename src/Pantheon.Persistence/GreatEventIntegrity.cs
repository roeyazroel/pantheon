using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks the plague and the great events in a loaded save (ADR-034).</summary>
internal static class GreatEventIntegrity
{
    /// <summary>Records every inconsistency of the plague and the great events.</summary>
    /// <param name="world">The world.</param>
    /// <param name="godIds">The gods' ids.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Plague is not (>= 0f and <= 1f) || settlement.Immunity is not (>= 0f and <= 1f))
            {
                errors.Add($"Settlement {settlement.Id} has plague or immunity outside 0..1.");
            }
        }

        var state = world.GreatEvents;
        if (state is null || state.Events is null || state.LastReformations is null || state.NextId < 1 || state.NextEclipseTick < 0)
        {
            errors.Add("The great events are missing, or their next id or eclipse is invalid.");
            return;
        }

        var ids = new HashSet<int>();
        foreach (var great in state.Events)
        {
            if (great is null || great.Id < 1 || great.Id >= state.NextId || !ids.Add(great.Id) || !Enum.IsDefined(great.Kind) || string.IsNullOrWhiteSpace(great.Name)
                || great.StartTick > world.Tick || great.EndTick < great.StartTick || (great.GodId is { } god && !godIds.Contains(god)) || great.Peak < 0)
            {
                errors.Add("A great event is null, has a repeated or unknown id, an unknown kind or god, no name, or impossible ticks.");
            }
        }

        foreach (var (god, tick) in state.LastReformations)
        {
            if (!godIds.Contains(god) || tick > world.Tick)
            {
                errors.Add($"A reformation is recorded for unknown god {god}, or in the future.");
            }
        }

        if (state.DrySinceTick > world.Tick)
        {
            errors.Add("The land has been dry since a tick in the future.");
        }
    }
}
