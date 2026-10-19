using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks the land's reshapings in a loaded save (ADR-045).</summary>
internal static class LandIntegrity
{
    /// <summary>Records every inconsistency of the reshapings.</summary>
    /// <param name="world">The world.</param>
    /// <param name="godIds">The gods' ids.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        if (world.Land?.Reshapes is null || world.Land.NextReshapeId < 1)
        {
            errors.Add("The land's reshapings are missing, or the next reshaping id is below 1.");
            return;
        }

        var ids = new HashSet<int>();
        foreach (var reshape in world.Land.Reshapes)
        {
            if (reshape is null || reshape.Id < 1 || reshape.Id >= world.Land.NextReshapeId || !ids.Add(reshape.Id) || !Enum.IsDefined(reshape.Kind)
                || !godIds.Contains(reshape.GodId) || reshape.Tick > world.Tick || !(reshape.Radius > 0f)
                || !float.IsFinite(reshape.X) || !float.IsFinite(reshape.Z) || !float.IsFinite(reshape.ToX) || !float.IsFinite(reshape.ToZ)
                || !float.IsFinite(reshape.Peak) || !float.IsFinite(reshape.FromHeight) || !float.IsFinite(reshape.ToHeight)
                || !float.IsFinite(reshape.DamX) || !float.IsFinite(reshape.DamZ) || !float.IsFinite(reshape.DamTop))
            {
                errors.Add("A reshaping of the land is null, has a repeated or unknown id, an unknown kind or god, a time to come, or an invalid place or size.");
            }
        }
    }
}
