using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Weather;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Weather;

namespace Pantheon.Game.Diagnostics;

/// <summary>The weather lines of the F1 debug panel (spec §106: weather cells; ADR-013). Pure.</summary>
public static class WeatherReadout
{
    /// <summary>Most cells listed, strongest first.</summary>
    public const int ListedCells = 4;

    /// <summary>Adds the spell, the wind and the strongest cells.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data; nothing is added without weather rules.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.WeatherRules is not { } rules)
        {
            return;
        }

        var weather = snapshot.World.Weather;
        lines.Add(Format("Weather: {0} {1} cells.", WeatherText.Describe(weather, snapshot.World.Tick), weather.Cells.Count));
        foreach (var cell in weather.Cells.OrderByDescending(c => Math.Max(c.Wind, c.Precipitation)).ThenBy(c => c.Id).Take(ListedCells))
        {
            lines.Add(DescribeCell(rules, cell));
        }
    }

    /// <summary>One cell: what it is, where, and its state.</summary>
    /// <param name="rules">Weather rules.</param>
    /// <param name="cell">The cell.</param>
    /// <returns>One line.</returns>
    public static string DescribeCell(WeatherRules rules, WeatherCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var speed = MathF.Sqrt((cell.VelocityX * cell.VelocityX) + (cell.VelocityZ * cell.VelocityZ));
        return Format(
            "  #{0} {1}{2} at ({3:0}, {4:0}) r {5:0} m: rain {6:0.00} wind {7:0.00} lightning {8:0.00} humidity {9:0.00} pressure {10:0.00}, {11:0.0} m/s",
            cell.Id,
            WeatherText.Kind(WeatherModel.Classify(rules, cell)),
            cell.SummonedByGodId is { } god ? Format(" (called by god {0})", god) : string.Empty,
            cell.X,
            cell.Z,
            cell.Radius,
            cell.Precipitation,
            cell.Wind,
            cell.ElectricalActivity,
            cell.Humidity,
            cell.Pressure,
            speed);
    }

    /// <summary>A settlement's water: its farmland's soil, the drought, the yield it allows and the weather over it.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>One line, or <c>null</c> without weather rules.</returns>
    public static string? DescribeSettlement(DebugSnapshot snapshot, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(settlement);
        if (snapshot.WeatherRules is not { } rules)
        {
            return null;
        }

        var land = SettlementEconomy.SurveyFarmland(snapshot.World.Regions, settlement.RegionId, snapshot.SettlementRules.FarmlandRadius);
        var drought = SettlementEconomy.ComputeDrought(snapshot.SettlementRules, land);
        var now = WeatherModel.At(rules, snapshot.World.Weather, settlement.Position);
        return Format(
            "  water: soil {0:0.00} ({1}, needs {2:0.00}), yield x{3:0.00}; rain now {4:0.00}, wind {5:0.00}{6}",
            land.MeanSoilMoisture,
            WeatherText.Drought(drought, snapshot.SettlementRules.DroughtThreshold),
            snapshot.SettlementRules.WellWateredSoil,
            SettlementEconomy.ComputeWaterYield(snapshot.SettlementRules, land),
            now.Rain,
            now.Wind,
            now.Wind >= rules.StormWind ? "  STORM: stores and building work are being ruined" : string.Empty);
    }

    /// <summary>The weather at a point of the ground, for the cursor lines.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <param name="point">The point.</param>
    /// <param name="regionId">The region under it.</param>
    /// <returns>One line, or <c>null</c> without weather rules.</returns>
    public static string? DescribePoint(DebugSnapshot snapshot, GroundPosition point, int regionId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.WeatherRules is not { } rules)
        {
            return null;
        }

        var now = WeatherModel.At(rules, snapshot.World.Weather, point);
        return Format(
            "  soil {0:0.00}  rain {1:0.00}  wind {2:0.00}  lightning {3:0.00}",
            snapshot.World.Regions.GetRegion(regionId).SoilMoisture,
            now.Rain,
            now.Wind,
            now.Lightning);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
