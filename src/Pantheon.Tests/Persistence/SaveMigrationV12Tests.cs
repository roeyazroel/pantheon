using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Belief;
using Pantheon.Domain.HolySites;
using Pantheon.Game;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by the Milestone 14 build (format version 12) still load: gods without domains, a world without holy sites yet.</summary>
public sealed class SaveMigrationV12Tests
{
    /// <summary>A save written by the Milestone 14 build at commit ab9cab9: seed 77, the small world, 4800 ticks, ten moments of history.</summary>
    private static readonly string Version12SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v12.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion12SaveWithHistory_AndNoDomainsOrSites()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(12, root["SaveVersion"]!.GetValue<int>());
        Assert.Equal(10, world["History"]!.AsArray().Count);
        Assert.False(world.ContainsKey("HolySites"));
        Assert.All(world["Gods"]!.AsArray(), god => Assert.False(god!.AsObject().ContainsKey("Domains")));
        Assert.All(world["Settlements"]!.AsArray(), settlement => Assert.False(settlement!.AsObject().ContainsKey("OldFaith")));
    }

    [Fact]
    public void LoadVersion12_HasNoDomainsSchismsOrSanctuaries()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version12SavePath);

        Assert.All(world.Gods, god => Assert.Empty(god.Domains));
        Assert.All(world.Settlements, settlement => Assert.Null(settlement.OldFaith));
        Assert.All(world.Settlements, settlement => Assert.Null(settlement.SanctuaryGodId));
        Assert.Empty(world.HolySites);
    }

    [Fact]
    public void LoadVersion12_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version12SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion12_InASession_FindsItsHolySites_AsANewWorldWould()
    {
        var session = GameSession.Load(TestContent.Small, Version12SavePath);
        var fresh = GameSession.CreateNew(TestContent.Small, session.World.Seed);

        Assert.Equal(TestContent.Shipped.HolySites.Sites.Count, session.World.HolySites.Count);
        Assert.Equal(fresh.World.HolySites.Select(s => (s.Kind, s.RegionId, s.Name)), session.World.HolySites.Select(s => (s.Kind, s.RegionId, s.Name)));
        Assert.All(session.World.HolySites, site => Assert.Null(site.KeeperGodId));
    }

    [Fact]
    public void LoadVersion12_KeepsPlayingTheSameWay_AndTakesUpDomains()
    {
        var engine = TestContent.Small.CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version12SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version12SavePath);

        engine.Run(straight, 1800);
        engine.Run(resumed, 900);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 900);

        WorldAssert.Equal(straight, resumed);
        Assert.Contains(straight.Gods, god => god.Domains.Count > 0);
        Assert.Equal(JsonSerializer.Serialize(straight.Gods.Select(g => g.Domains)), JsonSerializer.Serialize(resumed.Gods.Select(g => g.Domains)));
    }

    [Fact]
    public void Load_WithBrokenSitesFaithsOrDomains_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version12SavePath);
        world.HolySites.Add(new HolySite { Id = 1, Kind = HolySiteKind.SacredPeak, Name = "The Peak", RegionId = 0, Position = default, KeeperGodId = 99, ClaimantGodId = null, ClaimTicks = 0 });
        world.Settlements[0].OldFaith = new OldFaith { GodId = 1, Share = 1.5f, Returned = 0f, Faded = 0f, SinceTick = 0 };
        world.Gods[0].Domains.AddRange([Domain.Gods.GodDomain.Storm, Domain.Gods.GodDomain.Storm]);

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("Holy site 1 names a missing region or god", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"Settlement {world.Settlements[0].Id} has an Old Faith", exception.Message, StringComparison.Ordinal);
        Assert.Contains("God 1 has no domain list, an unknown domain or a domain twice", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVersion12_WithDomainsAlready_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Gods"]![0]!["Domains"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Domains'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version12SavePath))!.AsObject();
}
