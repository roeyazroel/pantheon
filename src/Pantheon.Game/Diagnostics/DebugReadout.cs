using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Game.Diagnostics;

/// <summary>Formats a <see cref="DebugSnapshot"/> into overlay text. Pure and culture-invariant.</summary>
public static class DebugReadout
{
    /// <summary>Camera control hints shown at the bottom of the overlay.</summary>
    public const string CameraHints = "[WASD/arrows/screen edge] pan  [RMB drag] grab  [wheel, +/-] zoom  [Q/E, MMB drag] rotate";

    /// <summary>World and simulation key hints shown at the bottom of the overlay.</summary>
    public const string KeyHints = "[F] focus  [F2] new world  [F5] save  [F9] load  [Space] pause  [1/2/3] speed";

    /// <summary>Debug tool key hints shown at the bottom of the overlay.</summary>
    public const string DebugHints = "[F1] overlay  [B] regions  [F3] debug map  [.] step one tick";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Builds the overlay content.</summary>
    /// <param name="snapshot">Frame data.</param>
    /// <returns>Overlay lines.</returns>
    public static DebugPanelModel Build(DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var world = snapshot.World;
        var lines = new List<string>
        {
            Format("FPS {0:0}   Tick {1}   {2}", snapshot.FramesPerSecond, world.Tick, FormatClock(snapshot)),
            Format("Seed {0}", world.Seed),
            FormatTerrain(snapshot),
            Format("Camera {0}  {1:0} m", snapshot.ZoomBand, snapshot.CameraDistance),
        };

        AppendCursor(lines, world, snapshot.CursorWorldPosition);
        foreach (var settlement in world.Settlements)
        {
            AppendSettlement(lines, world, settlement, snapshot.SettlementRules);
        }

        lines.Add(Format("Region boundaries {0}", snapshot.RegionBoundariesVisible ? "ON" : "OFF"));
        if (!string.IsNullOrEmpty(snapshot.StatusMessage))
        {
            lines.Add(snapshot.StatusMessage);
        }

        lines.Add(CameraHints);
        lines.Add(KeyHints);
        lines.Add(DebugHints);
        return new DebugPanelModel(lines);
    }

    /// <summary>Simulation rate, or the paused marker.</summary>
    private static string FormatClock(DebugSnapshot snapshot) =>
        snapshot.IsPaused
            ? "PAUSED"
            : Format("x{0:0.##} ({1:0.#} ticks/s)", snapshot.TimeScale, snapshot.TicksPerSecond * snapshot.TimeScale);

    /// <summary>Terrain backend line, which stands out when the fallback renderer is active (the backend name says why).</summary>
    private static string FormatTerrain(DebugSnapshot snapshot) =>
        snapshot.TerrainIsFallback
            ? Format("Terrain {0}  ** FALLBACK RENDERER **", snapshot.TerrainBackend)
            : Format("Terrain {0}", snapshot.TerrainBackend);

    /// <summary>Cursor position and the region under it.</summary>
    private static void AppendCursor(List<string> lines, WorldState world, Vector3? cursor)
    {
        if (cursor is not { } point || !world.Regions.TryGetRegionIdAt(point.X, point.Z, out var regionId))
        {
            lines.Add("Cursor -");
            return;
        }

        var region = world.Regions.GetRegion(regionId);
        var (column, row) = world.Regions.GetCoordinates(regionId);
        lines.Add(Format("Cursor ({0:0}, {1:0})  height {2:0.0} m", point.X, point.Z, point.Y));
        lines.Add(Format("Region #{0} ({1}, {2})  {3}", regionId, column, row, region.Biome));
        lines.Add(Format(
            "  elev {0:0.0} m  temp {1:0.00}  moist {2:0.00}  fert {3:0.00}  veg {4:0.00}",
            region.Elevation,
            region.Temperature,
            region.Moisture,
            region.Fertility,
            region.Vegetation));
    }

    /// <summary>Population, housing and food of one settlement.</summary>
    private static void AppendSettlement(List<string> lines, WorldState world, SettlementState settlement, SettlementRules rules)
    {
        var fertility = world.Regions.GetRegion(settlement.RegionId).Fertility;
        var balance = SettlementSystem.ComputeFoodBalance(rules, settlement.Population, fertility);
        var storage = SettlementSystem.ComputeFoodStorage(rules, settlement.Housing);

        lines.Add(Format("Settlement {0} (region #{1})", settlement.Name, settlement.RegionId));
        lines.Add(Format(
            "  pop {0} / {1}   food {2:0.0} / {3:0}   {4:+0.000;-0.000;0.000} food/tick",
            settlement.Population,
            settlement.Housing,
            settlement.Food,
            storage,
            balance));
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(Invariant, format, args);
}
