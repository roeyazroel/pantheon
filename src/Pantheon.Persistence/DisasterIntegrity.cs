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
    }
}
