using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Culture;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Persistence;

/// <summary>Saves written by Milestone 15, part 3's build (format version 15) still load: no feelings, cultures or conduct yet.</summary>
public sealed class SaveMigrationV15Tests
{
    /// <summary>A save written by part 3's build at commit b1dda8c: seed 77, the small world with rivers, 4800 ticks.</summary>
    private static readonly string Version15SavePath = Path.Combine(AppContext.BaseDirectory, "Persistence", "Fixtures", "save-v15.json");

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Fact]
    public void Fixture_IsAVersion15SaveWithSocieties_AndNoCulture()
    {
        var root = ReadFixture();
        var world = root["World"]!.AsObject();

        Assert.Equal(15, root["SaveVersion"]!.GetValue<int>());
        Assert.Contains(world["Settlements"]!.AsArray(), s => s!["Society"]!["Priority"]!.GetValue<string>() != "None");
        Assert.All(world["Settlements"]!.AsArray(), s => Assert.False(s!.AsObject().ContainsKey("Culture")));
        Assert.All(world["Gods"]!.AsArray(), g => Assert.False(g!.AsObject().ContainsKey("Conduct")));
    }

    [Fact]
    public void LoadVersion15_HasNoFeelingsCultureOrConduct()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version15SavePath);

        Assert.All(world.Settlements, s => Assert.Equal(0f, s.Culture.Secularism));
        Assert.All(world.Settlements.SelectMany(s => s.Beliefs), b => Assert.Equal((0f, 0f), (b.Dependency, b.Resentment)));
        Assert.All(world.Gods, g => Assert.Empty(g.Traits));
        Assert.All(world.Gods, g => Assert.Equal(0f, g.Conduct.Help));
    }

    [Fact]
    public void LoadVersion15_KeepsEveryOtherValue()
    {
        var expected = ReadFixture();
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
        Version34Expectations.Apply(expected);

        var resaved = JsonNode.Parse(WorldSaveSerializer.Serialize(WorldSaveSerializer.LoadFromFile(Version15SavePath)));

        Assert.True(JsonNode.DeepEquals(expected, resaved), resaved!.ToJsonString(Indented));
    }

    [Fact]
    public void LoadVersion15_KeepsPlayingTheSameWay_AndItsCulturesGrow()
    {
        var engine = (TestContent.Small with { WorldGen = TestContent.RiverWorldGen }).CreateEngine();
        var straight = WorldSaveSerializer.LoadFromFile(Version15SavePath);
        var resumed = WorldSaveSerializer.LoadFromFile(Version15SavePath);

        engine.Run(straight, 3000);
        engine.Run(resumed, 1500);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 1500);

        WorldAssert.Equal(straight, resumed);
        Assert.Equal(JsonSerializer.Serialize(straight.Settlements.Select(s => s.Culture)), JsonSerializer.Serialize(resumed.Settlements.Select(s => s.Culture)));
        Assert.Equal(JsonSerializer.Serialize(straight.Gods.Select(g => g.Conduct)), JsonSerializer.Serialize(resumed.Gods.Select(g => g.Conduct)));
        Assert.Contains(straight.Settlements, s => Enum.GetValues<Doctrine>().Any(d => s.Culture.Get(d) > 0.01f));
        Assert.Contains(straight.Gods, g => g.Conduct.Help + g.Conduct.Gifts + g.Conduct.Harm > 0f);
    }

    [Fact]
    public void Load_WithABrokenCultureOrConduct_Throws()
    {
        var world = WorldSaveSerializer.LoadFromFile(Version15SavePath);
        world.Settlements[0].Culture.Charity = 2f;
        world.Gods[0].Conduct.Harm = -1f;
        world.Gods[0].Traits.AddRange([GodTrait.Cruel, GodTrait.Cruel]);

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains($"Settlement {world.Settlements[0].Id} holds Charity at 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("God 1 has a conduct weight that is negative", exception.Message, StringComparison.Ordinal);
        Assert.Contains("God 1 has an unknown trait or a trait twice", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>The fixture as an editable JSON tree.</summary>
    private static JsonObject ReadFixture() => JsonNode.Parse(File.ReadAllText(Version15SavePath))!.AsObject();
}
