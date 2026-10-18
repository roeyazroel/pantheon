using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The shipped data files parse strictly and pass validation.</summary>
public sealed class ContentTests
{
    [Fact]
    public void ShippedContent_Parses()
    {
        var content = TestContent.Shipped;

        Assert.Equal(1024, content.WorldGen.HeightmapResolution);
        Assert.Equal(32, content.WorldGen.RegionsPerSide);
        Assert.True(content.WorldGen.Biomes[^1].IsCatchAll());
        Assert.Equal(new[] { Biome.Sea, Biome.Highland, Biome.Forest, Biome.Grassland }, content.WorldGen.Biomes.Select(rule => rule.Biome));
        Assert.NotEmpty(content.WorldGen.StartingSettlement.Names);
    }

    [Fact]
    public void ShippedPalette_Parses()
    {
        var palette = TestContent.Palette;

        Assert.Equal(RgbColor.ParseHex(palette.Grassland.ToHex()), palette.Grassland);
        Assert.NotEqual(palette.Grassland, palette.Forest);
    }

    [Fact]
    public void Content_WithUnknownMember_Throws()
    {
        var json = TestContent.ReadText(ContentJson.SettlementRulesFileName).Replace("\"WorkforceShare\"", "\"WorkForceShare\"", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => ContentJson.ParseSettlementRules(json));
        Assert.Contains(ContentJson.SettlementRulesFileName, exception.Message);
    }

    [Fact]
    public void Content_WithOutOfRangeValue_FailsValidation()
    {
        var rules = TestContent.ReadText(ContentJson.SettlementRulesFileName).Replace("\"WorkforceShare\": 0.5", "\"WorkforceShare\": 2.5", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(TestContent.ReadText(ContentJson.WorldGenFileName), rules));
        Assert.Contains("WorkforceShare", exception.Message);
    }

    [Fact]
    public void Content_WithInvalidColour_Throws()
    {
        var json = TestContent.ReadText(BiomePalette.FileName).Replace("\"#7ea44a\"", "\"green\"", StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => BiomePalette.Parse(json));
    }

    [Fact]
    public void Palette_WithOutOfRangeThreshold_FailsValidation()
    {
        var json = TestContent.ReadText(BiomePalette.FileName).Replace("\"ColorVariation\": 0.05", "\"ColorVariation\": 0.9", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => BiomePalette.Parse(json));
        Assert.Contains(BiomePalette.FileName, exception.Message);
        Assert.Contains("ColorVariation", exception.Message);
    }
}
