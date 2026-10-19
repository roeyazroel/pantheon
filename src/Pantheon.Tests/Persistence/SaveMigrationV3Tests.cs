using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 3 build (format version 3) still load, with a Creature added for every god.</summary>
public sealed class SaveMigrationV3Tests
{
    /// <summary>A save written by the Milestone 3 build: seed 77, a Food miracle, then 900 ticks.</summary>
    private static readonly string Version3SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v3.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion3Save()
    {
        var root = ReadFixture();

        Assert.Equal(3, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!.AsObject().ContainsKey("Gods"));
        Assert.False(root["World"]!.AsObject().ContainsKey("Creatures"));
    }

    [Fact]
    public void LoadVersion3_GivesEachGodACreatureAtItsHome()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version3SavePath);

        var creature = Assert.Single(world.Creatures);
        Assert.Equal(world.Gods[0].Id, creature.GodId);
        Assert.Equal(creature.Id, world.Gods[0].CreatureId);
        Assert.Equal(world.Settlements[0].Id, creature.HomeSettlementId);
        Assert.Equal(1, creature.Level);
    }

    [Fact]
    public void LoadVersion3_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version4Expectations.Apply(expected);
        Version5Expectations.Apply(expected);
        Version6Expectations.Apply(expected);
        Version7Expectations.Apply(expected);
        Version8Expectations.Apply(expected);
        Version9Expectations.Apply(expected);
        Version10Expectations.Apply(expected);
        Version11Expectations.Apply(expected);
        Version12Expectations.Apply(expected);
        Version13Expectations.Apply(expected);
        Version14Expectations.Apply(expected);
        Version15Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version3SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion3_KeepsSimulatingDeterministicallyWithItsCreature()
    {
        var content = TestContent.Shipped;
        var engine = new SimulationEngine(content.SettlementRules, content.DivineRules, content.Creatures);
        var straight = WorldSaveSerializer.LoadFromFile(Version3SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version3SavePath);

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
        Assert.NotEmpty(straight.Creatures[0].Memories);
    }

    [Fact]
    public void LoadVersion3_WithCreatures_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Creatures"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("Creatures", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version3SavePath))!.AsObject();
}
