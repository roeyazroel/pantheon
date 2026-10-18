using System;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The F3 debug map shows every region's statistic, the settlements and the cursor.</summary>
public sealed class RegionDebugMapTests
{
    [Fact]
    public void Next_CyclesThroughEveryLayerThenTurnsTheMapOff()
    {
        var visited = new[] { RegionDebugMap.Next(null) }.ToList();
        while (visited[^1] is { } layer)
        {
            visited.Add(RegionDebugMap.Next(layer));
        }

        Assert.Equal([.. Enum.GetValues<RegionDebugLayer>().Cast<RegionDebugLayer?>(), null], visited);
    }

    [Fact]
    public void Build_DrawsOnePixelPerRegionInGridOrder()
    {
        var world = CreateWorld();
        world.Settlements.Clear();

        var image = RegionDebugMap.Build(world, RegionDebugLayer.Biome, TestContent.Palette, cursorRegionId: null);

        Assert.Equal(world.Regions.Columns, image.Width);
        Assert.Equal(world.Regions.Rows, image.Height);
        Assert.Equal(world.Regions.Cells.Count * 4, image.Rgba.Length);
        Assert.Contains(world.Regions.Cells, region => region.Biome == Biome.Sea);
        Assert.Contains(world.Regions.Cells, region => region.Biome != Biome.Sea);
        foreach (var region in world.Regions.Cells)
        {
            var expected = region.Biome == Biome.Sea ? TestContent.Palette.DeepWater : TestContent.Palette.GetBiomeColor(region.Biome);
            Assert.Equal(Bytes(expected), PixelAt(image, region.Id));
            Assert.Equal(byte.MaxValue, image.Rgba[(region.Id * 4) + 3]);
        }

        Assert.StartsWith("Biome:", image.Legend, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RegionDebugLayer.Elevation, "0.0", " m")]
    [InlineData(RegionDebugLayer.Temperature, "0.00", "")]
    [InlineData(RegionDebugLayer.Moisture, "0.00", "")]
    [InlineData(RegionDebugLayer.Fertility, "0.00", "")]
    [InlineData(RegionDebugLayer.Vegetation, "0.00", "")]
    public void Build_SpreadsTheRampFromTheLowestToTheHighestValue(RegionDebugLayer layer, string numberFormat, string unit)
    {
        var world = CreateWorld();
        world.Settlements.Clear();
        var byValue = world.Regions.Cells.OrderBy(region => RegionDebugMap.GetValue(region, layer)).ToList();
        var (lowest, highest) = (byValue[0], byValue[^1]);
        string Show(RegionState region) => RegionDebugMap.GetValue(region, layer).ToString(numberFormat, CultureInfo.InvariantCulture) + unit;

        var image = RegionDebugMap.Build(world, layer, TestContent.Palette, cursorRegionId: null);

        Assert.Equal(Bytes(RgbColor.ParseHex("#440154")), PixelAt(image, lowest.Id));
        Assert.Equal(Bytes(RgbColor.ParseHex("#fde725")), PixelAt(image, highest.Id));
        Assert.Equal($"{layer}: {Show(lowest)} (dark) to {Show(highest)} (bright)", image.Legend.Split('\n')[0]);
    }

    [Fact]
    public void Build_DrawsAFlatLayerInTheMiddleOfTheRamp()
    {
        var world = CreateWorld();
        world.Settlements.Clear();
        foreach (var region in world.Regions.Cells)
        {
            region.Vegetation = 0.3f;
        }

        var image = RegionDebugMap.Build(world, RegionDebugLayer.Vegetation, TestContent.Palette, cursorRegionId: null);

        Assert.All(world.Regions.Cells, region => Assert.Equal(Bytes(RgbColor.ParseHex("#21918c")), PixelAt(image, region.Id)));
        Assert.Equal("Vegetation: 0.30 (dark) to 0.30 (bright)", image.Legend.Split('\n')[0]);
    }

    [Fact]
    public void Build_MarksSettlementsAndTheCursorWhichWinsOverASettlement()
    {
        var world = CreateWorld();
        var settlementRegion = world.Settlements[0].RegionId;
        var otherRegion = settlementRegion == 0 ? 1 : 0;

        var cursorElsewhere = RegionDebugMap.Build(world, RegionDebugLayer.Fertility, TestContent.Palette, otherRegion);
        var cursorOnSettlement = RegionDebugMap.Build(world, RegionDebugLayer.Fertility, TestContent.Palette, settlementRegion);

        Assert.Equal(Bytes(RegionDebugMap.SettlementMarker), PixelAt(cursorElsewhere, settlementRegion));
        Assert.Equal(Bytes(RegionDebugMap.CursorMarker), PixelAt(cursorElsewhere, otherRegion));
        Assert.Equal(Bytes(RegionDebugMap.CursorMarker), PixelAt(cursorOnSettlement, settlementRegion));
        Assert.Equal("white: cursor  red: settlement", cursorElsewhere.Legend.Split('\n')[1]);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RegionDebugMap.Build(world, RegionDebugLayer.Fertility, TestContent.Palette, world.Regions.Cells.Count));
    }

    /// <summary>A small generated world with its starting settlement.</summary>
    private static WorldState CreateWorld() => WorldGenerator.Generate(new WorldSeed(1234), TestContent.SmallWorldGen).State;

    /// <summary>The RGB bytes of one region's pixel.</summary>
    private static (byte R, byte G, byte B) PixelAt(RegionDebugMapImage image, int regionId) =>
        (image.Rgba[regionId * 4], image.Rgba[(regionId * 4) + 1], image.Rgba[(regionId * 4) + 2]);

    /// <summary>The RGB bytes a colour is drawn with.</summary>
    private static (byte R, byte G, byte B) Bytes(RgbColor color) =>
        (RgbColor.ToByte(color.R), RgbColor.ToByte(color.G), RgbColor.ToByte(color.B));
}
