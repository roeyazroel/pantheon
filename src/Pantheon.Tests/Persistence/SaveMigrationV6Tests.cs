using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 6 build (format version 6) still load, under clear skies, with their climate's water in the soil.</summary>
public sealed class SaveMigrationV6Tests
{
    /// <summary>A save written by the Milestone 6 build: seed 77, a player and an AI rival with a relation, 2100 ticks.</summary>
    private static readonly string Version6SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v6.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion6SaveWithDiplomacy_AndNoWeather()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(6, root["SaveVersion"]!.GetValue<int>());
        Assert.NotEmpty(world["Diplomacy"]!["Relations"]!.AsArray());
        Assert.False(world.ContainsKey("Weather"));
        Assert.False(world["Regions"]!["Cells"]![0]!.AsObject().ContainsKey("SoilMoisture"));
    }

    [Fact]
    public void LoadVersion6_StartsUnderClearSkies_WithEachRegionsClimateInItsSoil()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version6SavePath);

        Assert.Empty(world.Weather.Cells);
        Assert.Equal(1, world.Weather.NextCellId);
        Assert.Equal(WeatherSpell.Normal, world.Weather.Spell);
        Assert.Null(world.Weather.SpellEndsTick);
        Assert.Equal(0f, world.Weather.WindDirection);
        Assert.All(world.Regions.Cells, region => Assert.Equal(region.Moisture, region.SoilMoisture));
    }

    [Fact]
    public void LoadVersion6_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version7Expectations.Apply(expected);
        Version8Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version6SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion6_KeepsPlayingTheSameWay_WeatherIncluded()
    {
        var content = TestContent.Shipped;
        var engine = new SimulationEngine(content.SettlementRules, content.DivineRules, content.Creatures, content.GodAi, content.Miracles, content.Diplomacy, content.Weather, content.Disasters);
        var straight = WorldSaveSerializer.LoadFromFile(Version6SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version6SavePath);

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        Assert.NotEmpty(straight.Weather.Cells);
        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_WithABrokenWeatherCellOrSoil_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version6SavePath);
        WeatherWorlds.AddCell(world, new GroundPosition(100f, 100f), 200f, humidity: 0.8f, pressure: 0.3f).Humidity = 2f;
        world.Regions.Cells[3].SoilMoisture = -0.5f;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
        Assert.Contains("Weather cell 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Region 3 has soil moisture", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WithAWeatherMemberInAVersion6Save_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Weather"] = new JsonObject();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("must not contain 'Weather'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version6SavePath))!.AsObject();
}
