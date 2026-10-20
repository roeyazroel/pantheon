using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 43's build (format version 34) still load, their gods without personas, and play on as before.</summary>
public sealed class SaveMigrationV34Tests
{
    /// <summary>A save written by part 43's build at commit 6da1001: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version34SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v34.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion34Save_WithoutPersonas()
    {
        var root = ReadFixture();

        Assert.Equal(34, root["SaveVersion"]!.GetValue<int>());
        Assert.All(root["World"]!["Gods"]!.AsArray(), god => Assert.False(god!.AsObject().ContainsKey("Persona")));
    }

    [Fact]
    public void LoadVersion34_TheGodsHaveNoPersona()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version34SavePath);

        Assert.Equal(["Ilyra", "Vael", "Seren", "Kaio"], world.Gods.Select(g => g.Name));
        Assert.All(world.Gods, god => Assert.Null(god.Persona));
    }

    [Fact]
    public void LoadVersion34_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version35Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version34SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion34_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version34SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version34SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version34SavePath))!.AsObject();
}
