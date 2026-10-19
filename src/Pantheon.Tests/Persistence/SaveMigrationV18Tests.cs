using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 6's build (format version 18) still load, won by ascendancy alone.</summary>
public sealed class SaveMigrationV18Tests
{
    /// <summary>A save written by part 6's build at commit e4e4016: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version18SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v18.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion18SaveWithWars_AndNoVictorySettings()
    {
        var root = ReadFixture();

        Assert.Equal(18, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!["Diplomacy"]!.AsObject().ContainsKey("Wars"));
        Assert.False(root["World"]!.AsObject().ContainsKey("Victory"));
    }

    [Fact]
    public void LoadVersion18_IsWonByAscendancyAlone_WithNoOtherClocks()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version18SavePath);

        Assert.Equal(VictorySettings.Default, world.Victory);
        Assert.All(world.Gods, g => Assert.Null(g.ConquestSinceTick));
        Assert.All(world.Gods, g => Assert.Null(g.CulturalSinceTick));
    }

    [Fact]
    public void LoadVersion18_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version19Expectations.Apply(expected);
        Version20Expectations.Apply(expected);
        Version21Expectations.Apply(expected);
        Version22Expectations.Apply(expected);
        Version23Expectations.Apply(expected);
        Version24Expectations.Apply(expected);
        Version25Expectations.Apply(expected);
        Version26Expectations.Apply(expected);
        Version27Expectations.Apply(expected);
        Version28Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version18SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion18_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version18SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version18SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Gods), JsonSerializer.Serialize(resumed.Gods));
    }

    [Fact]
    public void Load_AVersion18SaveThatAlreadyHasVictorySettings_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Victory"] = new JsonObject { ["Ascendancy"] = false, ["Conquest"] = true, ["Cultural"] = false };

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Victory'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsAWorldsVictorySettings()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version18SavePath);
        world.Victory = new VictorySettings { Ascendancy = false, Cultural = true };
        world.Gods[0].CulturalSinceTick = world.Tick - 10;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(world.Victory, loaded.Victory);
        Assert.Equal(world.Tick - 10, loaded.Gods[0].CulturalSinceTick);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version18SavePath))!.AsObject();
}
