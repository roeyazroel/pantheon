using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on holy sites, Old Faiths and sanctuaries (part of <see cref="SaveIntegrity"/>, ADR-021).</summary>
internal static class HolySiteIntegrity
{
    // Shares that add up past 1 by less than this are rounding.
    private const float Tolerance = 0.001f;

    /// <summary>Records every site with a repeated id, an unknown kind, a missing region or god, or a negative claim.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        if (world.HolySites is null)
        {
            errors.Add("World has no holy site list.");
            return;
        }

        var ids = new HashSet<int>();
        foreach (var site in world.HolySites)
        {
            if (site is null || !ids.Add(site.Id) || !Enum.IsDefined(site.Kind) || string.IsNullOrWhiteSpace(site.Name))
            {
                errors.Add($"Holy site {site?.Id} is null, repeated, unnamed or of an unknown kind.");
                continue;
            }

            if (site.RegionId < 0 || site.RegionId >= world.Regions.Cells.Count || !Known(site.KeeperGodId, godIds) || !Known(site.ClaimantGodId, godIds) || site.ClaimTicks < 0)
            {
                errors.Add($"Holy site {site.Id} names a missing region or god, or has a negative claim.");
            }
        }
    }

    /// <summary>Checks a settlement's Old Faith and sanctuary: known gods, shares in 0..1 that leave room for the followers, ticks in time.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void ValidateFaith(WorldState world, SettlementState settlement, HashSet<int> godIds, List<string> errors)
    {
        if (settlement.OldFaith is { } old
            && (!godIds.Contains(old.GodId) || !SaveIntegrity.IsShare(old.Share) || !SaveIntegrity.IsShare(old.Returned) || !SaveIntegrity.IsShare(old.Faded) || old.SinceTick < 0 || old.SinceTick > world.Tick
                || BeliefShares(settlement) + old.Share > 1f + Tolerance))
        {
            errors.Add($"Settlement {settlement.Id} has an Old Faith of a missing god, out of time, or with shares outside 0..1 or past the people's.");
        }

        if (!Known(settlement.SanctuaryGodId, godIds) || settlement.SanctuaryUntilTick < 0)
        {
            errors.Add($"Settlement {settlement.Id} has a sanctuary of a missing god or a negative tick.");
        }
    }

    /// <summary>Every god's adherence in a settlement together.</summary>
    private static float BeliefShares(SettlementState settlement)
    {
        var total = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            total += belief?.Adherence ?? 0f;
        }

        return total;
    }

    /// <summary>Whether an optional id is absent or known.</summary>
    private static bool Known(int? id, HashSet<int> ids) => id is not { } value || ids.Contains(value);
}
