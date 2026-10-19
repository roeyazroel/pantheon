using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Land;
using Pantheon.Game.Land;

namespace Pantheon.Game.Diagnostics;

/// <summary>The land's reshapings in the F1 debug panel (spec §14, ADR-045). Pure.</summary>
public static class LandReadout
{
    /// <summary>Adds one line for the reshapings, when there are any.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        var world = snapshot.World;
        if (world.Land.Reshapes.Count == 0)
        {
            return;
        }

        lines.Add(Format("Land: {0} reshaped: {1}", world.Land.Reshapes.Count, string.Join("; ", world.Land.Reshapes.Select(r => Format(
            "{0} by {1} at ({2:0}, {3:0}){4}",
            LandCommands.Name(r.Kind),
            world.Gods.Find(g => g.Id == r.GodId)?.Name ?? "a god",
            r.X,
            r.Z,
            r.Kind switch
            {
                ReshapeKind.Island => Format(", {0:0} m high", r.Peak),
                ReshapeKind.Gorge => Format(" to ({0:0}, {1:0}), floor {2:0}-{3:0} m", r.ToX, r.ToZ, r.FromHeight, r.ToHeight),
                ReshapeKind.Channel => Format(" to ({0:0}, {1:0}), falling {2:0} m", r.ToX, r.ToZ, r.FromHeight - r.ToHeight),
            })))));
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
