using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Checks the villagers the gods follow in a loaded save (ADR-043).</summary>
internal static class VillagerIntegrity
{
    /// <summary>Records every inconsistency of the followed villagers.</summary>
    /// <param name="world">The world.</param>
    /// <param name="godIds">The gods' ids.</param>
    /// <param name="errors">The save's errors.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        if (world.Villagers is null || world.NextVillagerId < 1)
        {
            errors.Add("The followed villagers are missing, or the next villager id is below 1.");
            return;
        }

        var ids = new HashSet<int>();
        var followers = new HashSet<int>();
        foreach (var villager in world.Villagers)
        {
            if (villager is null || villager.Id < 1 || villager.Id >= world.NextVillagerId || !ids.Add(villager.Id)
                || !godIds.Contains(villager.FollowerGodId) || !followers.Add(villager.FollowerGodId)
                || string.IsNullOrWhiteSpace(villager.Name) || !Enum.IsDefined(villager.Work) || !Enum.IsDefined(villager.Activity)
                || !world.Settlements.Exists(s => s.Id == villager.SettlementId)
                || (villager.RefugeId is { } refuge && !world.Settlements.Exists(s => s.Id == refuge))
                || (villager.GodId is { } god && !godIds.Contains(god))
                || !Finite(villager.Home) || !Finite(villager.WorkPlace) || !Finite(villager.Shrine) || !Finite(villager.Position)
                || villager.Path is null || villager.Memories is null || !Share(villager.Faith) || !Share(villager.Hunger) || !Share(villager.Fatigue) || !Share(villager.Health)
                || villager.FollowedTick > world.Tick || !(villager.AgeYears >= 0f) || !(villager.LifeYears > villager.AgeYears))
            {
                errors.Add("A followed villager is null, has a repeated or unknown id, follower, village or god, an invalid place or need, or an impossible life.");
            }
        }
    }

    /// <summary>Whether a point is finite.</summary>
    private static bool Finite(GroundPosition point) => float.IsFinite(point.X) && float.IsFinite(point.Z);

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool Share(float value) => value is >= 0f and <= 1f;
}
