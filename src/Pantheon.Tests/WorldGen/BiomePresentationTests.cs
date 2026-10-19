using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Book;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.WorldGen;

/// <summary>How §5's biomes look and are named (ADR-038).</summary>
public sealed class BiomePresentationTests
{
    [Fact]
    public void EveryBiome_HasItsOwnColour_AndName()
    {
        var palette = TestContent.Palette;
        var biomes = Enum.GetValues<Biome>();

        Assert.Equal(biomes.Length, biomes.Select(palette.GetBiomeColor).Distinct().Count());
        Assert.Equal(biomes.Length, biomes.Select(BiomeText.Name).Distinct().Count());
        Assert.Equal("river valley", BiomeText.Name(Biome.RiverValley));
    }

    [Fact]
    public void TheTerrain_IsColouredByItsNewBiomes()
    {
        var terrain = TerrainGenerator.Generate(new WorldSeed(42), TestContent.BiomeWorldGen);
        var biomes = terrain.Biomes.ToArray();
        var volcanic = Array.IndexOf(biomes, Biome.Volcanic);
        var (x, z) = (volcanic % terrain.Resolution, volcanic / terrain.Resolution);

        var color = TerrainColorizer.ComputeColor(terrain, TestContent.Palette, x, z);

        Assert.True(volcanic >= 0);
        Assert.True(color.R < TestContent.Palette.Grassland.R && color.G < TestContent.Palette.Grassland.G, "volcanic ash is darker than grass");
    }

    [Fact]
    public void TheBook_CountsTheLandByBiome()
    {
        var session = GameSession.CreateNew(TestContent.Small with { WorldGen = TestContent.BiomeWorldGen }, new WorldSeed(42));

        var census = BiomeText.Census(session.World.Regions);
        var page = BookReadout.Build(session, BookTab.World);

        Assert.Contains("grassland", census, StringComparison.Ordinal);
        Assert.Contains("%", census, StringComparison.Ordinal);
        Assert.Contains(page.Lines, l => l.Text == "The land: " + census + ".");
        Assert.DoesNotContain("sea", census, StringComparison.Ordinal);
    }
}
