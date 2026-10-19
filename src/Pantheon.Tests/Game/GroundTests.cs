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
    public void AForestsEdge_BlendsIntoTheGrass_OverAFewMetres_NotAsAHardLine()
    {
        var field = Field((_, _) => 20f, (x, _) => x < 64 ? Biome.Grassland : Biome.Forest);

        var words = TerrainControl.Compute(field, Palette);
        float Blend(int x) => (words[(64 * field.Resolution) + x] >> 14 & 0xFF) / 255f;
        int Overlay(int x) => (int)(words[(64 * field.Resolution) + x] >> 22 & 0x1F);

        Assert.Equal(0f, Blend(40));
        Assert.Equal(0f, Blend(90));
        Assert.InRange(Blend(63), 0.25f, 0.5f);
        Assert.InRange(Blend(64), 0.25f, 0.5f);
        Assert.Equal(((int)GroundKind.ForestFloor, (int)GroundKind.Grass), (Overlay(63), Overlay(64)));
        Assert.True(Blend(61) > 0f && Blend(61) < Blend(63), "the blend fades away from the edge");
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
        var json = TestContent.ReadText(BiomePalette.FileName).Replace("      \"Ash\": [\"#26221f\", \"#5a524b\"]\n", string.Empty, StringComparison.Ordinal)
            .Replace("\"Mud\": [\"#2f2c1e\", \"#5e573a\"],", "\"Mud\": [\"#2f2c1e\", \"#5e573a\"]", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => BiomePalette.Parse(json));

        Assert.Contains("Ground.Textures needs a darker and a lighter colour for Ash", exception.Message, StringComparison.Ordinal);
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
