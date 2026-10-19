using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Migration;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The overlays of spec §58 added in ADR-031: population, disaster risk and political control. Each
/// paints through a callback, as <see cref="NatureOverlay"/> does.
/// </summary>
public static class StateOverlays
{
    /// <summary>The population overlay's legend.</summary>
    public const string PopulationLegend = "Population: each village's lands from deep blue (few people) to gold (the most); people on the road in white";

    /// <summary>The disaster risk overlay's legend.</summary>
    public const string RiskLegend = "Disaster risk: clear where the land is safe, through yellow to red where dry woods burn, low land by the water floods, steep wet slopes slide and the volcano threatens";

    /// <summary>The political control overlay's legend.</summary>
    public const string PoliticsLegend = "Political control: each village's lands in the colour of the bloc that holds it (allies share their strongest god's colour); lands of gods at war with you darkened";

    // Land this low, beside fresh water or the sea, floods; a drop this steep slides; the volcano threatens this far.
    private const float LowLand = 6f;
    private const float SteepDrop = 60f;
    private const float VolcanoReach = 450f;

    private static readonly RgbColor Few = RgbColor.ParseHex("#1d3a8a");
    private static readonly RgbColor Most = RgbColor.ParseHex("#f2c14e");
    private static readonly RgbColor Wary = RgbColor.ParseHex("#f1c40f");
    private static readonly RgbColor Perilous = RgbColor.ParseHex("#c0392b");
    private static readonly RgbColor Road = new(1f, 1f, 1f);

    /// <summary>Paints each village's lands by its people, against the largest village.</summary>
    /// <param name="world">World state.</param>
    /// <param name="opacity">Overlay opacity.</param>
    /// <param name="territory">Paints a village's lands: its region, colour, alpha.</param>
    /// <param name="region">Paints one region.</param>
    public static void PaintPopulation(WorldState world, float opacity, Action<int, RgbColor, float> territory, Action<int, RgbColor, float> region)
    {
        ArgumentNullException.ThrowIfNull(world);
        var largest = Math.Max(1, world.Settlements.Count == 0 ? 1 : world.Settlements.Max(s => s.Population));
        foreach (var settlement in world.Settlements.Where(s => s.Population > 0))
        {
            territory(settlement.RegionId, RgbColor.Lerp(Few, Most, MathF.Sqrt(settlement.Population / (float)largest)), opacity);
        }

        foreach (var group in world.Migration.Groups)
        {
            if (world.Regions.TryGetRegionIdAt(group.Position.X, group.Position.Z, out var here))
            {
                region(here, Road, opacity);
            }
        }
    }

    /// <summary>A land region's risk of disaster (0..1): fire, flood, landslide or the volcano, whichever is greatest.</summary>
    /// <param name="world">World state.</param>
    /// <param name="cell">The region.</param>
    /// <returns>Risk (0..1).</returns>
    public static float Risk(WorldState world, RegionState cell)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.Biome == Biome.Sea)
        {
            return 0f;
        }

        var dryness = 1f - Math.Clamp(cell.SoilMoisture, 0f, 1f);
        var fire = Math.Clamp(cell.Vegetation, 0f, 1f) * dryness * dryness * (1f - Math.Clamp(cell.Snow, 0f, 1f));
        var low = cell.Elevation < LowLand ? 1f - (Math.Max(0f, cell.Elevation) / LowLand) : 0f;
        var flood = low * Math.Max(Math.Clamp(cell.FreshWater, 0f, 1f), NearSea(world.Regions, cell.Id) ? 0.8f : 0f);
        var slide = HazardModel.Downhill(world.Regions, cell.Id) is { } below ? Math.Clamp(below.Drop / SteepDrop, 0f, 1f) * Math.Clamp(cell.SoilMoisture, 0f, 1f) : 0f;
        var volcano = world.Disasters.Volcano is { } mountain ? HazardModel.Falloff(world.Regions.GetRegionCenter(cell.Id), world.Regions.GetRegionCenter(mountain.RegionId), VolcanoReach) : 0f;
        return Math.Clamp(Math.Max(Math.Max(fire, flood), Math.Max(slide, volcano)), 0f, 1f);
    }

    /// <summary>Paints every land region by its risk of disaster.</summary>
    /// <param name="world">World state.</param>
    /// <param name="opacity">Overlay opacity.</param>
    /// <param name="region">Paints one region.</param>
    public static void PaintRisk(WorldState world, float opacity, Action<int, RgbColor, float> region)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var cell in world.Regions.Cells)
        {
            var risk = Risk(world, cell);
            if (risk > 0.15f)
            {
                region(cell.Id, RgbColor.Lerp(Wary, Perilous, risk), Math.Min(1f, opacity * (0.6f + (0.6f * risk))));
            }
        }
    }

    /// <summary>Paints each village's lands in the colour of the strongest god of the bloc of the god most there follow.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The local player's god.</param>
    /// <param name="view">God colours and overlay opacity.</param>
    /// <param name="territory">Paints a village's lands.</param>
    public static void PaintPolitics(WorldState world, int godId, DivineViewSettings view, Action<int, RgbColor, float> territory)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(view);
        foreach (var settlement in world.Settlements.Where(s => s.Population > 0))
        {
            if (MigrationModel.MainGod(settlement.Beliefs) is not { } main)
            {
                territory(settlement.RegionId, view.UnaffiliatedColor, 0.4f * view.OverlayOpacity);
                continue;
            }

            var leader = DiplomacyModel.BlocOf(world, main).OrderByDescending(g => GodStanding.CountFollowers(world, g)).ThenBy(g => g).First();
            var color = view.GodColor(leader);
            if (DiplomacyModel.AreAtWar(world, godId, main))
            {
                color = RgbColor.Lerp(color, new RgbColor(0f, 0f, 0f), 0.55f);
            }

            territory(settlement.RegionId, color, view.OverlayOpacity);
        }
    }

    /// <summary>Whether the sea touches a region or one beside it.</summary>
    private static bool NearSea(RegionGrid grid, int regionId)
    {
        var (column, row) = grid.GetCoordinates(regionId);
        for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
        {
            for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
            {
                if (grid.Cells[(r * grid.Columns) + c].Biome == Biome.Sea)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
