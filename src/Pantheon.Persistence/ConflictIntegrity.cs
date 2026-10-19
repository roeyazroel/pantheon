using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on wars and holy wars (part of <see cref="SaveIntegrity"/>, ADR-026).</summary>
internal static class ConflictIntegrity
{
    /// <summary>Records every inconsistency of the world's wars and holy wars.</summary>
    /// <param name="world">The world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        if (world.Diplomacy.Wars is not { } wars)
        {
            errors.Add("Diplomacy has no wars.");
            return;
        }

        foreach (var war in wars)
        {
            if (war is null || !godIds.Contains(war.AggressorGodId) || !godIds.Contains(war.DefenderGodId) || war.AggressorGodId == war.DefenderGodId || !Enum.IsDefined(war.Goal)
                || (war.SiteId is { } site && !world.HolySites.Exists(s => s.Id == site)) || !(war.AggressorScore >= 0f) || !(war.DefenderScore >= 0f)
                || !float.IsFinite(war.AggressorScore) || !float.IsFinite(war.DefenderScore))
            {
                errors.Add("A war names a missing god or site, one god twice, an unknown goal, or a score that is not finite and positive.");
            }
        }

        foreach (var settlement in world.Settlements)
        {
            if (settlement?.Society?.HolyWar is not { } holyWar)
            {
                continue;
            }

            if (!world.Settlements.Exists(s => s.Id == holyWar.DefenderSettlementId) || holyWar.DefenderSettlementId == settlement.Id || !godIds.Contains(holyWar.GodId)
                || !godIds.Contains(holyWar.EnemyGodId) || !Enum.IsDefined(holyWar.Stance) || holyWar.Killed < 0 || holyWar.Converted < 0)
            {
                errors.Add($"Settlement {settlement.Id} wages a holy war on a missing settlement or itself, for a missing god, or with an unknown stance or negative counts.");
            }
        }
    }
}
