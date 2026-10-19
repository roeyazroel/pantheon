using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Gods;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 2 build (format version 2) still load, with a player god added.</summary>
public sealed class SaveMigrationV2Tests
{
    /// <summary>A save written by the Milestone 2 build (format version 2): seed 77, 1205 ticks played.</summary>
    private static readonly string Version2SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v2.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion2Save()
    {
        var root = ReadFixture();

        Assert.Equal(2, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!.AsObject().ContainsKey("Gods"));
        Assert.True(root["World"]!["Settlements"]![0]!.AsObject().ContainsKey("Health"));
        Assert.False(root["World"]!["Settlements"]![0]!.AsObject().ContainsKey("Beliefs"));
    }

    [Fact]
    public void LoadVersion2_AddsThePlayerGodFollowedInTheFirstSettlement()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version2SavePath);

        var god = Assert.Single(world.Gods);
        Assert.Equal(GodController.Human, god.Controller);
        Assert.Equal(100f, god.DivinePower);
        Assert.Equal(DivineRank.MinorSpirit, god.Rank);
        var belief = Assert.Single(world.Settlements[0].Beliefs);
        Assert.Equal(god.Id, belief.GodId);
        Assert.Equal(0.6f, belief.Adherence);
        Assert.All(world.Regions.Cells, cell => Assert.Equal(0f, cell.Blessing));
        Assert.Equal(1, world.Generation.StartingSettlement.Count);
    }

    [Fact]
    public void LoadVersion2_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version2SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion2_KeepsSimulatingDeterministicallyWithBelief()
    {
        var content = TestContent.Shipped;
        var engine = new SimulationEngine(content.SettlementRules, content.DivineRules, content.Creatures);
        var straight = WorldSaveSerializer.LoadFromFile(Version2SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version2SavePath);

        engine.Run(straight, 400);
        engine.Run(resumed, 200);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 200);

        WorldAssert.Equal(straight, resumed);
        Assert.True(straight.Gods[0].DivinePower > 100f);
    }

    [Theory]
    [InlineData("Gods")]
    public void LoadVersion2_WithAMemberAddedInVersion3_Throws(string member)
    {
        var root = ReadFixture();
        root["World"]![member] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));
        Assert.Contains(member, exception.Message);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version2SavePath))!.AsObject();
}
