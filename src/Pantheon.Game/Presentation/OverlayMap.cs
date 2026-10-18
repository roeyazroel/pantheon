using System;
using System.Globalization;
using Pantheon.Domain.Belief;
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

    /// <summary>The overlay the overlay key shows next.</summary>
    /// <param name="kind">Current overlay, or <c>null</c> when none is shown.</param>
    /// <returns>The next overlay, or <c>null</c> after the last one.</returns>
    public static OverlayKind? Next(OverlayKind? kind) => kind is { } current ? After(current) : OverlayKind.Influence;

    /// <summary>The overlay after <paramref name="kind"/>, or <c>null</c> after the last one.</summary>
    private static OverlayKind? After(OverlayKind kind) => kind switch
    {
        OverlayKind.Influence => OverlayKind.Faith,
        OverlayKind.Faith => OverlayKind.Food,
        OverlayKind.Food => null,
    };

    /// <summary>Builds an overlay.</summary>
    /// <param name="world">World state.</param>
    /// <param name="kind">Overlay.</param>
    /// <param name="godId">The local player's god.</param>
    /// <param name="rules">Divine rules, for influence and food comfort.</param>
    /// <param name="view">God colours and overlay opacity.</param>
    /// <returns>The image and its legend.</returns>
    public static OverlayImage Build(WorldState world, OverlayKind kind, int godId, DivineRules rules, DivineViewSettings view)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(view);
        var grid = world.Regions;
        var rgba = new byte[grid.Cells.Count * BytesPerPixel];
        string legend;
        switch (kind)
        {
            case OverlayKind.Influence:
                var influence = InfluenceField.Build(rules, world, godId).SampleRegions(grid);
                var color = view.GodColor(godId);
                for (var i = 0; i < influence.Length; i++)
                {
                    Write(rgba, i, color, influence[i] * view.OverlayOpacity);
                }

                legend = "Influence: brighter where your god holds more sway";
                break;
            case OverlayKind.Faith:
                PaintTerritories(world, rgba, view.OverlayOpacity, s => BeliefModel.Find(s, godId)?.Faith ?? 0f);
                legend = "Faith in you: red (none) to green (devout)";
                break;
            case OverlayKind.Food:
                PaintTerritories(world, rgba, view.OverlayOpacity, s => 1f - BeliefModel.AssessConditions(rules, s).FoodNeed);
                legend = "Food stores: red (empty) to green (full)";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown overlay.");
        }

        return new OverlayImage(grid.Columns, grid.Rows, rgba, string.Create(CultureInfo.InvariantCulture, $"{kind} overlay - {legend}"));
    }

    /// <summary>Colours each settlement's lands on a red-to-green ramp by a value of the settlement.</summary>
    private static void PaintTerritories(WorldState world, byte[] rgba, float opacity, Func<Domain.Settlements.SettlementState, float> value)
    {
        var grid = world.Regions;
        foreach (var settlement in world.Settlements)
        {
            var color = Ramp(value(settlement));
            var (column, row) = grid.GetCoordinates(settlement.RegionId);
            for (var r = Math.Max(0, row - TerritoryRadius); r <= Math.Min(grid.Rows - 1, row + TerritoryRadius); r++)
            {
                for (var c = Math.Max(0, column - TerritoryRadius); c <= Math.Min(grid.Columns - 1, column + TerritoryRadius); c++)
                {
                    Write(rgba, (r * grid.Columns) + c, color, opacity);
                }
            }
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
