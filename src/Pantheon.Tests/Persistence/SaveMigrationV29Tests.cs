using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Hand;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 18's build (format version 29) still load, whatever a hand holds carrying no stores.</summary>
public sealed class SaveMigrationV29Tests
{
    /// <summary>A save written by part 18's build at commit d228743: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version29SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v29.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion29Save_WithLandformsButNoOfferings()
    {
        var root = ReadFixture();

        Assert.Equal(29, root["SaveVersion"]!.GetValue<int>());
        Assert.True(root["World"]!["Generation"]!.AsObject().ContainsKey("Landforms"));
        Assert.Null(root["World"]!["Gods"]![0]!["Held"]);
    }

    [Fact]
    public void LoadVersion29_AHandHoldingARock_CarriesNoStores()
    {
        var root = WithRock(ReadFixture());

        var world = WorldSaveSerializer.Deserialize(root.ToJsonString());

        var held = world.Gods[0].Held!;
        Assert.Equal((HeldKind.Rock, 0f, 0f, 0f), (held.Kind, held.Food, held.Wood, held.Stone));
    }

    [Fact]
    public void LoadVersion29_KeepsEveryOtherValue()
    {
        var expected = WithRock(ReadFixture());
        var source = expected.ToJsonString();
        Version30Expectations.Apply(expected);
        Version31Expectations.Apply(expected);
        Version32Expectations.Apply(expected);
        Version33Expectations.Apply(expected);
        Version34Expectations.Apply(expected);
        Version35Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.Deserialize(source)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion29_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version29SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version29SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_AVersion29SaveWhoseHandAlreadyCarriesFood_Throws()
    {
        var root = WithRock(ReadFixture());
        root["World"]!["Gods"]![0]!["Held"]!["Food"] = 0;

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Food'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsAnOffering_AndRejectsStoresInARock()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version29SavePath);
        world.Gods[0].Held = new HeldObject { Kind = HeldKind.Offering, Amount = 1, SettlementId = world.Settlements[0].Id, Food = 40f, Wood = 8f, Stone = 3f, Tick = world.Tick };

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal((40f, 8f, 3f), (loaded.Gods[0].Held!.Food, loaded.Gods[0].Held!.Wood, loaded.Gods[0].Held!.Stone));
        world.Gods[0].Held = new HeldObject { Kind = HeldKind.Rock, Amount = 1, Food = 5f, Tick = world.Tick };
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>The fixture with its first god's hand holding a rock, as version 29 saved it.</summary>
    private static JsonObject WithRock(JsonObject root)
    {
        root["World"]!["Gods"]![0]!["Held"] = new JsonObject
        {
            ["Kind"] = "Rock",
            ["Amount"] = 1,
            ["SettlementId"] = null,
            ["FromDanger"] = false,
            ["Health"] = 1,
            ["Beliefs"] = new JsonArray(),
            ["Tick"] = 10,
        };
        return root;
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version29SavePath))!.AsObject();
}
