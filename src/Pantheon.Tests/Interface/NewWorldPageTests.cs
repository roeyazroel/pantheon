using System;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Flow;
using Pantheon.Game.Launch;
using Pantheon.Game.Minds;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Tests.Minds;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>What the New World page shows and keeps (ADR-082): its choices and their memory, the launch options that make the same choices, and the words that tell a world's setup.</summary>
public sealed class NewWorldPageTests
{
    private static readonly WorldSeed Seed = new(31);

    [Fact]
    public void TheChoices_BecomeAWorldsOptions()
    {
        var choices = new NewWorldChoices
        {
            GodName = " Morwen ",
            CreatureName = "Pebble",
            Species = "Lion",
            Size = "large",
            Villages = VillageCount.Many,
            Seasons = false,
            Color = 3,
            Gods = 7,
            Temper = RivalTemper.Peaceful,
            Rivals = RivalStrength.Strong,
            GodsMet = true,
            Victory = "Conquest",
            HoldMinutes = 40,
            Calamities = CalamityLevel.Harsh,
        };

        var options = choices.Options(TestContent.Shipped);

        Assert.Equal("Lion", options.Species);
        Assert.Equal("Large", options.Size);
        Assert.Equal(VictoryText.Choices[2].Settings, options.Victory);
        Assert.False(options.Seasons);
        Assert.True(options.GodsMet);
        Assert.Equal("Morwen", options.GodName);
        Assert.Equal("Pebble", options.CreatureName);
        Assert.Equal(7, options.Gods);
        Assert.Equal(VillageCount.Many, options.Villages);
        Assert.Equal(3, options.Color);
        Assert.Equal((RivalTemper.Peaceful, RivalStrength.Strong, CalamityLevel.Harsh, 40), (options.Temper, options.Rivals, options.Calamities, options.HoldMinutes!.Value));
    }

    [Fact]
    public void ChoicesOutOfReach_FallBackToTheContentsOwn()
    {
        var options = new NewWorldChoices { GodName = "  ", Species = "Dragon", Size = "Gigantic", Gods = 30, Victory = "Glory", HoldMinutes = 13, Color = -2 }.Options(TestContent.Shipped);

        Assert.Equal(new NewWorldOptions { Gods = 4 }, options);
        Assert.Equal(new NewWorldOptions(), new NewWorldChoices().Options(TestContent.Shipped));
    }

    [Fact]
    public void TheProfile_KeepsTheChoices_AndForgetsWhatItCannotRead()
    {
        var choices = new NewWorldChoices { GodName = "Morwen", Size = "Huge", Gods = 5, Temper = RivalTemper.Warlike, Calamities = CalamityLevel.Gentle, Victory = "Sandbox" };

        Assert.Equal(choices, NewWorldChoices.FromJson(choices.ToJson()));
        Assert.DoesNotContain("VictoryIndex", choices.ToJson(), StringComparison.Ordinal);
        Assert.Equal(4, NewWorldChoices.FromJson(choices.ToJson()).VictoryIndex);
        Assert.Equal(new NewWorldChoices(), NewWorldChoices.FromJson(null));
        Assert.Equal(new NewWorldChoices(), NewWorldChoices.FromJson("{ not json"));
        Assert.Equal(new NewWorldChoices(), NewWorldChoices.FromJson("{\"Temper\": \"Furious\"}"));
        Assert.Equal(new NewWorldChoices(), NewWorldChoices.FromJson("{\"Temper\": 9}"));
        Assert.Equal("Morwen", NewWorldChoices.FromJson("{\"GodName\": \"Morwen\", \"Extra\": 1}").GodName);
    }

    [Fact]
    public void LaunchOptions_MakeTheSameChoices()
    {
        var options = LaunchOptions.Parse(["--god-name=Morwen", "--creature-name=Pebble", "--gods=6", "--villages=many", "--color=3", "--rival-temper=warlike", "--rival-strength=weak", "--calamities=harsh", "--hold=20", "--world-size=large"]);

        Assert.Empty(options.Warnings);
        var world = options.NewWorld(TestContent.Shipped);
        Assert.Equal("Large", world.Size);
        Assert.Equal(("Morwen", "Pebble", 6, VillageCount.Many, 2), (world.GodName, world.CreatureName, world.Gods!.Value, world.Villages, world.Color!.Value));
        Assert.Equal((RivalTemper.Warlike, RivalStrength.Weak, CalamityLevel.Harsh, 20), (world.Temper, world.Rivals, world.Calamities, world.HoldMinutes!.Value));
    }

    [Fact]
    public void LaunchOptions_WarnAboutBadChoices()
    {
        var options = LaunchOptions.Parse(["--god-name=", "--gods=1", "--gods=many", "--villages=heaps", "--villages=2", "--color=0", "--rival-temper=calm", "--rival-strength=1", "--calamities=apocalyptic", "--hold=0", $"--creature-name={new string('x', 30)}"]);

        Assert.Equal(11, options.Warnings.Count);
        Assert.Equal(new NewWorldOptions(), options.NewWorld(TestContent.Shipped));
    }

    [Fact]
    public void TheSetup_IsToldInTheBook_AndTheDebugPanel()
    {
        var world = WorldMaker.Generate(TestContent.Small, Seed, new NewWorldOptions { Gods = 3, Villages = VillageCount.Many, Calamities = CalamityLevel.Harsh, Rivals = RivalStrength.Strong, Temper = RivalTemper.Warlike, Color = 1 }).State;

        Assert.Equal("Made with 3 gods and many villages; harsh calamities; strong, warlike rivals; a victory is held for twenty minutes.", SetupText.Sentence(world, 1200f));
        Assert.Equal("Setup: 3 gods, many villages, calamities harsh, rivals strong and warlike, victory held 1200 s, colours 1:2 2:1", SetupText.DebugLine(world, 1200f));

        Assert.EndsWith("Gods: Ilyra (Human, None, Bramble, colour 2), Vael (Ai, Tyrant, Gristle, colour 1), Seren (Ai, Opportunist, Thistle, colour 3)", SetupText.RunLines(world, 1200f, id => TestContent.DivineView.ColorIndex(world, id)), StringComparison.Ordinal);

        var plain = WorldMaker.Generate(TestContent.Small, Seed, new NewWorldOptions(Victory: VictorySettings.Sandbox)).State;
        Assert.Equal("Made with 4 gods and its size's villages; normal calamities; even rivals; no victory.", SetupText.Sentence(plain, 600f));
    }

    [Fact]
    public void TheHold_IsToldInWords_InTheVictoryAndTheGuide()
    {
        Assert.Equal(["a minute", "ten minutes", "twenty minutes", "forty minutes", "an hour", "90 minutes", "90 seconds"], new[] { 60f, 600f, 1200f, 2400f, 3600f, 5400f, 90f }.Select(VictoryText.Span));
        Assert.Equal("your followers held the world's belief for forty minutes", VictoryText.Reason(VictoryKind.Ascendancy, "forty minutes"));

        var guide = new Guide();
        guide.Next();
        guide.Observe([new DomainChosen { Tick = 0, GodId = 1, Domain = GodDomain.Storm }], 1, 1, false);
        guide.Observe([new MiracleCast { Tick = 0, GodId = 1, MiracleId = "food", Target = default, Cost = 1f, AffectedSettlementIds = [], Deaths = 0 }], 1, 1, false);
        guide.Observe([new CreatureChanged { Tick = 0, CreatureId = 1, Change = CreatureChange.Rewarded }], 1, 1, false);
        Assert.Equal(GuideStep.Diplomacy, guide.Step);

        Assert.StartsWith("Five other gods want", guide.Text("Morwen", "Pebble", 5, "twenty minutes"), StringComparison.Ordinal);
        Assert.StartsWith("Another god wants", guide.Text("Morwen", "Pebble", 1, "twenty minutes"), StringComparison.Ordinal);
        guide.Observe([], 1, 1, diplomacyOpen: true);
        Assert.Contains("for twenty minutes to win", guide.Text("Morwen", "Pebble", 5, "twenty minutes"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGodColours_HaveANameEach()
    {
        var view = TestContent.DivineView;

        Assert.Equal(view.GodColors.Count, view.GodColorNames.Count);
        Assert.Equal("Gold", view.GodColorNames[0]);
        var json = TestContent.ReadText(DivineViewSettings.FileName);
        Assert.Throws<ContentFormatException>(() => DivineViewSettings.Parse(json.Replace("\"Ivory\"", "\"Gold\"", StringComparison.Ordinal)));
        Assert.Throws<ContentFormatException>(() => DivineViewSettings.Parse(json.Replace(", \"Ivory\"]", "]", StringComparison.Ordinal)));
    }

    [Fact]
    public void AModel_IsAskedForTheTempersPersonalities_AndAGodOfAnotherIsTheScribes()
    {
        var options = new NewWorldOptions { Temper = RivalTemper.Peaceful };
        var setup = WorldMaker.PantheonFor(TestContent.Shipped, options);
        var allowed = TestContent.Shipped.Sizes.PersonalitiesOf(RivalTemper.Peaceful);
        var scribed = PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, options);

        var text = PantheonPrompt.Question(setup, "a test island", TestContent.Minds, allowed).Turns.Single().Text;
        Assert.Contains("from these only: Guardian, NatureKeeper, Trickster, Isolationist", text, StringComparison.Ordinal);
        Assert.DoesNotContain("from these only", PantheonPrompt.Question(setup, "a test island", TestContent.Minds, TestContent.Shipped.Sizes.PersonalitiesOf(RivalTemper.Mixed)).Turns.Single().Text, StringComparison.Ordinal);

        var answer = PantheonPromptTests.Answer(PantheonPromptTests.God(1, "Morrakh", personality: "Warlord"), PantheonPromptTests.God(2, "Sela", "Bramblewick", "Guardian"));
        var born = PantheonPrompt.Read(answer, scribed, "Claude Test", allowed);
        Assert.Equal(scribed.Gods[1], born.Gods[1]);
        Assert.Equal("Sela", born.Gods[2].Name);
        Assert.All(born.Gods.Skip(1), g => Assert.Contains(g.Personality, allowed));
    }
}
