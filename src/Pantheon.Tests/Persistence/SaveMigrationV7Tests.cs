using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 7 build (format version 7) still load: nothing burns, shakes or lies in ruins.</summary>
public sealed class SaveMigrationV7Tests
{
    /// <summary>A save written by the Milestone 7 build: seed 77, two gods, weather with a rain cloud the player called, 2100 ticks.</summary>
    private static readonly string Version7SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v7.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion7SaveWithWeather_AndNoDisasters()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(7, root["SaveVersion"]!.GetValue<int>());
        Assert.NotEmpty(world["Weather"]!["Cells"]!.AsArray());
        Assert.False(world.ContainsKey("Disasters"));
        Assert.False(world["Settlements"]![0]!.AsObject().ContainsKey("Ruins"));
    }

    [Fact]
    public void LoadVersion7_NothingBurnsShakesOrLiesInRuins()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version7SavePath);

        Assert.Empty(world.Disasters.Earthquakes);
        Assert.Equal(1, world.Disasters.NextEarthquakeId);
        Assert.All(world.Regions.Cells, region => Assert.Equal((0f, 0f), (region.Fire, region.Scorch)));
        Assert.All(world.Settlements, settlement => Assert.Equal((0, 0L), (settlement.Ruins, settlement.ShieldedUntilTick)));
        Assert.NotEmpty(world.Weather.Cells);
    }

    [Fact]
    public void LoadVersion7_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version8Expectations.Apply(expected);
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version7SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion7_KeepsPlayingTheSameWay_DisastersIncluded()
    {
        var content = TestContent.Shipped;
        var engine = content.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version7SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version7SavePath);
        DisasterModel.StartQuake(content.Disasters, straight.Disasters, straight.Settlements[0].Position, 6.5f, straight.Tick, null);
        DisasterModel.StartQuake(content.Disasters, resumed.Disasters, resumed.Settlements[0].Position, 6.5f, resumed.Tick, null);

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_WithABrokenQuakeFireOrRuins_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version7SavePath);
        DisasterModel.StartQuake(DisasterWorlds.Rules, world.Disasters, new GroundPosition(10f, 10f), 6f, world.Tick, godId: 99);
        world.Regions.Cells[4].Fire = 1.5f;
        world.Settlements[0].Ruins = -1;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
        Assert.Contains("Earthquake 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Region 4 has fire 1.5", exception.Message, StringComparison.Ordinal);
        Assert.Contains("negative construction progress, ruins or shield", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version7SavePath))!.AsObject();
}
