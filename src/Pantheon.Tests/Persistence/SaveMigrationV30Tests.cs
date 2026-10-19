using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Villagers;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 21's build (format version 30) still load, no god following a villager.</summary>
public sealed class SaveMigrationV30Tests
{
    /// <summary>A save written by part 21's build at commit 4c00aa3: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version30SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v30.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion30Save_WithoutVillagers()
    {
        var root = ReadFixture();

        Assert.Equal(30, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!.AsObject().ContainsKey("Villagers"));
        Assert.False(root["World"]!.AsObject().ContainsKey("NextVillagerId"));
    }

    [Fact]
    public void LoadVersion30_NoGodFollowsAVillager()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version30SavePath);

        Assert.Empty(world.Villagers);
        Assert.Equal(1, world.NextVillagerId);
    }

    [Fact]
    public void LoadVersion30_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version31Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version30SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion30_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version30SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version30SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void Load_AVersion30SaveThatAlreadyHasVillagers_Throws()
    {
        var root = ReadFixture();
        root["World"]!["Villagers"] = new JsonArray();

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(root.ToJsonString()));

        Assert.Contains("must not contain 'Villagers'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveAndLoad_KeepsAFollowedVillagerLivingTheSameWay_AndRejectsTwoForOneGod()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var world = WorldSaveSerializer.LoadFromFile(Version30SavePath);
        var village = world.Settlements.First(s => s.Population > 0);
        var at = village.Position;
        Assert.Equal(VillagerRejection.None, new VillagerAuthority(TestContent.Shipped.SettlementRules.Villagers)
            .Follow(world, new FollowVillager(world.Gods[0].Id, village.Id, 3, VillagerWork.Farming, Near(at, 10f), Near(at, 30f), Near(at, 4f), Near(at, 10f)), []));
        engine.Run(world, 600);

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));
        engine.Run(world, 1200);
        engine.Run(loaded, 1200);

        WorldAssert.Equal(world, loaded);
        Assert.NotEmpty(loaded.Villagers.Single().Memories);
        var copy = world.Villagers[0];
        world.Villagers.Add(new VillagerState
        {
            Id = world.NextVillagerId++, Name = "Twin", FollowerGodId = copy.FollowerGodId, SettlementId = copy.SettlementId, Work = copy.Work,
            Home = copy.Home, WorkPlace = copy.WorkPlace, Shrine = copy.Shrine, Position = copy.Position, AgeYears = 20f, LifeYears = 60f, FollowedTick = world.Tick,
        });
        Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));
    }

    /// <summary>A point a little east of another.</summary>
    private static GroundPosition Near(GroundPosition point, float metres) => new(point.X + metres, point.Z);

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version30SavePath))!.AsObject();
}
