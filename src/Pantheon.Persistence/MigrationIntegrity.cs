using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the people on the move in a deserialized world (part of <see cref="SaveIntegrity"/>).</summary>
internal static class MigrationIntegrity
{
    /// <summary>Records every inconsistency of the migration groups.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        var migration = world.Migration;
        if (migration.NextGroupId < 1)
        {
            errors.Add("The next migration group id must be at least 1.");
        }

        var settlementIds = new HashSet<int>();
        foreach (var settlement in world.Settlements)
        {
            if (settlement is not null)
            {
                settlementIds.Add(settlement.Id);
            }
        }

        var ids = new HashSet<int>();
        foreach (var group in migration.Groups)
        {
            if (group is null || group.Id < 1 || group.Id >= migration.NextGroupId || !ids.Add(group.Id))
            {
                errors.Add("A migration group is null, or its id is repeated or not below the next group id.");
                continue;
            }

            if (!settlementIds.Contains(group.OriginSettlementId) || !settlementIds.Contains(group.DestinationSettlementId) || group.TurnedAwayFrom.Exists(id => !settlementIds.Contains(id)))
            {
                errors.Add($"Migration group {group.Id} names a missing settlement.");
            }

            if (group.Population < 1 || !float.IsFinite(group.Food) || group.Food < 0f || !SaveIntegrity.IsShare(group.Health)
                || !Enum.IsDefined(group.Cause) || group.DepartedTick < 0 || group.DepartedTick > world.Tick)
            {
                errors.Add($"Migration group {group.Id} has an invalid size, food, health, cause or departure.");
            }

            if (!float.IsFinite(group.Position.X) || !float.IsFinite(group.Position.Z) || group.Path.Exists(p => !float.IsFinite(p.X) || !float.IsFinite(p.Z)))
            {
                errors.Add($"Migration group {group.Id} has a position or path point that is not finite.");
            }

            SaveIntegrity.ValidateBeliefs(group.Beliefs, $"Migration group {group.Id}", godIds, errors);
        }
    }
}
