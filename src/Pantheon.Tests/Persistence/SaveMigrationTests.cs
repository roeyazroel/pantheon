using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by earlier builds are upgraded on load without losing anything.</summary>
public sealed class SaveMigrationTests
{
    /// <summary>A save written by the Milestone 1 build (format version 1): seed 77, 250 ticks played.</summary>
    private static readonly string Version1SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v1.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion1Save()
    {
        var root = ReadFixture();

        Assert.Equal(1, root["SaveVersion"]!.GetValue<int>());
        var settlement = root["World"]!["Settlements"]![0]!.AsObject();
        Assert.False(settlement.ContainsKey("Health"));
        Assert.False(settlement.ContainsKey("ConstructionProgress"));
    }

    [Fact]
    public void LoadVersion1_SettlementsAreHealthyWithNothingUnderConstruction()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version1SavePath);

        var settlement = Assert.Single(world.Settlements);
        Assert.Equal(1f, settlement.Health);
        Assert.Equal(0f, settlement.ConstructionProgress);
    }

    [Fact]
    public void LoadVersion1_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        foreach (var settlement in expected["World"]!["Settlements"]!.AsArray().Select(node => node!.AsObject()))
        {
            settlement["Health"] = 1f;
            settlement["ConstructionProgress"] = 0f;
        }

        Version3Expectations.Apply(expected);
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
        Version16Expectations.Apply(expected);
        Version17Expectations.Apply(expected);
        Version18Expectations.Apply(expected);
        Version19Expectations.Apply(expected);
        Version20Expectations.Apply(expected);
        Version21Expectations.Apply(expected);
        Version22Expectations.Apply(expected);
        Version23Expectations.Apply(expected);
        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version1SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion1_ResavesAsTheCurrentVersion()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version1SavePath);

        var json = WorldSaveSerializer.Serialize(world);

        Assert.Equal(WorldSaveSerializer.CurrentSaveVersion, JsonNode.Parse(json)!["SaveVersion"]!.GetValue<int>());
        WorldAssert.Equal(world, WorldSaveSerializer.Deserialize(json));
    }

    [Fact]
    public void LoadVersion1_KeepsSimulatingDeterministically()
    {
        var straight = WorldSaveSerializer.LoadFromFile(Version1SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version1SavePath);
        var engine = new SimulationEngine(TestContent.BusyRules);

        engine.Run(straight, 300);
        engine.Run(resumed, 150);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 150);

        Assert.Equal(550, straight.Tick);
        WorldAssert.Equal(straight, resumed);
    }

    [Theory]
    [InlineData("Health")]
    [InlineData("ConstructionProgress")]
    public void LoadVersion1_WithAMemberAddedInVersion2_Throws(string member)
    {
        var root = ReadFixture();
        root["World"]!["Settlements"]![0]![member] = 0.5f;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains(member, exception.Message);
    }

    [Fact]
    public void LoadVersion1_GetsTheSameChecksAsCurrentSaves()
    {
        var unknownMember = ReadFixture();
        unknownMember["World"]!["Mana"] = 5;
        var negativePopulation = ReadFixture();
        negativePopulation["World"]!["Settlements"]![0]!["Population"] = -1;

        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(unknownMember.ToJsonString()));
        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(negativePopulation.ToJsonString()));
        Assert.Contains("negative population", exception.Message);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version1SavePath))!.AsObject();
}
