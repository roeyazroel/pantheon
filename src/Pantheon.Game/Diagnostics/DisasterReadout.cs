using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Diagnostics;

/// <summary>The disaster lines of the F1 debug panel (spec §106, ADR-014). Pure.</summary>
public static class DisasterReadout
{
    /// <summary>Most fires listed, fiercest first.</summary>
    public const int ListedFires = 3;

    /// <summary>Adds the earthquakes under way and the fires burning.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data; nothing is added without disaster rules.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.DisasterRules is null)
        {
            return;
        }

        var world = snapshot.World;
        var burning = world.Regions.Cells.Where(r => r.Fire > 0f).ToList();
        var scorched = world.Regions.Cells.Count(r => r.Scorch >= 0.05f);
        lines.Add(Format("Disasters: {0} earthquakes, {1} regions burning, {2} scorched", world.Disasters.Earthquakes.Count, burning.Count, scorched));
        foreach (var quake in world.Disasters.Earthquakes)
        {
            lines.Add(Format(
                "  quake #{0} M{1:0.0} at ({2:0}, {3:0}): shaking {4:0.00}, {5:0} m around, {6} left{7}",
                quake.Id,
                quake.Magnitude,
                quake.X,
                quake.Z,
                quake.Intensity,
                quake.Radius,
                DivineText.Clock((quake.EndsAtTick - world.Tick) / snapshot.TicksPerSecond),
                quake.CausedByGodId is { } god ? Format(", caused by god {0}", god) : string.Empty));
        }

        foreach (var region in burning.OrderByDescending(r => r.Fire).ThenBy(r => r.Id).Take(ListedFires))
        {
            lines.Add(Format(
                "  fire in region #{0}: {1:0.00}, fuel {2:0.00}, scorch {3:0.00}",
                region.Id,
                region.Fire,
                DisasterModel.Flammability(snapshot.DisasterRules, region),
                region.Scorch));
        }
    }

    /// <summary>A settlement's dangers and how it is coping: fire threat, shaking, ruins, the homeless and any shield.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>One line, or <c>null</c> without disaster rules.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settlement);
        if (snapshot.DisasterRules is null)
        {
            return null;
        }

        var world = snapshot.World;
        var shield = DisasterModel.IsShielded(settlement, world.Tick)
            ? Format(", shielded for {0}", DivineText.Clock((settlement.ShieldedUntilTick - world.Tick) / snapshot.TicksPerSecond))
            : string.Empty;
        return Format(
            "  dangers: fire threat {0:0.00}, shaking {1:0.00}; {2} in ruins, {3:0%} homeless, scorched land {4:0.00}{5}",
            DisasterModel.FireThreat(snapshot.SettlementRules, world.Regions, settlement),
            DisasterModel.ShakingAt(world.Disasters, settlement.Position),
            settlement.Ruins,
            SettlementEconomy.ComputeHomelessShare(settlement),
            SettlementEconomy.SurveyFarmland(world.Regions, settlement.RegionId, snapshot.SettlementRules.FarmlandRadius).MeanScorch,
            shield);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
