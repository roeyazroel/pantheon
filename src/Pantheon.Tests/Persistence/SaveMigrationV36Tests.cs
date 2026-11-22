using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 58's build (format version 36) still load, made with the choices a world had before the New World page offered more (ADR-082), and play on as before.</summary>
public sealed class SaveMigrationV36Tests
{
    /// <summary>A save written by part 58's build at commit cba5f0d: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version36SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v36.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion36Save_WithoutASetup()
    {
        var root = ReadFixture();

        Assert.Equal(36, root["SaveVersion"]!.GetValue<int>());
        Assert.False(root["World"]!.AsObject().ContainsKey("Setup"));
        Assert.False(root["World"]!["Victory"]!.AsObject().ContainsKey("HoldSeconds"));
        Assert.All(root["World"]!["Gods"]!.AsArray(), god => Assert.False(god!.AsObject().ContainsKey("Color")));
    }

    [Fact]
    public void LoadVersion36_TheWorldHasTheOldSetup_ItsRulesHold_AndItsIdsColours()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version36SavePath);

        Assert.Equal(WorldSetup.Default, world.Setup);
        Assert.Null(world.Victory.HoldSeconds);
        Assert.Equal(["Ilyra", "Vael", "Seren", "Kaio"], world.Gods.Select(g => g.Name));
        Assert.All(world.Gods, god => Assert.Null(god.Color));
    }

    [Fact]
    public void LoadVersion36_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
        Version37Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version36SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion36_KeepsPlayingTheSameWay()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version36SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version36SavePath);

        engine.Run(straight, 4000);
        engine.Run(resumed, 2000);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 2000);

        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void TheChoices_SurviveASaveAndLoad()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version36SavePath);
        world.Setup = new WorldSetup { Calamities = CalamityLevel.Harsh, Rivals = RivalStrength.Strong, Temper = RivalTemper.Warlike, Villages = VillageCount.Many };
        world.Victory = world.Victory with { HoldSeconds = 1200f };
        world.Gods[0].Color = 2;
        world.Gods[2].Color = 0;

        var loaded = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world));

        Assert.Equal(world.Setup, loaded.Setup);
        Assert.Equal(1200f, loaded.Victory.HoldSeconds);
        Assert.Equal([2, null, 0, null], loaded.Gods.Select(g => g.Color));
    }

    [Fact]
    public void ALoad_RefusesTwoGodsInOneColour_AndAHoldThatIsNoTime()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version36SavePath);
        world.Gods[0].Color = 1;
        world.Gods[1].Color = 1;
        Assert.Contains("colour", Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world))).Message, StringComparison.Ordinal);

        world.Gods[1].Color = null;
        world.Victory = world.Victory with { HoldSeconds = 0f };
        Assert.Contains("hold", Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world))).Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version36SavePath))!.AsObject();
}
