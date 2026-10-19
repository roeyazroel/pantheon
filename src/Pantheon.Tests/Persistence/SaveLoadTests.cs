using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves are versioned, human-readable JSON that round-trips <see cref="WorldState"/> exactly.</summary>
public sealed class SaveLoadTests
{
    [Fact]
    public void RoundTrip_PreservesWorldState()
    {
        var world = CreatePlayedWorld();

        var json = WorldSaveSerializer.Serialize(world);
        var loaded = WorldSaveSerializer.Deserialize(json);

        WorldAssert.Equal(world, loaded);
        Assert.Equal(json, WorldSaveSerializer.Serialize(loaded));
    }

    [Fact]
    public void RoundTrip_ThroughFile_PreservesWorldState()
    {
        var world = CreatePlayedWorld();
        var directory = Path.Combine(Path.GetTempPath(), "pantheon-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "saves", "quicksave.json");

        try
        {
            WorldSaveSerializer.SaveToFile(world, path);
            WorldSaveSerializer.SaveToFile(world, path);
            var loaded = WorldSaveSerializer.LoadFromFile(path);

            WorldAssert.Equal(world, loaded);
            Assert.Equal(new[] { path }, Directory.GetFiles(Path.GetDirectoryName(path)!));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Save_ContainsCurrentSaveVersion()
    {
        var json = WorldSaveSerializer.Serialize(CreatePlayedWorld());

        using var document = JsonDocument.Parse(json);
        var first = document.RootElement.EnumerateObject().First();
        Assert.Equal("SaveVersion", first.Name);
        Assert.Equal(WorldSaveSerializer.CurrentSaveVersion, first.Value.GetInt32());
        Assert.Equal(31, WorldSaveSerializer.CurrentSaveVersion);
    }

    [Fact]
    public void Save_IsHumanReadableJson()
    {
        var world = CreatePlayedWorld();
        var json = WorldSaveSerializer.Serialize(world);
        var root = JsonNode.Parse(json)!;

        Assert.Contains("\n  \"World\": {", json);
        Assert.Equal(world.Seed.Value, root["World"]!["Seed"]!.GetValue<ulong>());
        Assert.Equal(world.Regions.Cells[0].Biome.ToString(), root["World"]!["Regions"]!["Cells"]![0]!["Biome"]!.GetValue<string>());
        Assert.Equal(world.Settlements[0].Name, root["World"]!["Settlements"]![0]!["Name"]!.GetValue<string>());
    }

    [Fact]
    public void Load_WithoutSaveVersion_Throws()
    {
        var root = SaveAsNode();
        root.Remove("SaveVersion");

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("SaveVersion", exception.Message);
    }

    [Fact]
    public void Load_WithFutureSaveVersion_Throws()
    {
        var root = SaveAsNode();
        root["SaveVersion"] = WorldSaveSerializer.CurrentSaveVersion + 1;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("newer", exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("\"1\"")]
    [InlineData("1.5")]
    public void Load_WithInvalidSaveVersion_Throws(string version)
    {
        var root = SaveAsNode();
        root["SaveVersion"] = JsonNode.Parse(version);

        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("{\"SaveVersion\": 1}")]
    [InlineData("{\"SaveVersion\": 2}")]
    [InlineData("{\"SaveVersion\": 1, \"World\": 5}")]
    [InlineData("{\"SaveVersion\": 1, \"World\": {\"Settlements\": [3]}}")]
    [InlineData("{\"SaveVersion\": 1, \"World\": {}, \"World\": {}}")]
    public void Load_MalformedSave_Throws(string json)
    {
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(json));
    }

    [Fact]
    public void Load_WithUnknownMember_Throws()
    {
        var root = SaveAsNode();
        root["World"]!["Mana"] = 5;

        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void Load_WithNullForNonNullableMember_Throws()
    {
        var root = SaveAsNode();
        root["World"]!["Settlements"]![0]!["Name"] = null;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("Name", exception.Message);
    }

    [Fact]
    public void Load_WithInconsistentRegions_Throws()
    {
        var root = SaveAsNode();
        root["World"]!["Regions"]!["Cells"]![3]!["Id"] = 99;
        root["World"]!["Settlements"]![0]!["RegionId"] = -1;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains("index 3", exception.Message);
        Assert.Contains("missing region", exception.Message);
    }

    [Theory]
    [InlineData("Health")]
    [InlineData("ConstructionProgress")]
    public void Load_CurrentSaveWithoutSettlementMember_Throws(string member)
    {
        var root = SaveAsNode();
        root["World"]!["Settlements"]![0]!.AsObject().Remove(member);

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains(member, exception.Message);
    }

    [Theory]
    [InlineData("Health", 1.5, "health")]
    [InlineData("Health", -0.1, "health")]
    [InlineData("ConstructionProgress", -1.0, "construction progress")]
    public void Load_WithOutOfRangeSettlementValue_Throws(string member, double value, string expectedMessage)
    {
        var root = SaveAsNode();
        root["World"]!["Settlements"]![0]![member] = value;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void Load_WithAllZeroRandomState_Throws()
    {
        var root = SaveAsNode();
        root["World"]!["Random"] = new JsonObject { ["S0"] = 0, ["S1"] = 0, ["S2"] = 0, ["S3"] = 0 };

        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
    }

    /// <summary>A small world that has been simulated, so tick, RNG and settlement values are non-trivial.</summary>
    private static WorldState CreatePlayedWorld()
    {
        var world = WorldGenerator.Generate(new WorldSeed(77), TestContent.SmallWorldGen).State;
        new SimulationEngine(TestContent.BusyRules).Run(world, 250);
        return world;
    }

    /// <summary>Serializes a played world into an editable JSON tree.</summary>
    private static JsonObject SaveAsNode() => JsonNode.Parse(WorldSaveSerializer.Serialize(CreatePlayedWorld()))!.AsObject();
}
