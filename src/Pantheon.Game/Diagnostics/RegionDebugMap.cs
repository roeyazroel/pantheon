using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Diagnostics;

/// <summary>
/// Builds the F3 debug map: one pixel per region, coloured by a region statistic, with settlement
/// regions and the region under the cursor marked. Pure and culture-invariant.
/// </summary>
public static class RegionDebugMap
{
    private const string MarkerKey = "white: cursor  red: settlement";
    private const int BytesPerPixel = 4;

    /// <summary>Colour of regions that hold a settlement.</summary>
    public static readonly RgbColor SettlementMarker = new(1f, 0.15f, 0.1f);

    /// <summary>Colour of the region under the cursor.</summary>
    public static readonly RgbColor CursorMarker = new(1f, 1f, 1f);

    // Viridis: dark to bright, readable with colour-vision deficiencies, and free of the marker colours.
    private static readonly RgbColor[] Ramp =
    [
        RgbColor.ParseHex("#440154"),
        RgbColor.ParseHex("#3b528b"),
        RgbColor.ParseHex("#21918c"),
        RgbColor.ParseHex("#5ec962"),
        RgbColor.ParseHex("#fde725"),
    ];

    /// <summary>The layer F3 shows next.</summary>
    /// <param name="layer">Current layer, or <c>null</c> when the map is off.</param>
    /// <returns>The next layer, or <c>null</c> after the last one (map off).</returns>
    public static RegionDebugLayer? Next(RegionDebugLayer? layer) => layer is { } current ? After(current) : RegionDebugLayer.Biome;

    /// <summary>The layer after <paramref name="layer"/> in the F3 cycle, or <c>null</c> after the last one.</summary>
    private static RegionDebugLayer? After(RegionDebugLayer layer) => layer switch
    {
        RegionDebugLayer.Biome => RegionDebugLayer.Elevation,
        RegionDebugLayer.Elevation => RegionDebugLayer.Temperature,
        RegionDebugLayer.Temperature => RegionDebugLayer.Moisture,
        RegionDebugLayer.Moisture => RegionDebugLayer.Fertility,
        RegionDebugLayer.Fertility => RegionDebugLayer.Vegetation,
        RegionDebugLayer.Vegetation => RegionDebugLayer.Blessing,
        RegionDebugLayer.Blessing => RegionDebugLayer.Influence,
        RegionDebugLayer.Influence => null,
    };

    /// <summary>A region's value on a numeric layer.</summary>
    /// <param name="region">Region to read.</param>
    /// <param name="layer">Any layer except <see cref="RegionDebugLayer.Biome"/>.</param>
    /// <param name="influence">Influence per region, for <see cref="RegionDebugLayer.Influence"/>; 0 everywhere when <c>null</c>.</param>
    /// <returns>The statistic.</returns>
    /// <exception cref="ArgumentException">The layer is <see cref="RegionDebugLayer.Biome"/>, which has no numeric value.</exception>
    public static float GetValue(RegionState region, RegionDebugLayer layer, IReadOnlyList<float>? influence = null)
    {
        ArgumentNullException.ThrowIfNull(region);
        return layer switch
        {
            RegionDebugLayer.Biome => throw new ArgumentException("The biome layer has no numeric value.", nameof(layer)),
            RegionDebugLayer.Blessing => region.Blessing,
            RegionDebugLayer.Influence => influence is not null && region.Id < influence.Count ? influence[region.Id] : 0f,
            RegionDebugLayer.Elevation => region.Elevation,
            RegionDebugLayer.Temperature => region.Temperature,
            RegionDebugLayer.Moisture => region.Moisture,
            RegionDebugLayer.Fertility => region.Fertility,
            RegionDebugLayer.Vegetation => region.Vegetation,
        };
    }

    /// <summary>Builds the map of one layer.</summary>
    /// <param name="world">World whose regions are drawn.</param>
    /// <param name="layer">Statistic that colours the regions.</param>
    /// <param name="palette">Palette that colours the biome layer.</param>
    /// <param name="cursorRegionId">Region under the cursor, if any.</param>
    /// <param name="influence">Influence per region, for <see cref="RegionDebugLayer.Influence"/>.</param>
    /// <returns>The image and its legend.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cursor region is outside the grid.</exception>
    public static RegionDebugMapImage Build(WorldState world, RegionDebugLayer layer, BiomePalette palette, int? cursorRegionId, IReadOnlyList<float>? influence = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(palette);
        var cells = world.Regions.Cells;
        if (cursorRegionId is { } cursor)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(cursor, nameof(cursorRegionId));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cursor, cells.Count, nameof(cursorRegionId));
        }

        RgbColor[] colors;
        string description;
        if (layer == RegionDebugLayer.Biome)
        {
            colors = ColorByBiome(cells, palette);
            description = "Biome: terrain colours, sea in blue";
        }
        else
        {
            var (min, max) = IsShareLayer(layer) ? (0f, 1f) : GetRange(cells, layer, influence);
            colors = ColorByValue(cells, layer, min, max, influence);
            description = Format("{0}: {1} (dark) to {2} (bright)", layer, FormatValue(layer, min), FormatValue(layer, max));
        }

        foreach (var settlement in world.Settlements)
        {
            colors[settlement.RegionId] = SettlementMarker;
        }

        // Marked last, so the cursor stays visible over a settlement.
        if (cursorRegionId is { } cursorRegion)
        {
            colors[cursorRegion] = CursorMarker;
        }

        return new RegionDebugMapImage(world.Regions.Columns, world.Regions.Rows, ToRgba(colors), description + "\n" + MarkerKey);
    }

    /// <summary>Colours each region with its biome's palette colour; the sea uses the deep water colour.</summary>
    private static RgbColor[] ColorByBiome(List<RegionState> cells, BiomePalette palette)
    {
        var colors = new RgbColor[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            colors[i] = cells[i].Biome == Biome.Sea ? palette.DeepWater : palette.GetBiomeColor(cells[i].Biome);
        }

        return colors;
    }

    /// <summary>Colours each region by where its value lies between the layer's lowest and highest value.</summary>
    private static RgbColor[] ColorByValue(List<RegionState> cells, RegionDebugLayer layer, float min, float max, IReadOnlyList<float>? influence)
    {
        var colors = new RgbColor[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            // A flat layer has no range to spread, so every region gets the middle of the ramp.
            var t = max > min ? (GetValue(cells[i], layer, influence) - min) / (max - min) : 0.5f;
            colors[i] = SampleRamp(t);
        }

        return colors;
    }

    /// <summary>Lowest and highest value of a numeric layer; (0, 0) for an empty grid.</summary>
    private static (float Min, float Max) GetRange(List<RegionState> cells, RegionDebugLayer layer, IReadOnlyList<float>? influence)
    {
        if (cells.Count == 0)
        {
            return (0f, 0f);
        }

        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        foreach (var region in cells)
        {
            var value = GetValue(region, layer, influence);
            min = MathF.Min(min, value);
            max = MathF.Max(max, value);
        }

        return (min, max);
    }

    /// <summary>Layers whose values are shares, drawn on a fixed 0..1 scale so changes over time are visible.</summary>
    private static bool IsShareLayer(RegionDebugLayer layer) => layer is RegionDebugLayer.Blessing or RegionDebugLayer.Influence;

    /// <summary>Colour at <paramref name="t"/> (0..1) along the ramp, blending linearly between its stops.</summary>
    private static RgbColor SampleRamp(float t)
    {
        var position = Math.Clamp(t, 0f, 1f) * (Ramp.Length - 1);
        var stop = Math.Min((int)position, Ramp.Length - 2);
        return RgbColor.Lerp(Ramp[stop], Ramp[stop + 1], position - stop);
    }

    /// <summary>A layer value as shown in the legend: metres for elevation, two decimals otherwise.</summary>
    private static string FormatValue(RegionDebugLayer layer, float value) =>
        layer == RegionDebugLayer.Elevation ? Format("{0:0.0} m", value) : Format("{0:0.00}", value);

    /// <summary>Packs colours into opaque RGBA8 pixels.</summary>
    private static byte[] ToRgba(RgbColor[] colors)
    {
        var rgba = new byte[colors.Length * BytesPerPixel];
        for (var i = 0; i < colors.Length; i++)
        {
            var offset = i * BytesPerPixel;
            rgba[offset] = RgbColor.ToByte(colors[i].R);
            rgba[offset + 1] = RgbColor.ToByte(colors[i].G);
            rgba[offset + 2] = RgbColor.ToByte(colors[i].B);
            rgba[offset + 3] = byte.MaxValue;
        }

        return rgba;
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
