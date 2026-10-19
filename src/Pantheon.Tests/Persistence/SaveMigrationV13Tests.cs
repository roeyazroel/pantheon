using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Water;
using Pantheon.Game;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 15 build (format version 13) still load: a world without rivers keeps its land, and its wells are full.</summary>
public sealed class SaveMigrationV13Tests
{
    /// <summary>A save written by the Milestone 15 build at commit df01142: seed 77, the small world, 4800 ticks, with domains taken up and a holy site kept.</summary>
    private static readonly string Version13SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v13.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion13SaveWithDomains_AndNoWater()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(13, root["SaveVersion"]!.GetValue<int>());
        Assert.Contains(world["Gods"]!.AsArray(), god => god!["Domains"]!.AsArray().Count > 0);
        Assert.False(world.ContainsKey("Water"));
        Assert.False(world["Generation"]!.AsObject().ContainsKey("Hydrology"));
        Assert.All(world["Settlements"]!.AsArray(), settlement => Assert.False(settlement!.AsObject().ContainsKey("Water")));
        Assert.All(world["Regions"]!["Cells"]!.AsArray(), region => Assert.False(region!.AsObject().ContainsKey("FreshWater")));
    }

    [Fact]
    public void LoadVersion13_HasNoRiversLakesOrFloods_AndFullWells()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version13SavePath);

        Assert.Empty(world.Water.Rivers);
        Assert.Empty(world.Water.Lakes);
        Assert.Null(world.Generation.Hydrology);
        Assert.All(world.Regions.Cells, region => Assert.Equal((0f, 0f), (region.FreshWater, region.Flood)));
        Assert.All(world.Settlements, settlement => Assert.Equal(settlement.Housing * 6f, settlement.Water));
    }

    [Fact]
    public void LoadVersion13_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version14Expectations.Apply(expected);
        Version15Expectations.Apply(expected);
        Version16Expectations.Apply(expected);
        Version17Expectations.Apply(expected);
        Version18Expectations.Apply(expected);
        Version19Expectations.Apply(expected);
        Version20Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version13SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion13_InASession_KeepsItsLandWithoutRivers()
    {
        var session = GameSession.Load(TestContent.Small with { WorldGen = TestContent.RiverWorldGen }, Version13SavePath);

        Assert.Empty(session.Terrain.Hydrology.Rivers);
        Assert.Empty(session.Terrain.Hydrology.Lakes);
        Assert.Empty(session.World.Water.Rivers);
    }

    [Fact]
    public void LoadVersion13_KeepsPlayingTheSameWay()
    {
        var engine = TestContent.Small.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version13SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version13SavePath);

        engine.Run(straight, 1800);
        engine.Run(resumed, 900);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 900);

        WorldAssert.Equal(straight, resumed);
        Assert.All(straight.Settlements, settlement => Assert.True(settlement.Water > 0f));
    }

    [Fact]
    public void Load_WithBrokenWater_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version13SavePath);
        world.Water.Rivers.Add(new River { Id = 1, Name = "Silverrun", Regions = [0, 0], Level = -1f });
        world.Water.Lakes.Add(new Lake { Id = 1, Name = "Stillmere", Regions = [world.Regions.Cells.Count], SurfaceHeight = 4f });
        world.Regions.Cells[3].Flood = 2f;
        world.Settlements[0].Water = -5f;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("River 1 has a negative or invalid level", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Lake 1 has an invalid surface", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Region 3 has fresh water 0 or flood 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Settlement {world.Settlements[0].Id} has negative population, housing, food or water", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVersion13_WithWaterAlready_Throws()
    {
        var root = ReadFixture();
        root["World"]!.AsObject()["Water"] = new JsonObject { ["Rivers"] = new JsonArray(), ["Lakes"] = new JsonArray() };

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Water'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version13SavePath))!.AsObject();
}
