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
    public void GeneratedWorld_HasFullRegionGridAndSpacedSettlementsOnLand()
    {
        var settings = TestContent.Shipped.WorldGen;
        var world = WorldGenerator.Generate(Seed, settings);
        var state = world.State;
        var founding = settings.StartingSettlement;

        Assert.Equal(0, state.Tick);
        Assert.Equal(settings.RegionsPerSide, state.Regions.Columns);
        Assert.Equal(settings.RegionsPerSide, state.Regions.Rows);
        Assert.Equal(settings.HeightmapResolution / settings.RegionsPerSide * settings.VertexSpacing, state.Regions.RegionSize);
        Assert.Equal(Enumerable.Range(0, settings.RegionsPerSide * settings.RegionsPerSide), state.Regions.Cells.Select(cell => cell.Id));
        Assert.All(state.Regions.Cells, cell => Assert.Equal(0f, cell.Blessing));

        Assert.InRange(state.Settlements.Count, 2, founding.Count);
        Assert.Equal(Enumerable.Range(SettlementFounder.StartingSettlementId, state.Settlements.Count), state.Settlements.Select(s => s.Id));
        Assert.Equal(state.Settlements.Count, state.Settlements.Select(s => s.Name).Distinct().Count());
        foreach (var settlement in state.Settlements)
        {
            var region = state.Regions.GetRegion(settlement.RegionId);
            Assert.NotEqual(Biome.Sea, region.Biome);
            Assert.True(region.Fertility > 0f);
            Assert.True(world.Terrain.SampleHeight(settlement.Position.X, settlement.Position.Z) >= 1.5f);
            Assert.Equal(state.Regions.GetRegionCenter(settlement.RegionId), settlement.Position);
            Assert.Contains(settlement.Name, founding.Names);
            Assert.InRange(settlement.Population, founding.Population * (1f - founding.PopulationVariation) - 1f, founding.Population * (1f + founding.PopulationVariation) + 1f);
            Assert.Equal(founding.Housing, settlement.Housing);
            Assert.Equal(founding.Food, settlement.Food);
            Assert.Empty(settlement.Beliefs);
            foreach (var other in state.Settlements.Where(o => o.Id != settlement.Id))
            {
                var (c1, r1) = state.Regions.GetCoordinates(settlement.RegionId);
                var (c2, r2) = state.Regions.GetCoordinates(other.RegionId);
                Assert.True(Math.Max(Math.Abs(c1 - c2), Math.Abs(r1 - r2)) >= founding.MinimumSpacing);
            }
        }

        Assert.Empty(state.Gods);
    }

    [Fact]
    public void GeneratedWorld_WithPantheon_FoundsEachGodInItsHomeSettlement_FarApart()
    {
        var content = TestContent.Shipped;

        var state = WorldGenerator.Generate(Seed, content.WorldGen, content.Pantheon).State;

        Assert.Equal(content.Pantheon.Gods.Count, state.Gods.Count);
        var player = state.Gods[0];
        Assert.Equal(PantheonFounder.FirstGodId, player.Id);
        Assert.Equal(content.Pantheon.Gods[0].Name, player.Name);
        Assert.Equal(content.Pantheon.StartingDivinePower, player.DivinePower);
        var home = Assert.Single(state.Settlements[0].Beliefs);
        Assert.Equal(player.Id, home.GodId);
        Assert.Equal(content.Pantheon.HomeBelief.Adherence, home.Adherence);
        Assert.Equal(content.Pantheon.HomeBelief.Faith, home.Faith);

        var rival = state.Gods[1];
        Assert.Equal(Pantheon.Domain.Gods.GodController.Ai, rival.Controller);
        Assert.NotEqual(Pantheon.Domain.Gods.GodPersonality.None, rival.Personality);
        var rivalHome = Assert.Single(state.Settlements, s => s.Beliefs.Any(b => b.GodId == rival.Id));
        var farthest = state.Settlements.Skip(1).MaxBy(s => Distance(s.Position, state.Settlements[0].Position))!;
        Assert.Equal(farthest.Id, rivalHome.Id);
        Assert.Equal(state.Gods.Count, state.Settlements.Count(s => s.Beliefs.Count > 0));
        Assert.Empty(state.WinnerGodIds);
    }

    /// <summary>Distance between two ground positions.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    [Fact]
    public void SettlementSites_AreTheBestScoringSiteFirst()
    {
        var world = WorldGenerator.Generate(Seed, TestContent.Shipped.WorldGen);
        var founding = TestContent.Shipped.WorldGen.StartingSettlement;

        var single = SettlementFounder.SelectSites(world.State.Regions, world.Terrain, founding.CenterPreference, 1, founding.MinimumSpacing);

        Assert.Equal(world.State.Settlements[0].RegionId, Assert.Single(single));
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
