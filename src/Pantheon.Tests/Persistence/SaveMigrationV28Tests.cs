using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Ecology;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 17's build (format version 28) still load, with no landforms, their terrain as it was.</summary>
public sealed class SaveMigrationV28Tests
{
    /// <summary>A save written by part 17's build at commit edc948f: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version28SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v28.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion28Save_WithoutLandforms()
    {
        var root = ReadFixture();

        Assert.Equal(28, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!["Generation"]!.AsObject().ContainsKey("Landforms"));
        Assert.True(root["World"]!["Creatures"]![0]!["Personality"]!.AsObject().ContainsKey("Courage"));
    }

    [Fact]
    public void LoadVersion28_HasNoLandforms_AndGrowsTheSameTerrainOfFourBiomes()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version28SavePath);
        var terrain = TerrainGenerator.Generate(world.Seed, world.Generation);

        Assert.Null(world.Generation.Landforms);
        Assert.Equal(4, world.Generation.Biomes.Count);
        Assert.All(terrain.Biomes.ToArray().Distinct(), b => Assert.Contains(b, new[] { Biome.Sea, Biome.Grassland, Biome.Forest, Biome.Highland }));
        Assert.All(world.Regions.Cells, r => Assert.Contains(r.Biome, new[] { Biome.Sea, Biome.Grassland, Biome.Forest, Biome.Highland }));
    }

    [Fact]
    public void LoadVersion28_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version29Expectations.Apply(expected);
        Version30Expectations.Apply(expected);
        Version31Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version28SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion28_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version28SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version28SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_AVersion28SaveThatAlreadyHasLandforms_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Generation"]!["Landforms"] = null;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Landforms'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheLandformsAndTheNewBiomes()
    {
        var world = Pantheon.Simulation.WorldGen.WorldGenerator.Generate(new Pantheon.Domain.World.WorldSeed(42), TestContent.BiomeWorldGen).State;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.NotNull(loaded.Generation.Landforms);
        Assert.Equal(JsonSerializer.Serialize(world.Generation), JsonSerializer.Serialize(loaded.Generation));
        Assert.Equal(world.Regions.Cells.Select(r => r.Biome), loaded.Regions.Cells.Select(r => r.Biome));
        Assert.Contains(loaded.Regions.Cells, r => r.Biome > Biome.Highland);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version28SavePath))!.AsObject();
}
