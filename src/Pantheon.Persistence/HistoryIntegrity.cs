using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the world's history (part of <see cref="SaveIntegrity"/>, ADR-020).</summary>
internal static class HistoryIntegrity
{
    /// <summary>Records every record out of time or order, of an unknown kind, or naming what the world does not have.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        if (world.History is null)
        {
            errors.Add("World has no history.");
            return;
        }

        var settlements = new HashSet<int>();
        world.Settlements.ForEach(s => settlements.Add(s.Id));
        var creatures = new HashSet<int>();
        world.Creatures.ForEach(c => creatures.Add(c.Id));
        var previous = 0L;
        for (var i = 0; i < world.History.Count; i++)
        {
            var record = world.History[i];
            if (record is null || !Enum.IsDefined(record.Kind) || record.Tick < previous || record.Tick > world.Tick || record.Count < 0)
            {
                errors.Add($"History record {i} is of an unknown kind, out of order or out of time.");
                previous = record?.Tick ?? previous;
                continue;
            }

            previous = record.Tick;
            if (!Known(record.GodId, godIds) || !Known(record.OtherGodId, godIds) || !Known(record.SettlementId, settlements)
                || !Known(record.CreatureId, creatures) || !Known(record.OtherCreatureId, creatures))
            {
                errors.Add($"History record {i} names a missing god, settlement or Creature.");
            }
        }
    }

    /// <summary>Whether an optional id is absent or known.</summary>
    private static bool Known(int? id, HashSet<int> ids) => id is not { } value || ids.Contains(value);
}
