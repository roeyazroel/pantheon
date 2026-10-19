using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Simulation.Water;

namespace Pantheon.Game.Diagnostics;

/// <summary>The debug overlay's lines about the rivers and lakes (ADR-022): each river's level, the floods, and the lakes.</summary>
public static class WaterReadout
{
    /// <summary>Adds the rivers' and lakes' lines, when the world has any and the snapshot carries the water rules.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        var water = snapshot.World.Water;
        if (snapshot.WaterRules is not { } rules || (water.Rivers.Count == 0 && water.Lakes.Count == 0))
        {
            return;
        }

        var rivers = water.Rivers.Select(river =>
        {
            var overflow = WaterModel.Overflow(rules, river.Level);
            return Format("{0} {1:0.00}{2}", river.Name, river.Level, overflow > 0f ? Format(" FLOOD {0:0%}", overflow) : string.Empty);
        });
        lines.Add(Format("Rivers ({0}): {1}", water.Rivers.Count, string.Join(", ", rivers)));
        var flooded = snapshot.World.Regions.Cells.Count(region => region.Flood > 0f);
        lines.Add(Format(
            "  lakes: {0}   flooded regions {1}   watered regions {2}",
            water.Lakes.Count == 0 ? "none" : string.Join(", ", water.Lakes.Select(lake => Format("{0} ({1:0} m)", lake.Name, lake.SurfaceHeight))),
            flooded,
            snapshot.World.Regions.Cells.Count(region => region.FreshWater > 0f)));
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
