using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Politics;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 4's build (format version 16) still load: no treaties, favours or council yet.</summary>
public sealed class SaveMigrationV16Tests
{
    /// <summary>A save written by part 4's build at commit cf3149f: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version16SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v16.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion16SaveWithCultures_AndNoTreaties()
    {
        var root = ReadFixture();
        var diplomacy = root["World"]!["Diplomacy"]!.AsObject();

        Assert.Equal(16, root["SaveVersion"]!.GetValue<int>());
        Assert.All(root["World"]!["Settlements"]!.AsArray(), s => Assert.True(s!.AsObject().ContainsKey("Culture")));
        Assert.False(diplomacy.ContainsKey("Treaties"));
        Assert.False(diplomacy.ContainsKey("Council"));
    }

    [Fact]
    public void LoadVersion16_HasNoTreatiesFavoursOrCouncil()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version16SavePath);
        var diplomacy = world.Diplomacy;

        Assert.Empty(diplomacy.Treaties);
        Assert.Equal(1, diplomacy.NextTreatyId);
        Assert.Empty(diplomacy.Favours);
        Assert.Empty(diplomacy.Obligations);
        Assert.Null(diplomacy.Council.Session);
        Assert.Equal(-1, diplomacy.Council.NextSessionTick);
    }

    [Fact]
    public void LoadVersion16_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version16SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion16_KeepsPlayingTheSameWay_AndItsCouncilMeets()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version16SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version16SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Diplomacy), JsonSerializer.Serialize(resumed.Diplomacy));
        Assert.True(straight.Diplomacy.Council.NextSessionTick > 4800);
    }

    [Fact]
    public void Load_WithBrokenPolitics_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version16SavePath);
        world.Diplomacy.Treaties.Add(new Treaty
        {
            Id = 5, Kind = TreatyKind.Tribute, FirstGodId = 1, SecondGodId = 1, TargetGodId = null, PayerGodId = null, Amount = 10f, Secret = false, SignedTick = 10, EndsTick = 5,
        });
        world.Diplomacy.Favours.Add(new Favour { CreditorGodId = 1, DebtorGodId = 99, Weight = 1f, SinceTick = 0, SettlementId = null });

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("Treaty 5 is repeated or not below the next id 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Treaty 5 has an unknown kind, a missing god or one god twice", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Treaty 5 has a payer or amount that does not fit its kind", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Treaty 5 ends before it was signed", exception.Message, StringComparison.Ordinal);
        Assert.Contains("A favour names a missing god", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version16SavePath))!.AsObject();
}
