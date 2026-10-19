using System;
using System.Globalization;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Builds the map overlays (spec §58): one RGBA pixel per region, which the client stretches over
/// the terrain. Transparent pixels leave the ground as it is. Pure and culture-invariant.
/// </summary>
public static class OverlayMap
{
    private const int BytesPerPixel = 4;

    // Settlement overlays colour the lands within this many regions of the settlement (the farmland radius).
    private const int TerritoryRadius = 2;

    private static readonly RgbColor Low = RgbColor.ParseHex("#c0392b");
    private static readonly RgbColor Middle = RgbColor.ParseHex("#f1c40f");
    private static readonly RgbColor High = RgbColor.ParseHex("#2ecc71");
    private static readonly RgbColor Parched = RgbColor.ParseHex("#8b4a1c");
    private static readonly RgbColor Enough = RgbColor.ParseHex("#e3c565");
    private static readonly RgbColor Soaked = RgbColor.ParseHex("#2f7fd8");
    private static readonly RgbColor Flames = RgbColor.ParseHex("#ff5a1f");
    private static readonly RgbColor Ash = RgbColor.ParseHex("#3a2e26");
    private static readonly RgbColor Shielded = RgbColor.ParseHex("#7fc8ff");
    private static readonly RgbColor Floodwater = RgbColor.ParseHex("#123f8c");

    /// <summary>The overlay the overlay key shows next.</summary>
    /// <param name="kind">Current overlay, or <c>null</c> when none is shown.</param>
    /// <returns>The next overlay, or <c>null</c> after the last one.</returns>
    public static OverlayKind? Next(OverlayKind? kind) => kind is { } current ? After(current) : OverlayKind.Influence;

    /// <summary>The overlay after <paramref name="kind"/>, or <c>null</c> after the last one.</summary>
    private static OverlayKind? After(OverlayKind kind) => kind switch
    {
        OverlayKind.Influence => OverlayKind.Religion,
        OverlayKind.Religion => OverlayKind.Faith,
        OverlayKind.Faith => OverlayKind.Food,
        OverlayKind.Food => OverlayKind.Weather,
        OverlayKind.Weather => OverlayKind.Danger,
        OverlayKind.Danger => OverlayKind.Migration,
        OverlayKind.Migration => OverlayKind.Culture,
        OverlayKind.Culture => null,
    };

    /// <summary>Builds an overlay.</summary>
    /// <param name="world">World state.</param>
    /// <param name="kind">Overlay.</param>
    /// <param name="godId">The local player's god.</param>
    /// <param name="rules">Divine rules, for influence and food comfort.</param>
    /// <param name="settlementRules">Settlement rules, for the soil crops need.</param>
    /// <param name="view">God colours and overlay opacity.</param>
    /// <param name="pressure">Each settlement's pressure to leave (0..1), for the migration overlay; none without it.</param>
    /// <returns>The image and its legend.</returns>
    public static OverlayImage Build(WorldState world, OverlayKind kind, int godId, DivineRules rules, SettlementRules settlementRules, DivineViewSettings view, Func<SettlementState, float>? pressure = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(view);
        var grid = world.Regions;
        var rgba = new byte[grid.Cells.Count * BytesPerPixel];
        string legend;
        switch (kind)
        {
            case OverlayKind.Influence:
                PaintInfluence(world, rules, view, rgba);
                legend = "Influence: each region in the colour of the strongest god there";
                break;
            case OverlayKind.Religion:
                PaintReligion(world, view, rgba);
                legend = "Religion: each settlement's lands in the colour of the god most follow (grey: no god)";
                break;
            case OverlayKind.Faith:
                PaintTerritories(world, rgba, view.OverlayOpacity, s => BeliefModel.Find(s, godId)?.Faith ?? 0f);
                legend = "Faith in you: red (none) to green (devout)";
                break;
            case OverlayKind.Food:
                PaintTerritories(world, rgba, view.OverlayOpacity, s => 1f - BeliefModel.AssessConditions(rules, s, Hazards.None).FoodNeed);
                legend = "Food stores: red (empty) to green (full)";
                break;
            case OverlayKind.Weather:
                PaintSoil(world, settlementRules, view.OverlayOpacity, rgba);
                legend = "Weather: the water in the soil, from brown (drought) through yellow (enough for crops) to blue (soaked); rivers and lakes in deep blue";
                break;
            case OverlayKind.Danger:
                PaintDanger(world, view.OverlayOpacity, rgba);
                legend = "Danger: fires in orange (brighter when fiercer), floods in deep blue, scorched land in ash, shielded settlements in blue";
                break;
            case OverlayKind.Migration:
                PaintMigration(world, view, pressure, rgba);
                legend = "Migration: each settlement's lands from green (content) to red (people want to leave); people on the road and their way in their god's colour";
                break;
            case OverlayKind.Culture:
                CultureOverlay.Paint(world, view.OverlayOpacity, (regionId, color, alpha) => PaintTerritory(grid, regionId, rgba, color, alpha));
                legend = CultureOverlay.Legend;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown overlay.");
        }

        return new OverlayImage(grid.Columns, grid.Rows, rgba, string.Create(CultureInfo.InvariantCulture, $"{kind} overlay - {legend}"));
    }

    /// <summary>Colours each region with the god of the strongest influence there, opacity by that influence.</summary>
    private static void PaintInfluence(WorldState world, DivineRules rules, DivineViewSettings view, byte[] rgba)
    {
        var strongest = new float[world.Regions.Cells.Count];
        foreach (var god in world.Gods)
        {
            var influence = InfluenceField.Build(rules, world, god.Id).SampleRegions(world.Regions);
            var color = view.GodColor(god.Id);
            for (var i = 0; i < influence.Length; i++)
            {
                if (influence[i] > strongest[i])
                {
                    strongest[i] = influence[i];
                    Write(rgba, i, color, influence[i] * view.OverlayOpacity);
                }
            }
        }
    }

    /// <summary>Colours every land region by its soil moisture: brown when parched, yellow when just well watered, blue when soaked.</summary>
    private static void PaintSoil(WorldState world, SettlementRules settlementRules, float opacity, byte[] rgba)
    {
        var enough = settlementRules.WellWateredSoil;
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome == Biome.Sea)
            {
                continue;
            }

            var soil = Math.Clamp(region.SoilMoisture, 0f, 1f);
            var color = soil < enough
                ? RgbColor.Lerp(Parched, Enough, soil / enough)
                : RgbColor.Lerp(Enough, Soaked, (soil - enough) / Math.Max(0.01f, 1f - enough));
            Write(rgba, region.Id, RgbColor.Lerp(color, Floodwater, 0.6f * Math.Clamp(region.FreshWater, 0f, 1f)), opacity);
        }
    }

    /// <summary>Colours burning regions by their fire, scorched ones by their scorch, and the lands of shielded settlements blue.</summary>
    private static void PaintDanger(WorldState world, float opacity, byte[] rgba)
    {
        foreach (var settlement in world.Settlements)
        {
            if (Simulation.Disasters.DisasterModel.IsShielded(settlement, world.Tick))
            {
                PaintTerritory(world.Regions, settlement.RegionId, rgba, Shielded, 0.6f * opacity);
            }
        }

        foreach (var region in world.Regions.Cells)
        {
            if (region.Fire > 0f)
            {
                Write(rgba, region.Id, Flames, (0.4f + (0.6f * region.Fire)) * opacity);
            }
            else if (region.Flood > 0.02f)
            {
                Write(rgba, region.Id, Floodwater, (0.4f + (0.6f * Math.Clamp(region.Flood, 0f, 1f))) * opacity);
            }
            else if (region.Scorch > 0.05f)
            {
                Write(rgba, region.Id, Ash, Math.Clamp(region.Scorch, 0f, 1f) * opacity);
            }
        }
    }

    /// <summary>Colours each settlement's lands by the pressure to leave, and each group's remaining way by its main god.</summary>
    private static void PaintMigration(WorldState world, DivineViewSettings view, Func<SettlementState, float>? pressure, byte[] rgba)
    {
        if (pressure is not null)
        {
            PaintTerritories(world, rgba, 0.6f * view.OverlayOpacity, settlement => 1f - pressure(settlement));
        }

        var grid = world.Regions;
        foreach (var group in world.Migration.Groups)
        {
            var color = Simulation.Migration.MigrationModel.MainGod(group.Beliefs) is { } god ? view.GodColor(god) : view.UnaffiliatedColor;
            var from = group.Position;
            foreach (var to in group.Path)
            {
                var (dx, dz) = (to.X - from.X, to.Z - from.Z);
                var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt((dx * dx) + (dz * dz)) / (grid.RegionSize * 0.5f)));
                for (var i = 0; i <= steps; i++)
                {
                    if (grid.TryGetRegionIdAt(from.X + (dx * i / steps), from.Z + (dz * i / steps), out var region))
                    {
                        Write(rgba, region, color, view.OverlayOpacity);
                    }
                }

                from = to;
            }

            if (grid.TryGetRegionIdAt(group.Position.X, group.Position.Z, out var here))
            {
                Write(rgba, here, RgbColor.Lerp(color, new RgbColor(1f, 1f, 1f), 0.5f), 1f);
            }
        }
    }

    /// <summary>Colours each settlement's lands with its main god's colour, opacity by that god's share.</summary>
    private static void PaintReligion(WorldState world, DivineViewSettings view, byte[] rgba)
    {
        foreach (var settlement in world.Settlements)
        {
            var main = SettlementLabel.MainBelief(settlement);
            var color = main is null ? view.UnaffiliatedColor : view.GodColor(main.GodId);
            var share = main is null ? 0.5f : 0.35f + (0.65f * main.Adherence);
            PaintTerritory(world.Regions, settlement.RegionId, rgba, color, share * view.OverlayOpacity);
        }
    }

    /// <summary>Colours the lands within the territory radius of a region.</summary>
    private static void PaintTerritory(RegionGrid grid, int regionId, byte[] rgba, RgbColor color, float alpha)
    {
        var (column, row) = grid.GetCoordinates(regionId);
        for (var r = Math.Max(0, row - TerritoryRadius); r <= Math.Min(grid.Rows - 1, row + TerritoryRadius); r++)
        {
            for (var c = Math.Max(0, column - TerritoryRadius); c <= Math.Min(grid.Columns - 1, column + TerritoryRadius); c++)
            {
                Write(rgba, (r * grid.Columns) + c, color, alpha);
            }
        }
    }

    /// <summary>Colours each settlement's lands on a red-to-green ramp by a value of the settlement.</summary>
    private static void PaintTerritories(WorldState world, byte[] rgba, float opacity, Func<SettlementState, float> value)
    {
        foreach (var settlement in world.Settlements)
        {
            PaintTerritory(world.Regions, settlement.RegionId, rgba, Ramp(value(settlement)), opacity);
        }
    }

    /// <summary>Red at 0, yellow at 0.5, green at 1.</summary>
    private static RgbColor Ramp(float t)
    {
        var x = Math.Clamp(t, 0f, 1f);
        return x < 0.5f ? RgbColor.Lerp(Low, Middle, x * 2f) : RgbColor.Lerp(Middle, High, (x - 0.5f) * 2f);
    }

    /// <summary>Writes one pixel.</summary>
    private static void Write(byte[] rgba, int index, RgbColor color, float alpha)
    {
        var offset = index * BytesPerPixel;
        rgba[offset] = RgbColor.ToByte(color.R);
        rgba[offset + 1] = RgbColor.ToByte(color.G);
        rgba[offset + 2] = RgbColor.ToByte(color.B);
        rgba[offset + 3] = RgbColor.ToByte(alpha);
    }
}

/// <summary>A map overlay: RGBA8 pixels, one per region in row-major order, and a legend.</summary>
/// <param name="Width">Pixels across (region columns).</param>
/// <param name="Height">Pixels down (region rows).</param>
/// <param name="Rgba">Pixel data, 4 bytes per pixel.</param>
/// <param name="Legend">What the colours mean.</param>
public sealed record OverlayImage(int Width, int Height, byte[] Rgba, string Legend);
