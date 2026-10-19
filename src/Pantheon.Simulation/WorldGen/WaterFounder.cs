using System;
using System.Collections.Generic;
using Pantheon.Domain.Water;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Records the rivers and lakes of the generated terrain in the world state (ADR-022).</summary>
public static class WaterFounder
{
    /// <summary>Level of a river in a new world: an ordinary flow, which the weather soon sets.</summary>
    public const float InitialLevel = 0.5f;

    /// <summary>The world's rivers and lakes, with the regions each runs through or covers.</summary>
    /// <param name="hydrology">The terrain's rivers and lakes.</param>
    /// <param name="regions">Region grid.</param>
    /// <returns>The water state; ids start at 1, in the map's order.</returns>
    public static WaterState Found(HydrologyMap hydrology, RegionGrid regions)
    {
        ArgumentNullException.ThrowIfNull(hydrology);
        ArgumentNullException.ThrowIfNull(regions);
        var rivers = new List<River>(hydrology.Rivers.Count);
        for (var i = 0; i < hydrology.Rivers.Count; i++)
        {
            var course = hydrology.Rivers[i];
            var path = new List<int>();
            foreach (var point in course.Points)
            {
                if (regions.TryGetRegionIdAt(point.X, point.Z, out var id) && !path.Contains(id))
                {
                    path.Add(id);
                }
            }

            rivers.Add(new River { Id = i + 1, Name = course.Name, Regions = path, Level = InitialLevel });
        }

        var lakes = new List<Lake>(hydrology.Lakes.Count);
        for (var i = 0; i < hydrology.Lakes.Count; i++)
        {
            var basin = hydrology.Lakes[i];
            var covered = new SortedSet<int>();
            foreach (var cell in basin.Cells)
            {
                if (regions.TryGetRegionIdAt(cell.X, cell.Z, out var id))
                {
                    covered.Add(id);
                }
            }

            lakes.Add(new Lake { Id = i + 1, Name = basin.Name, Regions = [.. covered], SurfaceHeight = basin.Surface });
        }

        return new WaterState { Rivers = rivers, Lakes = lakes };
    }

    /// <summary>A world without fresh water.</summary>
    /// <returns>An empty water state.</returns>
    public static WaterState None() => new() { Rivers = [], Lakes = [] };
}
