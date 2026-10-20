using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 50's build (format version 35) still load, their gods without journals, and play on as before.</summary>
public sealed class SaveMigrationV35Tests
{
    /// <summary>A save written by part 50's build at commit 4765b94: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version35SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v35.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion35Save_WithoutJournals()
    {
        var root = ReadFixture();

        Assert.Equal(35, root["SaveVersion"]!.GetValue<int>());
        Assert.All(root["World"]!["Gods"]!.AsArray(), god => Assert.False(god!.AsObject().ContainsKey("Journal")));
    }

    [Fact]
    public void LoadVersion35_TheGodsHaveNoJournal()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version35SavePath);

        Assert.Equal(["Ilyra", "Vael", "Seren", "Kaio"], world.Gods.Select(g => g.Name));
        Assert.All(world.Gods, god => Assert.Null(god.Journal));
    }

    [Fact]
    public void LoadVersion35_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version36Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version35SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion35_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version35SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version35SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void AJournal_SurvivesASaveAndLoad()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version35SavePath);
        world.Gods[1].Journal = "Ilyra's beast walked in my fields. I will remember.";

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal("Ilyra's beast walked in my fields. I will remember.", loaded.Gods[1].Journal);
        Assert.Null(loaded.Gods[0].Journal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version35SavePath))!.AsObject();
}
