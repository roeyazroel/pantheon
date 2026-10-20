using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 39's build (format version 33) still load, their Creatures become animals.</summary>
public sealed class SaveMigrationV33Tests
{
    /// <summary>A save written by part 39's build at commit d6d24e1: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version33SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v33.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion33Save_WithTheOldSpecies()
    {
        var root = ReadFixture();

        Assert.Equal(33, root["SaveVersion"]!.GetValue<int>());
        Assert.Equal(["Lumen", "Mossback", "Stonehide", "Swiftclaw"], root["World"]!["Creatures"]!.AsArray().Select(c => c!["Species"]!.GetValue<string>()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void LoadVersion33_TheCreaturesBecomeTheAnimalsOfTheirNatures()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version33SavePath);

        Assert.Equal(["Ape", "Cow", "Tiger", "Tortoise"], world.Creatures.Select(c => c.Species).Order(StringComparer.Ordinal));
        Assert.All(world.Creatures, c => Assert.Contains(TestContent.Shipped.Creatures.Species, s => s.Name == c.Species));
    }

    [Fact]
    public void LoadVersion33_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version34Expectations.Apply(expected);
        Version35Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version33SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion33_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version33SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version33SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version33SavePath))!.AsObject();
}
