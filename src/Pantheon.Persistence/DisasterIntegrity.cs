using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the disasters of a deserialized world (part of <see cref="SaveIntegrity"/>).</summary>
internal static class DisasterIntegrity
{
    /// <summary>Records every inconsistency of the earthquakes under way.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        var disasters = world.Disasters;
        if (disasters.NextEarthquakeId < 1)
        {
            errors.Add("The next earthquake id must be at least 1.");
        }

        var ids = new HashSet<int>();
        foreach (var quake in disasters.Earthquakes)
        {
            if (quake is null || quake.Id < 1 || quake.Id >= disasters.NextEarthquakeId || !ids.Add(quake.Id))
            {
                errors.Add("An earthquake is null, or its id is repeated or not below the next earthquake id.");
                continue;
            }

            if (!float.IsFinite(quake.X) || !float.IsFinite(quake.Z) || !float.IsFinite(quake.Magnitude) || !(quake.Radius > 0f)
                || !(quake.Intensity is >= 0f and <= 1f) || quake.EndsAtTick < 0 || (quake.CausedByGodId is { } god && !godIds.Contains(god)))
            {
                errors.Add($"Earthquake {quake.Id} has an invalid position, magnitude, radius, intensity or end, or names a missing god.");
            }
        }

        ValidateHazards(world, errors);
    }

    /// <summary>Records every inconsistency of the tornadoes, swarms, volcano, marks and blighted villages (ADR-029).</summary>
    private static void ValidateHazards(WorldState world, List<string> errors)
    {
        var disasters = world.Disasters;
        if (disasters.NextHazardId < 1 || disasters.Tornadoes is null || disasters.Swarms is null || disasters.Marks is null || disasters.BlightedSettlementIds is null)
        {
            errors.Add("The hazards are missing, or the next hazard id is below 1.");
            return;
        }

        var ids = new HashSet<int>();
        foreach (var tornado in disasters.Tornadoes)
        {
            if (tornado is null || tornado.Id < 1 || tornado.Id >= disasters.NextHazardId || !ids.Add(tornado.Id)
                || !float.IsFinite(tornado.X) || !float.IsFinite(tornado.Z) || !float.IsFinite(tornado.VelocityX) || !float.IsFinite(tornado.VelocityZ))
            {
                errors.Add("A tornado is null, has a repeated id or one not below the next hazard id, or an invalid position or velocity.");
            }
        }

        foreach (var swarm in disasters.Swarms)
        {
            if (swarm is null || swarm.Id < 1 || swarm.Id >= disasters.NextHazardId || !ids.Add(swarm.Id)
                || !float.IsFinite(swarm.X) || !float.IsFinite(swarm.Z) || !(swarm.Size is >= 0f and <= 1f))
            {
                errors.Add("A locust swarm is null, has a repeated id or one not below the next hazard id, or an invalid position or size.");
            }
        }

        if (disasters.Volcano is { } volcano && (volcano.RegionId < 0 || volcano.RegionId >= world.Regions.Cells.Count || !System.Enum.IsDefined(volcano.Phase)))
        {
            errors.Add("The volcano stands outside the region grid, or does something unknown.");
        }

        foreach (var mark in disasters.Marks)
        {
            if (mark is null || !float.IsFinite(mark.X) || !float.IsFinite(mark.Z) || !(mark.Radius > 0f) || !System.Enum.IsDefined(mark.Kind))
            {
                errors.Add("A mark on the land is null, or has an invalid position, radius or kind.");
            }
        }

        var blighted = new HashSet<int>();
        foreach (var id in disasters.BlightedSettlementIds)
        {
            if (!blighted.Add(id) || !world.Settlements.Exists(s => s.Id == id))
            {
                errors.Add("A village told of blight is repeated, or is no settlement.");
            }
        }
    }
}
