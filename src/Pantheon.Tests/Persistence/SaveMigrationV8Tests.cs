using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 8 build (format version 8) still load: the one winner becomes the list of winners.</summary>
public sealed class SaveMigrationV8Tests
{
    /// <summary>A save written by the Milestone 8 build: seed 77, two gods, an earthquake under way, three ruins and a fire, 1600 ticks.</summary>
    private static readonly string Version8SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v8.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion8SaveWithOneWinnerField_AndDisasters()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(8, root["SaveVersion"]!.GetValue<int>());
        Assert.True(world.ContainsKey("WinnerGodId"));
        Assert.False(world.ContainsKey("WinnerGodIds"));
        Assert.NotEmpty(world["Disasters"]!["Earthquakes"]!.AsArray());
    }

    [Fact]
    public void LoadVersion8_HasNoWinners_AndKeepsItsDisasters()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version8SavePath);

        Assert.Empty(world.WinnerGodIds);
        Assert.Single(world.Disasters.Earthquakes);
        Assert.Contains(world.Settlements, settlement => settlement.Ruins > 0);
        Assert.Contains(world.Regions.Cells, region => region.Fire > 0f);
    }

    [Fact]
    public void LoadVersion8_WithAWinner_KeepsItAsTheOnlyWinner()
    {
        var root = ReadFixture();
        root["World"]!["WinnerGodId"] = 2;

        var world = WorldSaveSerializer.Deserialize(root.ToJsonString());

        Assert.Equal([2], world.WinnerGodIds);
    }

    [Fact]
    public void LoadVersion8_WithoutAWinnerField_Throws()
    {
        var root = ReadFixture();
        root["World"]!.AsObject().Remove("WinnerGodId");

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("'WinnerGodId'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVersion8_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version9Expectations.Apply(expected);
        Version10Expectations.Apply(expected);
        Version11Expectations.Apply(expected);
        Version12Expectations.Apply(expected);
        Version13Expectations.Apply(expected);
        Version14Expectations.Apply(expected);
        Version15Expectations.Apply(expected);
        Version16Expectations.Apply(expected);
        Version17Expectations.Apply(expected);
        Version18Expectations.Apply(expected);
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
        Version29Expectations.Apply(expected);
        Version30Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version8SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion8_KeepsPlayingTheSameWay()
    {
        var content = TestContent.Shipped;
        var engine = content.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version8SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version8SavePath);

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_WithARepeatedWinner_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version8SavePath);
        world.WinnerGodIds.AddRange([1, 1]);

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("missing or repeated god 1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version8SavePath))!.AsObject();
}
