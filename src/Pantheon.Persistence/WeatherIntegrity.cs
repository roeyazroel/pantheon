using System;
using System.Collections.Generic;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>Structural checks on the weather of a deserialized world (part of <see cref="SaveIntegrity"/>).</summary>
internal static class WeatherIntegrity
{
    /// <summary>Records every inconsistency of the weather state and its cells.</summary>
    /// <param name="world">Deserialized world.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(WorldState world, HashSet<int> godIds, List<string> errors)
    {
        var weather = world.Weather;
        if (weather.NextCellId < 1 || !Enum.IsDefined(weather.Spell) || weather.SpellEndsTick < 0 || !float.IsFinite(weather.WindDirection))
        {
            errors.Add("The weather has a next cell id below 1, an unknown spell, a negative spell end or an invalid wind direction.");
        }

        var ids = new HashSet<int>();
        foreach (var cell in weather.Cells)
        {
            if (cell is null || cell.Id < 1 || cell.Id >= weather.NextCellId || !ids.Add(cell.Id))
            {
                errors.Add("A weather cell is null, or its id is repeated or not below the next cell id.");
                continue;
            }

            if (!float.IsFinite(cell.X) || !float.IsFinite(cell.Z) || !float.IsFinite(cell.VelocityX) || !float.IsFinite(cell.VelocityZ) || !(cell.Radius > 0f)
                || !IsShare(cell.Pressure) || !IsShare(cell.Temperature) || !IsShare(cell.Humidity) || !IsShare(cell.Precipitation) || !IsShare(cell.Wind) || !IsShare(cell.ElectricalActivity)
                || (cell.SummonedByGodId is { } god && !godIds.Contains(god)))
            {
                errors.Add($"Weather cell {cell.Id} has an invalid position, velocity or radius, a value outside 0..1, or names a missing god.");
            }
        }
    }

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool IsShare(float value) => value is >= 0f and <= 1f;
}
