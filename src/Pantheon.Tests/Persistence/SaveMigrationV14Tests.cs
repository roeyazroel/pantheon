using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Society;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 2's build (format version 14) still load: every settlement starts its society as a newly founded one.</summary>
public sealed class SaveMigrationV14Tests
{
    /// <summary>A save written by part 2's build at commit 772a3ac: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version14SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v14.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion14SaveWithRivers_AndNoSociety()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(14, root["SaveVersion"]!.GetValue<int>());
        Assert.NotEmpty(world["Water"]!["Rivers"]!.AsArray());
        Assert.All(world["Settlements"]!.AsArray(), settlement => Assert.False(settlement!.AsObject().ContainsKey("Society")));
    }

    [Fact]
    public void LoadVersion14_GivesEverySettlementAFoundedSociety()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version14SavePath);

        Assert.All(world.Settlements, settlement =>
        {
            var society = settlement.Society;
            Assert.Equal((1f, 0.6f, 0f, 0.1f, 0.7f), (society.Safety, society.Happiness, society.Wealth, society.Education, society.Stability));
            Assert.Empty(society.Buildings);
            Assert.Equal(SettlementPriority.None, society.Priority);
            Assert.All(Enum.GetValues<TechnologyField>(), field => Assert.Equal(0f, society.Technology.Get(field)));
        });
    }

    [Fact]
    public void LoadVersion14_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version14SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion14_KeepsPlayingTheSameWay_AndItsSocietiesLive()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version14SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version14SavePath);

        engine.Run(straight, 1800);
        engine.Run(resumed, 900);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 900);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Settlements.Select(s => s.Society)), JsonSerializer.Serialize(resumed.Settlements.Select(s => s.Society)));
        Assert.Contains(straight.Settlements, settlement => settlement.Society.Priority != SettlementPriority.None);
        Assert.Contains(straight.Settlements, settlement => settlement.Society.Education > 0.1f || settlement.Society.Wealth > 0f);
    }

    [Fact]
    public void Load_WithABrokenSociety_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version14SavePath);
        var society = world.Settlements[0].Society;
        society.Happiness = 1.5f;
        society.Technology.Science = -1f;
        society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = null });

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains($"Settlement {world.Settlements[0].Id} has a society value out of range", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Science technology -1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("a temple without a known god", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version14SavePath))!.AsObject();
}
