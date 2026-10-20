using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 26's build (format version 32) still load, every village's houses whole.</summary>
public sealed class SaveMigrationV32Tests
{
    /// <summary>A save written by part 26's build at commit b26b69b: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version32SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v32.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion32Save_WithoutIntegrity()
    {
        var root = ReadFixture();

        Assert.Equal(32, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!.AsObject().ContainsKey("Land"));
        Assert.False(root["World"]!["Settlements"]![0]!.AsObject().ContainsKey("Integrity"));
    }

    [Fact]
    public void LoadVersion32_EveryVillagesHousesAreWhole()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version32SavePath);

        Assert.All(world.Settlements, s => Assert.Equal(1f, s.Integrity));
    }

    [Fact]
    public void LoadVersion32_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version33Expectations.Apply(expected);
        Version34Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version32SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion32_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version32SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version32SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_AVersion32SaveWhoseVillageAlreadyHasIntegrity_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Settlements"]![0]!["Integrity"] = 1;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Integrity'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsCrackedHouses_AndRejectsIntegrityOutsideItsRange()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version32SavePath);
        world.Settlements[0].Integrity = 0.4f;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(0.4f, loaded.Settlements[0].Integrity);
        world.Settlements[0].Integrity = 1.5f;
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version32SavePath))!.AsObject();
}
