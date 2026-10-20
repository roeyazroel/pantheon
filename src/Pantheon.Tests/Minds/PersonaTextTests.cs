using System;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Game.Launch;
using Pantheon.Game.Minds;
using Pantheon.Game.Settings;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds;

/// <summary>A god's persona in words, the minds' content and settings, and their launch options (ADR-065).</summary>
public sealed class PersonaTextTests
{
    private static readonly Lazy<Domain.World.WorldState> World = new(() => Pantheon.Game.WorldMaker.Generate(TestContent.Small, new Domain.World.WorldSeed(5), new Pantheon.Game.NewWorldOptions()).State);

    /// <summary>One of the content's gods with a persona: the player's Ilyra, Vael the Tyrant or Kaio the Trickster.</summary>
    private static GodState God(GodPersona? persona, GodPersonality personality = GodPersonality.Tyrant)
    {
        var god = World.Value.Gods.First(g => g.Personality == personality);
        god.Persona = persona;
        return god;
    }

    private static GodPersona Persona(GodTemper temper) => new()
    {
        Epithet = "the Ash-Crowned",
        History = "Vael woke beneath the mountain.",
        Voice = "Cold and imperious",
        Desire = "every village on its knees.",
        Dread = "being laughed at",
        Temper = temper,
        Author = BornPantheon.Scribes,
    };

    [Fact]
    public void ATitle_IsTheEpithet_ElseThePersonality()
    {
        Assert.Equal("Vael the Ash-Crowned", PersonaText.Title(God(Persona(GodTemper.Even))));
        Assert.Equal("Vael the Tyrant", PersonaText.Title(God(null)));
        Assert.Equal("Ilyra", PersonaText.Title(God(null, GodPersonality.None)));
    }

    [Fact]
    public void ANature_TellsThePersonalityItsStrongestLeaningsVoiceDesireAndDread()
    {
        var nature = PersonaText.Nature(God(Persona(new GodTemper { Aggression = 0.8f, Grudge = -0.4f, Convert = 0.1f })));

        Assert.Equal("A Tyrant, quicker to war and quicker to forgive than most. Speaks cold and imperious. Wants every village on its knees; dreads being laughed at.", nature);
        Assert.StartsWith("A Trickster. ", PersonaText.Nature(God(Persona(GodTemper.Even), GodPersonality.Trickster)), StringComparison.Ordinal);
        Assert.Empty(PersonaText.Nature(God(null)));
    }

    [Fact]
    public void TheNumbers_ShowEveryLeaning()
    {
        Assert.Equal("aggression +0.8, grudge -0.4", PersonaText.Numbers(new GodTemper { Aggression = 0.8f, Grudge = -0.4f }));
        Assert.Equal("even", PersonaText.Numbers(GodTemper.Even));
    }

    [Fact]
    public void TheShippedMinds_OfferClaudeFirstAndLoreForEveryPersonality()
    {
        var minds = TestContent.Minds;

        Assert.Equal(GameSettings.DefaultClaudeModel, minds.ClaudeModels[0].Id);
        Assert.Equal(GameSettings.DefaultOpenAIModel, minds.OpenAIModels[0].Id);
        Assert.Equal(GameSettings.DefaultGeminiModel, minds.GeminiModels[0].Id);
        Assert.Equal(minds.GeminiModels[0], minds.GeminiModel("gemini-nonsense"));
        Assert.Equal("GPT-6 Astra", minds.OpenAIModel("gpt-6-astra").Name);
        Assert.Equal(GameSettings.DefaultLocalModel, minds.LocalModel);
        Assert.Equal(GameSettings.DefaultLocalUrl, minds.LocalUrl);
        Assert.Equal(minds.ClaudeModels[0], minds.ClaudeModel("no-such-model"));
        Assert.All(Enum.GetValues<GodPersonality>().Where(p => p != GodPersonality.None), p => Assert.Equal(p, minds.Lore.For(p).Personality));
    }

    [Theory]
    [InlineData("\"BirthMaxTokens\": 16000", "\"BirthMaxTokens\": 10")]
    [InlineData("\"LocalUrl\": \"http://localhost:11434\"", "\"LocalUrl\": \"ftp://nowhere\"")]
    [InlineData("\"Personality\": \"Warlord\"", "\"Personality\": \"Guardian\"")]
    [InlineData("\"BirthEffort\": \"Medium\"", "\"BirthEffort\": \"Frantic\"")]
    [InlineData("{ \"Id\": \"gpt-6-astra\"", "{ \"Id\": \"gpt-6.1-sol\"")]
    [InlineData("\"Id\": \"gemini-4-argon\", \"Name\": \"Gemini 4 Argon\"", "\"Id\": \"gemini-4-argon\", \"Name\": \"\"")]
    public void BrokenMinds_AreRefused(string shipped, string broken)
    {
        var text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestContent.DataDirectory, MindContent.FileName));
        Assert.Contains(shipped, text, StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => MindContent.Parse(text.Replace(shipped, broken, StringComparison.Ordinal)));
    }

    [Fact]
    public void TheMindsSettings_RoundTrip_AndComeBackIntoRange()
    {
        var chosen = new GameSettings { Minds = MindProvider.Local, ClaudeModel = "claude-haiku-5-5", OpenAIModel = "gpt-6-luna", GeminiModel = "gemini-4-argon", LocalUrl = "http://192.168.1.4:11434", LocalModel = "qwen3:8b" };

        Assert.Equal(chosen, GameSettings.Parse(chosen.Serialize()));
        Assert.Contains("\"Minds\": \"Local\"", chosen.Serialize(), StringComparison.Ordinal);
        var broken = GameSettings.Parse("{\"Minds\": \"Oracle\", \"LocalUrl\": \"not a url\", \"LocalModel\": \" \"}");
        Assert.Equal(new GameSettings(), broken);
        Assert.Equal(MindProvider.Claude, GameSettings.Parse("{\"minds\": \"claude\"}").Minds);
        Assert.Equal(MindProvider.Gemini, GameSettings.Parse("{\"Minds\": \"Gemini\"}").Minds);
        Assert.Equal(GameSettings.DefaultOpenAIModel, GameSettings.Parse("{\"OpenAIModel\": \" \"}").OpenAIModel);
    }

    [Fact]
    public void TheLaunchOptions_ChooseTheMindsAndOpenThePantheon()
    {
        Assert.Equal(MindProvider.OpenAI, LaunchOptions.Parse(["--minds=openai"]).Minds);
        Assert.Equal(MindProvider.Gemini, LaunchOptions.Parse(["--minds=gemini"]).Minds);
        var options = LaunchOptions.Parse(["--minds=claude", "--pantheon=on"]);
        var wrong = LaunchOptions.Parse(["--minds=oracle"]);

        Assert.Equal(MindProvider.Claude, options.Minds);
        Assert.True(options.PantheonPage);
        Assert.Empty(options.Warnings);
        Assert.Null(wrong.Minds);
        Assert.Contains(wrong.Warnings, w => w.Contains("'--minds' needs scribes, claude, openai, gemini or local", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLaunchOptions_SpeakWithAGod()
    {
        var options = LaunchOptions.Parse(["--talk=3", "--talk-say=  Let us be allies.  "]);
        var player = LaunchOptions.Parse(["--talk=1"]);

        Assert.Equal(3, options.Talk);
        Assert.Equal("Let us be allies.", options.TalkSay);
        Assert.Empty(options.Warnings);
        Assert.Null(player.Talk);
        Assert.Contains(player.Warnings, w => w.Contains("'--talk' needs a rival god's number", StringComparison.Ordinal));
    }
}
