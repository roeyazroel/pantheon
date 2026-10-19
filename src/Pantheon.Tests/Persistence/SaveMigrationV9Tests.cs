using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Migration;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 9 build (format version 9) still load: nobody is on the road and every god welcomes migrants.</summary>
public sealed class SaveMigrationV9Tests
{
    /// <summary>A save written by the Milestone 9 build: seed 77, four gods, Seren and Kaio allied, 2400 ticks.</summary>
    private static readonly string Version9SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v9.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion9SaveWithFourGodsAndAnAlliance_AndNoMigration()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(9, root["SaveVersion"]!.GetValue<int>());
        Assert.Equal(4, world["Gods"]!.AsArray().Count);
        Assert.Contains(world["Diplomacy"]!["Stances"]!.AsArray(), s => s!["Status"]!.GetValue<string>() == "Alliance");
        Assert.False(world.ContainsKey("Migration"));
        Assert.False(world["Gods"]![0]!.AsObject().ContainsKey("RefugeePolicy"));
    }

    [Fact]
    public void LoadVersion9_NobodyIsOnTheRoad_AndEveryGodWelcomesMigrants()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version9SavePath);

        Assert.Empty(world.Migration.Groups);
        Assert.Equal(1, world.Migration.NextGroupId);
        Assert.All(world.Settlements, settlement => Assert.Equal(0f, settlement.Emigration));
        Assert.All(world.Gods, god => Assert.Equal(RefugeePolicy.Welcome, god.RefugeePolicy));
    }

    [Fact]
    public void LoadVersion9_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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
        Version31Expectations.Apply(expected);
        Version32Expectations.Apply(expected);
        Version33Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version9SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion9_KeepsPlayingTheSameWay_TheAiChoosingItsPolicies()
    {
        var engine = TestContent.Shipped.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version9SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version9SavePath);

        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);

        WorldAssert.Equal(straight, resumed);
        Assert.Contains(straight.Gods, god => god.RefugeePolicy == RefugeePolicy.DemandConversion);
    }

    [Fact]
    public void Load_WithABrokenGroupOrEmigration_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version9SavePath);
        world.Settlements[0].Emigration = -1f;
        world.Migration.Groups.Add(new MigrationGroup
        {
            Id = 1,
            OriginSettlementId = 99,
            DestinationSettlementId = world.Settlements[0].Id,
            Cause = MigrationCause.War,
            Population = 0,
            Food = 0f,
            Health = 1f,
            Position = world.Settlements[0].Position,
            Path = [],
            Beliefs = [],
            TurnedAwayFrom = [],
            DepartedTick = 0,
        });
        world.Migration.NextGroupId = 2;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("has emigration -1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Migration group 1 names a missing settlement", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Migration group 1 has an invalid size", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVersion9_WithAMigrationAlready_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Migration"] = new JsonObject();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Migration'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version9SavePath))!.AsObject();
}
