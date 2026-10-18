using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.WorldGen;

/// <summary>World generation is reproducible from the seed and produces a usable starting world.</summary>
public sealed class WorldGenerationTests
{
    private static readonly WorldSeed Seed = new(1234);

    [Fact]
    public void SameSeed_ProducesIdenticalWorld()
    {
        var first = WorldGenerator.Generate(Seed, TestContent.Shipped.WorldGen);
        var second = WorldGenerator.Generate(Seed, TestContent.Shipped.WorldGen);

        WorldAssert.Equal(first.State, second.State);
        WorldAssert.Equal(first.Terrain, second.Terrain);
        Assert.Equal(WorldSaveSerializer.Serialize(first.State), WorldSaveSerializer.Serialize(second.State));
    }

    [Fact]
    public void SameTextSeed_ProducesIdenticalWorld()
    {
        var first = WorldGenerator.Generate(WorldSeed.Parse("first light"), TestContent.SmallWorldGen);
        var second = WorldGenerator.Generate(WorldSeed.Parse("  first light "), TestContent.SmallWorldGen);

        WorldAssert.Equal(first.State, second.State);
        WorldAssert.Equal(first.Terrain, second.Terrain);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentWorlds()
    {
        var first = WorldGenerator.Generate(new WorldSeed(1), TestContent.SmallWorldGen);
        var second = WorldGenerator.Generate(new WorldSeed(2), TestContent.SmallWorldGen);

        Assert.False(first.Terrain.Heights.SequenceEqual(second.Terrain.Heights));
        Assert.NotEqual(WorldSaveSerializer.Serialize(first.State), WorldSaveSerializer.Serialize(second.State));
    }

    [Fact]
    public void GeneratedWorld_HasFullRegionGridAndOneSettlementOnLand()
    {
        var settings = TestContent.Shipped.WorldGen;
        var world = WorldGenerator.Generate(Seed, settings);
        var state = world.State;

        Assert.Equal(0, state.Tick);
        Assert.Equal(settings.RegionsPerSide, state.Regions.Columns);
        Assert.Equal(settings.RegionsPerSide, state.Regions.Rows);
        Assert.Equal(settings.HeightmapResolution / settings.RegionsPerSide * settings.VertexSpacing, state.Regions.RegionSize);
        Assert.Equal(Enumerable.Range(0, settings.RegionsPerSide * settings.RegionsPerSide), state.Regions.Cells.Select(cell => cell.Id));

        var settlement = Assert.Single(state.Settlements);
        var region = state.Regions.GetRegion(settlement.RegionId);
        Assert.NotEqual(Biome.Sea, region.Biome);
        Assert.True(region.Fertility > 0f);
        Assert.True(world.Terrain.SampleHeight(settlement.Position.X, settlement.Position.Z) >= 1.5f);
        Assert.Equal(state.Regions.GetRegionCenter(settlement.RegionId), settlement.Position);
        Assert.Contains(settlement.Name, settings.StartingSettlement.Names);
        Assert.Equal(settings.StartingSettlement.Population, settlement.Population);
        Assert.Equal(settings.StartingSettlement.Housing, settlement.Housing);
        Assert.Equal(settings.StartingSettlement.Food, settlement.Food);
    }

    [Fact]
    public void GeneratedWorld_ContainsSeaAndAllThreeLandBiomes()
    {
        var world = WorldGenerator.Generate(Seed, TestContent.Shipped.WorldGen);

        var sampleBiomes = world.Terrain.Biomes.ToArray().Distinct().ToHashSet();
        var regionBiomes = world.State.Regions.Cells.Select(cell => cell.Biome).Distinct().ToHashSet();
        foreach (var biome in new[] { Biome.Sea, Biome.Grassland, Biome.Forest, Biome.Highland })
        {
            Assert.Contains(biome, sampleBiomes);
            Assert.Contains(biome, regionBiomes);
        }
    }

    [Fact]
    public void GeneratedWorld_IsAnIslandSurroundedBySea()
    {
        var terrain = WorldGenerator.Generate(Seed, TestContent.Shipped.WorldGen).Terrain;
        var last = terrain.Resolution - 1;

        for (var i = 0; i <= last; i++)
        {
            Assert.True(terrain.GetHeight(i, 0) < 0f);
            Assert.True(terrain.GetHeight(i, last) < 0f);
            Assert.True(terrain.GetHeight(0, i) < 0f);
            Assert.True(terrain.GetHeight(last, i) < 0f);
        }

        var landShare = terrain.Heights.ToArray().Count(height => height >= 0f) / (double)terrain.Heights.Length;
        Assert.InRange(landShare, 0.15, 0.6);
    }

    [Fact]
    public void RegionBuilder_DerivesTraitsFromBiomeRules()
    {
        var world = WorldGenerator.Generate(Seed, TestContent.SmallWorldGen);

        foreach (var region in world.State.Regions.Cells)
        {
            Assert.InRange(region.Fertility, 0f, 1f);
            Assert.InRange(region.Vegetation, 0f, 1f);
            Assert.InRange(region.Moisture, 0f, 1f);
            Assert.InRange(region.Temperature, 0f, 1f);
            if (region.Biome == Biome.Sea)
            {
                Assert.Equal(0f, region.Fertility);
                Assert.Equal(0f, region.Vegetation);
            }
        }
    }

    [Fact]
    public void Validator_ReportsEveryInvalidSetting()
    {
        var settings = TestContent.SmallWorldGen with
        {
            HeightmapResolution = 250,
            VertexSpacing = 0f,
        };

        var exception = Assert.Throws<ArgumentException>(() => WorldGenSettingsValidator.Validate(settings));
        Assert.Contains("divisible", exception.Message);
        Assert.Contains("VertexSpacing", exception.Message);
    }
}
