using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The ground's textures (spec §61, ADR-051): Terrain3D's control map and tint.</summary>
public sealed class GroundTests
{
    private static readonly BiomePalette Palette = TestContent.Palette;

    [Fact]
    public void TheControlWord_PacksTheTexturesBlendAndTurn_AsTerrain3DReadsThem()
    {
        var word = TerrainControl.Encode(new TerrainControl.Ground(GroundKind.ForestFloor, GroundKind.Rock, 0.5f), 9);

        Assert.Equal((int)GroundKind.ForestFloor, (int)(word >> 27 & 0x1F));
        Assert.Equal((int)GroundKind.Rock, (int)(word >> 22 & 0x1F));
        Assert.Equal(128u, word >> 14 & 0xFF);
        Assert.Equal(9u, word >> 10 & 0xF);
        Assert.Equal(0u, word & 0x3FF);
    }

    [Fact]
    public void EachBiome_HasItsGround_SandNearTheSea_SteepSlopesRock_AndColdHeightsSnow()
    {
        Assert.All(Enum.GetValues<Biome>(), biome => Assert.True(Enum.IsDefined(TerrainControl.BaseOf(biome))));
        Assert.Equal(
            [GroundKind.Grass, GroundKind.ForestFloor, GroundKind.Sand, GroundKind.Mud, GroundKind.Moss, GroundKind.Ash],
            new[] { Biome.Grassland, Biome.Forest, Biome.Desert, Biome.Swamp, Biome.Tundra, Biome.Volcanic }.Select(TerrainControl.BaseOf));

        var field = Field((x, z) => x < 20 ? 1f : x < 60 ? 20f : x < 80 ? 20f + ((x - 60) * 4f) : 300f, (_, _) => Biome.Grassland);
        var beach = TerrainControl.At(field, Palette, 10, 50);
        var meadow = TerrainControl.At(field, Palette, 40, 50);
        var cliff = TerrainControl.At(field, Palette, 70, 50);
        var peak = TerrainControl.At(field, Palette, 100, 50);

        Assert.Equal((GroundKind.Grass, GroundKind.Sand), (beach.Base, beach.Overlay));
        Assert.InRange(beach.Blend, 0.6f, 0.75f);
        Assert.Equal(GroundKind.Sand, TerrainControl.At(Field((_, _) => -3f, (_, _) => Biome.Sea), Palette, 10, 10).Base);
        Assert.Equal((GroundKind.Grass, 0f), (meadow.Base, meadow.Blend));
        Assert.Equal((GroundKind.Rock, 1f), (cliff.Overlay, cliff.Blend));
        Assert.Equal((GroundKind.Snow, 1f), (peak.Overlay, peak.Blend));
    }

    [Fact]
    public void ABiomesBorder_Wanders_AndBlendsOverManyMetres_NotAsAHardLine()
    {
        var field = Field((_, _) => 20f, (x, _) => x < 64 ? Biome.Grassland : Biome.Forest);

        var words = TerrainControl.Compute(field, Palette);
        var n = field.Resolution;
        int Base(int x, int z) => (int)(words[(z * n) + x] >> 27 & 0x1F);
        float Blend(int x, int z) => (words[(z * n) + x] >> 14 & 0xFF) / 255f;
        var edges = Enumerable.Range(0, n).Select(z => Enumerable.Range(0, n).First(x => Base(x, z) != (int)GroundKind.Grass)).ToList();

        Assert.All(Enumerable.Range(0, n), z => Assert.Equal(0f, Blend(4, z)));
        Assert.True(edges.Max() - edges.Min() >= 6, "the border wanders by many metres from row to row");
        Assert.All(Enumerable.Range(0, n), z => Assert.InRange(Blend(edges[z] - 1, z), 0.05f, 0.51f));
        Assert.True(Enumerable.Range(0, n).Average(z => Blend(edges[z] - 6, z)) < Enumerable.Range(0, n).Average(z => Blend(edges[z] - 1, z)), "the blend fades away from the edge");
    }

    [Fact]
    public void GrassShowsThroughAForestsLitter_InPatches_ButTheSameLandIsAlwaysTheSame()
    {
        var field = Field((_, _) => 20f, (_, _) => Biome.Forest);

        var words = TerrainControl.Compute(field, Palette);
        var grass = words.Count(word => (word >> 27 & 0x1F) == (uint)GroundKind.Grass);

        Assert.InRange(grass / (float)words.Length, 0.1f, 0.8f);
        Assert.Equal(words, TerrainControl.Compute(field, Palette));
    }

    [Fact]
    public void TheTint_KeepsTheTexturesBrightness_AndTurnsThemOnlyGentlyTowardsTheBiomesHue()
    {
        byte[] rgba = [126, 164, 74, 128, 238, 241, 244, 128];

        TerrainControl.Tint(rgba, Palette.Ground);

        Assert.True(rgba[1] > rgba[0] && rgba[1] > rgba[2], "grass stays greenish");
        Assert.InRange((0.2126f * rgba[0]) + (0.7152f * rgba[1]) + (0.0722f * rgba[2]), 235f, 260f);
        Assert.InRange(rgba[1] - rgba[2], 1, 90);
        Assert.All(rgba.Skip(4).Take(3), b => Assert.InRange(b, 245, 255));
        Assert.Equal(128, rgba[3]);
    }

    [Fact]
    public void TheGround_NeedsColoursForEveryTexture()
    {
        var json = TestContent.ReadText(BiomePalette.FileName).Replace("      \"Ash\": [\"#3a3431\", \"#6e645c\"]\n", string.Empty, StringComparison.Ordinal)
            .Replace("\"Mud\": [\"#3b3725\", \"#6a6242\"],", "\"Mud\": [\"#3b3725\", \"#6a6242\"]", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => BiomePalette.Parse(json));

        Assert.Contains("Ground.Textures needs a darker and a lighter colour for Ash", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLandsShade_DarkensHollows_LightensRidges_AndIsTheSameEachTime()
    {
        // A valley along the middle, its floor 6 m below the level land.
        var field = Field((x, _) => 20f - (6f * MathF.Exp(-MathF.Pow((x - 64f) / 6f, 2f))) + (6f * MathF.Exp(-MathF.Pow((x - 20f) / 6f, 2f))), (_, _) => Biome.Grassland);
        var first = Shaded(field);
        float Light(int x) => Enumerable.Range(0, field.Resolution).Average(z => (float)first[((z * field.Resolution) + x) * 4]);

        Assert.True(Light(64) < Light(100) * 0.9f, "a hollow is darker than the level land");
        Assert.True(Light(20) >= Light(100), "a ridge is no darker than the level land");
        Assert.Equal(first, Shaded(field));
    }

    [Fact]
    public void TheSeaFloor_KeepsItsLevelShade_AndWetSandDarkensAtTheWatersEdge()
    {
        var field = Field((x, _) => x < 40 ? -4f : (x - 40) * 0.1f, (x, _) => x < 40 ? Biome.Sea : Biome.Grassland);
        var shaded = Shaded(field);
        float Light(int x) => Enumerable.Range(0, field.Resolution).Average(z => (float)shaded[((z * field.Resolution) + x) * 4]);

        Assert.Equal(200f * Palette.Ground.Shade.Neutral, Light(10), 0);
        Assert.True(Light(42) < Light(60) * 0.9f, "wet ground at the water's edge is darker");
    }

    /// <summary>A grey colour map over the land, shaded.</summary>
    private static byte[] Shaded(TerrainField field)
    {
        var rgba = new byte[field.Resolution * field.Resolution * 4];
        Array.Fill(rgba, (byte)200);
        GroundShade.Apply(rgba, field, Palette);
        return rgba;
    }

    /// <summary>A 129-sample square terrain, 2 m apart, of the given heights and biomes.</summary>
    private static TerrainField Field(Func<int, int, float> height, Func<int, int, Biome> biome)
    {
        const int side = 129;
        var count = side * side;
        return new TerrainField(
            side,
            2f,
            Enumerable.Range(0, count).Select(i => height(i % side, i / side)).ToArray(),
            new float[count],
            new float[count],
            Enumerable.Range(0, count).Select(i => biome(i % side, i / side)).ToArray());
    }
}
