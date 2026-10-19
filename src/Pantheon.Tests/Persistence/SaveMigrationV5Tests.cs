using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Diplomacy;
using Pantheon.Persistence;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 5 build (format version 5) still load, with gods that have no relations yet.</summary>
public sealed class SaveMigrationV5Tests
{
    /// <summary>A save written by the Milestone 5 build: seed 77, a player and an AI rival, 1500 ticks.</summary>
    private static readonly string Version5SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v5.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion5SaveWithARival()
    {
        var root = ReadFixture();

        Assert.Equal(5, root["SaveVersion"]!.GetValue<int>());
        Assert.Equal(2, root["World"]!["Gods"]!.AsArray().Count);
        Assert.False(root["World"]!.AsObject().ContainsKey("Diplomacy"));
    }

    [Fact]
    public void LoadVersion5_GodsStartWithNoRelations()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version5SavePath);

        Assert.Empty(world.Diplomacy.Relations);
        Assert.Empty(world.Diplomacy.Proposals);
        Assert.Equal(DiplomaticStatus.Neutral, DiplomacyModel.StatusOf(world, world.Gods[0].Id, world.Gods[1].Id));
    }

    [Fact]
    public void LoadVersion5_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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
        Version16Expectations.Apply(expected);
        Version17Expectations.Apply(expected);
        Version18Expectations.Apply(expected);
        Version19Expectations.Apply(expected);
        Version20Expectations.Apply(expected);
        Version21Expectations.Apply(expected);
        Version22Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version5SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion5_KeepsPlayingTheSameWay()
    {
        var content = TestContent.Shipped;
        var engine = content.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version5SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version5SavePath);

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_WithABrokenRelation_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version5SavePath);
        var relation = DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, world, world.Gods[0].Id, world.Gods[1].Id);
        relation.Trust = 2f;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
        Assert.Contains("outside 0..1", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version5SavePath))!.AsObject();
}
