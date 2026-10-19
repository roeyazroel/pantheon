using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks the world's prophets and elders in a loaded save (ADR-032).</summary>
internal static class NotableIntegrity
{
    /// <summary>Records every inconsistency of the notables.</summary>
    /// <param name="world">The world.</param>
    /// <param name="godIds">The gods' ids.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        if (world.Notables is null || world.NextNotableId < 1)
        {
            errors.Add("The notables are missing, or the next notable id is below 1.");
            return;
        }

        var ids = new HashSet<int>();
        foreach (var notable in world.Notables)
        {
            if (notable is null || notable.Id < 1 || notable.Id >= world.NextNotableId || !ids.Add(notable.Id)
                || !Enum.IsDefined(notable.Kind) || string.IsNullOrWhiteSpace(notable.Name) || !godIds.Contains(notable.GodId)
                || !world.Settlements.Exists(s => s.Id == notable.SettlementId)
                || (notable.TargetSettlementId is { } target && !world.Settlements.Exists(s => s.Id == target))
                || !float.IsFinite(notable.Position.X) || !float.IsFinite(notable.Position.Z) || notable.Path is null || notable.Memories is null
                || notable.TicksLeft < 0 || notable.BornTick > world.Tick || notable.DiesAtTick < notable.BornTick)
            {
                errors.Add("A notable is null, has a repeated or unknown id, an unknown kind, god or village, an invalid place or an impossible life.");
            }
        }
    }
}
