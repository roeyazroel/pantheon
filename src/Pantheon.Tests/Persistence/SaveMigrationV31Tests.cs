using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Land;
using Pantheon.Persistence;
using Pantheon.Simulation.Land;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 23's build (format version 31) still load, the land as the seed made it.</summary>
public sealed class SaveMigrationV31Tests
{
    /// <summary>A save written by part 23's build at commit 6757297: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version31SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v31.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion31Save_WithoutReshapedLand()
    {
        var root = ReadFixture();

        Assert.Equal(31, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!.AsObject().ContainsKey("Villagers"));
        Assert.False(root["World"]!.AsObject().ContainsKey("Land"));
    }

    [Fact]
    public void LoadVersion31_TheLandIsAsTheSeedMadeIt()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version31SavePath);

        Assert.Empty(world.Land.Reshapes);
        Assert.Equal(1, world.Land.NextReshapeId);
    }

    [Fact]
    public void LoadVersion31_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version32Expectations.Apply(expected);
        Version33Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version31SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion31_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version31SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version31SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_AVersion31SaveThatAlreadyHasLand_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Land"] = new JsonObject { ["Reshapes"] = new JsonArray(), ["NextReshapeId"] = 1 };

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Land'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsTheReshapedLand_WhoseTerrainIsMadeAgain_AndRejectsAnUnknownGod()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version31SavePath);
        var terrain = TerrainGenerator.Generate(world.Seed, world.Generation);
        var island = Lands.Find(world, terrain, ReshapeKind.Island, world.Settlements[0].Position)!;
        terrain = LandShaper.Apply(world, terrain, island);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        WorldAssert.Equal(world, loaded);
        Assert.True(TerrainGenerator.Generate(loaded.Seed, loaded.Generation, loaded.Land.Reshapes).Heights.SequenceEqual(terrain.Heights));
        var json = JsonNode.Parse(WorldSaveSerializer.Serialize(world))!;
        json["World"]!["Land"]!["Reshapes"]![0]!["GodId"] = 99;
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(json.ToJsonString()));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version31SavePath))!.AsObject();
}
