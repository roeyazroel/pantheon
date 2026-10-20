using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds;

/// <summary>What a model is asked for a world's gods, and how its answer is read (ADR-065).</summary>
public sealed class PantheonPromptTests
{
    private static readonly WorldSeed Seed = new(42);

    private static PantheonSetup Setup => TestContent.Shipped.Pantheon;

    private static BornPantheon Scribed => PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, new NewWorldOptions());

    /// <summary>A model's god for a slot, as JSON.</summary>
    internal static JsonObject God(int slot, string name, string creature = "Cinderhoof", string personality = "Warlord", int aggression = 3) => new()
    {
        ["slot"] = slot,
        ["name"] = name,
        ["epithet"] = "the Unbroken",
        ["personality"] = personality,
        ["creature_name"] = creature,
        ["history"] = $"{name} rose from the salt marsh with {creature} at its side.",
        ["voice"] = "Blunt and loud",
        ["desire"] = "glory in battle",
        ["dread"] = "a quiet death",
        ["temper"] = new JsonObject { ["aggression"] = aggression, ["cooperation"] = -3, ["treachery"] = 1, ["grudge"] = 0, ["protect"] = 0, ["convert"] = 0, ["intimidate"] = -1 },
    };

    /// <summary>A model's answer of these gods.</summary>
    internal static string Answer(params JsonObject[] gods) => new JsonObject { ["gods"] = new JsonArray(gods) }.ToJsonString();

    [Fact]
    public void TheSchema_AsksForEveryLineOfEveryGod_AndOnlyThem()
    {
        var schema = JsonNode.Parse(PantheonPrompt.Schema)!;
        var god = schema["properties"]!["gods"]!["items"]!;

        Assert.False(schema["additionalProperties"]!.GetValue<bool>());
        Assert.False(god["additionalProperties"]!.GetValue<bool>());
        Assert.Equal(["slot", "name", "epithet", "personality", "creature_name", "history", "voice", "desire", "dread", "temper"], god["required"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(Enum.GetNames<GodPersonality>().Where(n => n != "None"), god["properties"]!["personality"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void TheQuestion_NamesThePlayerAndEveryRivalSlotWithItsAnimal()
    {
        var question = PantheonPrompt.Question(Setup, "a test island", TestContent.Minds);
        var text = question.Turns.Single().Text;

        Assert.Contains(Setup.Gods[0].Name, text, StringComparison.Ordinal);
        for (var i = 1; i < Setup.Gods.Count; i++)
        {
            Assert.Contains($"Slot {i}: a rival god whose Creature is a {Setup.Gods[i].CreatureSpecies.ToLowerInvariant()}", text, StringComparison.Ordinal);
        }

        Assert.Equal(TestContent.Minds.BirthEffort, question.Effort);
        Assert.Equal(TestContent.Minds.BirthMaxTokens, question.MaxTokens);
        Assert.Equal(PantheonPrompt.Schema, question.Schema);
    }

    [Fact]
    public void AGoodAnswer_GivesTheModelsGods()
    {
        var born = PantheonPrompt.Read(Answer(God(1, "Morrakh"), God(2, "Sela", "Bramblewick", "Guardian"), God(3, "Quint", "Hob", "Trickster")), Scribed, "Claude Test");

        Assert.Equal("Claude Test", born.Author);
        Assert.Null(born.Problem);
        Assert.Equal([Setup.Gods[0].Name, "Morrakh", "Sela", "Quint"], born.Gods.Select(g => g.Name));
        Assert.Equal([GodPersonality.None, GodPersonality.Warlord, GodPersonality.Guardian, GodPersonality.Trickster], born.Gods.Select(g => g.Personality));
        var temper = born.Gods[1].Persona!.Temper;
        Assert.Equal(1f, temper.Aggression);
        Assert.Equal(-1f, temper.Cooperation);
        Assert.Equal(1f / 3f, temper.Treachery, 4);
        Assert.Equal("Claude Test", born.Gods[1].Persona!.Author);
    }

    [Fact]
    public void ATemperBeyondThree_IsKeptToOne()
    {
        var born = PantheonPrompt.Read(Answer(God(1, "Morrakh", aggression: 9), God(2, "Sela", "Bramblewick"), God(3, "Quint", "Hob")), Scribed, "Claude Test");

        Assert.Equal(1f, born.Gods[1].Persona!.Temper.Aggression);
    }

    [Fact]
    public void AGodWrittenBadly_IsTheScribes()
    {
        var tooLong = God(2, "Sela", "Bramblewick");
        tooLong["name"] = new string('a', 40);
        var noTemper = God(3, "Quint", "Hob");
        noTemper.Remove("temper");

        var born = PantheonPrompt.Read(Answer(God(1, "Morrakh"), tooLong, noTemper), Scribed, "Claude Test");

        Assert.Equal("Morrakh", born.Gods[1].Name);
        Assert.Equal(Scribed.Gods[2], born.Gods[2]);
        Assert.Equal(Scribed.Gods[3], born.Gods[3]);
        Assert.Equal("Claude Test", born.Author);
        Assert.Contains("2 of the 3", born.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void AGodNamedLikeThePlayerOrAnother_IsTheScribes()
    {
        var player = Setup.Gods[0].Name;

        var born = PantheonPrompt.Read(Answer(God(1, player), God(2, "Sela", "Bramblewick"), God(3, "sela", "Hob")), Scribed, "Claude Test");

        Assert.Equal(Scribed.Gods[1].Name, born.Gods[1].Name);
        Assert.Equal("Sela", born.Gods[2].Name);
        Assert.Equal(Scribed.Gods[3].Name, born.Gods[3].Name);
        Assert.Equal(born.Gods.Count, born.Gods.Select(g => g.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"gods\": 3}")]
    [InlineData("[]")]
    public void AnAnswerThatIsNotGods_LeavesThemAllToTheScribes(string answer)
    {
        var born = PantheonPrompt.Read(answer, Scribed, "Claude Test");

        Assert.Equal(Scribed.Gods, born.Gods);
        Assert.Equal(BornPantheon.Scribes, born.Author);
        Assert.NotNull(born.Problem);
    }

    [Fact]
    public void AnAnswerForTheWrongSlots_IsIgnored()
    {
        var born = PantheonPrompt.Read(Answer(God(0, "Morrakh"), God(9, "Sela", "Bramblewick")), Scribed, "Claude Test");

        Assert.Equal(Scribed.Gods, born.Gods);
    }

    /// <summary>The names in a list of born gods, for messages.</summary>
    internal static IEnumerable<string> Names(BornPantheon born) => born.Gods.Select(g => g.Name);
}
