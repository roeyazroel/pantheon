using System;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Terrain;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.WorldGen;

/// <summary>The biomes of §5 that climate and place decide (ADR-038).</summary>
public sealed class LandformTests
{
    private const int Size = 32;
    private const float Spacing = 10f;

    private static readonly LandformSettings Landforms = new()
    {
        IslandMaxArea = 1000f,
        CoastMetres = 20f,
        CoastMaxHeight = 10f,
        CoastOver = [Biome.Grassland],
        ValleyMetres = 10f,
        ValleyMaxHeight = 40f,
        ValleyOver = [Biome.Grassland],
        VolcanoMinElevation = 60f,
        VolcanicMetres = 30f,
        Traits = TestContent.Shipped.WorldGen.Landforms!.Traits,
    };

    [Fact]
    public void Landforms_MarkTheIslet_TheVolcano_TheValley_AndTheCoast_AndLeaveTheSea()
    {
        var (heights, hydrology, biomes) = Field();

        LandformMarker.Mark(Landforms, 4, Size, Spacing, heights, hydrology, biomes);

        Assert.Equal(Biome.Island, At(biomes, 1, 1));
        Assert.Equal(Biome.Sea, At(biomes, 0, 0));
        Assert.Equal((Biome.Coast, Biome.Coast, Biome.Grassland), (At(biomes, 4, 10), At(biomes, 5, 10), At(biomes, 6, 10)));
        Assert.Equal((Biome.RiverValley, Biome.RiverValley, Biome.RiverValley, Biome.Grassland), (At(biomes, 15, 5), At(biomes, 16, 5), At(biomes, 17, 5), At(biomes, 18, 5)));
        Assert.Equal(Biome.Volcanic, At(biomes, 28, 28));
        Assert.Equal(Biome.Volcanic, At(biomes, 26, 28));
        Assert.Equal(Biome.Highland, At(biomes, 24, 24));
    }

    [Fact]
    public void TheVolcanicLand_NeedsAHighEnoughPeak()
    {
        var (heights, hydrology, biomes) = Field();

        LandformMarker.Mark(Landforms with { VolcanoMinElevation = 120f }, 4, Size, Spacing, heights, hydrology, biomes);

        Assert.DoesNotContain(Biome.Volcanic, biomes);
        Assert.Equal(Biome.Highland, At(biomes, 28, 28));
    }

    [Fact]
    public void AWorldOfEveryBiome_PutsItsVolcanoOnVolcanicLand_AndKeepsItsLand()
    {
        var biomes = new System.Collections.Generic.HashSet<Biome>();
        foreach (var seed in new ulong[] { 1, 42, 77, 1234 })
        {
            var settings = TestContent.BiomeWorldGen;
            var world = WorldGenerator.Generate(new WorldSeed(seed), settings).State;
            var four = WorldGenerator.Generate(new WorldSeed(seed), TestContent.RiverWorldGen).State;
            biomes.UnionWith(TerrainGenerator.Generate(new WorldSeed(seed), settings).Biomes.ToArray());

            if (HazardModel.FindVolcano(TestContent.Shipped.Disasters.Hazards.Volcano, world.Regions) is { } crater)
            {
                Assert.Equal(Biome.Volcanic, world.Regions.Cells[crater].Biome);
            }

            // Splitting the land among more biomes does not let the sea take the coast's regions.
            Assert.True(world.Regions.Cells.Count(r => r.Biome != Biome.Sea) >= four.Regions.Cells.Count(r => r.Biome != Biome.Sea));
        }

        Assert.Equal(Enum.GetValues<Biome>().Order(), biomes.Order());
    }

    [Fact]
    public void TestWorlds_KeepTheirFourBiomes_AndARiverValleyTakesItsTraits()
    {
        var plain = TerrainGenerator.Generate(new WorldSeed(42), TestContent.SmallWorldGen).Biomes.ToArray().Distinct();
        var valley = RegionBuilder.Traits(new BiomeClassifier(TestContent.Shipped.WorldGen.Biomes), TestContent.Shipped.WorldGen.Landforms, Biome.RiverValley);
        var desert = RegionBuilder.Traits(new BiomeClassifier(TestContent.Shipped.WorldGen.Biomes), TestContent.Shipped.WorldGen.Landforms, Biome.Desert);

        Assert.All(plain, b => Assert.Contains(b, new[] { Biome.Sea, Biome.Grassland, Biome.Forest, Biome.Highland }));
        Assert.Equal((1f, 0.65f), (valley.BaseFertility, valley.BaseVegetation));
        Assert.Equal(0.25f, desert.BaseFertility);
    }

    /// <summary>
    /// A 32 by 32 field, 10 m apart: sea along the west edge with a 2 by 2 islet, land rising east,
    /// a river down column 16, and a 90 m peak filling the south-east region.
    /// </summary>
    private static (float[] Heights, HydrologyMap Hydrology, Biome[] Biomes) Field()
    {
        var heights = new float[Size * Size];
        var surface = new float[Size * Size];
        var biomes = new Biome[Size * Size];
        for (var z = 0; z < Size; z++)
        {
            for (var x = 0; x < Size; x++)
            {
                var i = (z * Size) + x;
                heights[i] = x < 4 ? (x is 1 or 2 && z is 1 or 2 ? 3f : -5f) : x >= 24 && z >= 24 ? 90f : 5f + x;
                surface[i] = x == 16 ? heights[i] + 1f : float.NaN;
                biomes[i] = heights[i] < 0f ? Biome.Sea : heights[i] >= 70f ? Biome.Highland : Biome.Grassland;
            }
        }

        return (heights, new HydrologyMap(surface, [], []), biomes);
    }

    /// <summary>The biome of one sample.</summary>
    private static Biome At(Biome[] biomes, int x, int z) => biomes[(z * Size) + x];
}
