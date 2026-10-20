using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Persistence;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds;

/// <summary>A world's gods are born of a model or the scribes, and the world is made from them (ADR-065).</summary>
public sealed class PantheonBirthTests
{
    private static readonly WorldSeed Seed = new(42);

    private static Task<BornPantheon> Ask(FakeLanguageModel model, CancellationToken cancel = default) =>
        PantheonBirth.Ask(model, TestContent.Shipped, TestContent.Minds, Seed, new NewWorldOptions(), cancel);

    [Fact]
    public async Task AModelThatAnswers_WritesTheGods()
    {
        var model = new FakeLanguageModel(_ => ModelAnswer.Of(PantheonPromptTests.Answer(PantheonPromptTests.God(1, "Morrakh"), PantheonPromptTests.God(2, "Sela", "Bramblewick"), PantheonPromptTests.God(3, "Quint", "Hob"))));

        var born = await Ask(model);

        Assert.Equal(["Morrakh", "Sela", "Quint"], born.Gods.Skip(1).Select(g => g.Name));
        Assert.Equal("Fake Model", born.Author);
        var question = Assert.Single(model.Asked);
        Assert.Contains($"of {TestContent.Shipped.WorldGen.StartingSettlement.Count} villages and 4 gods", question.Turns[0].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ModelOutcome.Refused)]
    [InlineData(ModelOutcome.Failed)]
    [InlineData(ModelOutcome.CutShort)]
    public async Task AModelThatDoesNotAnswer_LeavesTheGodsToTheScribes(ModelOutcome outcome)
    {
        var born = await Ask(new FakeLanguageModel(_ => ModelAnswer.Without(outcome, "it would not")));

        Assert.Equal(PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, new NewWorldOptions()).Gods, born.Gods);
        Assert.Equal(BornPantheon.Scribes, born.Author);
        Assert.Contains("Fake Model wrote no gods (it would not)", born.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACancelledQuestion_LeavesTheGodsToTheScribes_WithoutAProblem()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        var born = await Ask(new FakeLanguageModel(_ => ModelAnswer.Of("{}")), cancel.Token);

        Assert.Equal(BornPantheon.Scribes, born.Author);
        Assert.Null(born.Problem);
    }

    [Fact]
    public void AWorldMadeFromBornGods_HasTheirNamesCreaturesPersonalitiesAndPersonas()
    {
        var options = new NewWorldOptions();
        var born = PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, options);

        var world = WorldMaker.Generate(TestContent.Small, Seed, options with { Born = born }).State;

        for (var i = 0; i < born.Gods.Count; i++)
        {
            var god = world.Gods[i];
            Assert.Equal(born.Gods[i].Name, god.Name);
            Assert.Equal(born.Gods[i].Persona, god.Persona);
            Assert.Equal(i == 0 ? GodPersonality.None : born.Gods[i].Personality, god.Personality);
            if (world.Creatures.Find(c => c.GodId == god.Id) is { } creature)
            {
                Assert.Equal(born.Gods[i].CreatureName, creature.Name);
            }
        }

        Assert.Contains(world.Creatures, c => c.GodId != world.Gods[0].Id);
    }

    [Fact]
    public void AWorldMadeWithoutBornGods_HasTheContentsGods()
    {
        var world = WorldMaker.Generate(TestContent.Small, Seed, new NewWorldOptions()).State;

        Assert.Equal(TestContent.Shipped.Pantheon.Gods.Select(g => g.Name), world.Gods.Select(g => g.Name));
        Assert.All(world.Gods, g => Assert.Null(g.Persona));
    }

    [Fact]
    public void BornGodsForAnotherSize_AreIgnored()
    {
        var large = TestContent.Shipped.Sizes.Sizes.MaxBy(s => s.Gods)!;
        var born = PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, new NewWorldOptions(Size: large.Name));

        var world = WorldMaker.Generate(TestContent.Small, Seed, new NewWorldOptions(Born: born)).State;

        Assert.Equal(TestContent.Shipped.Pantheon.Gods.Select(g => g.Name), world.Gods.Select(g => g.Name));
    }

    [Fact]
    public void BornGods_KeepTheAnimalThePlayerChose()
    {
        var options = new NewWorldOptions(Species: "Lion");
        var born = PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, options);

        var world = WorldMaker.Generate(TestContent.Small, Seed, options with { Born = born }).State;

        Assert.Equal("Lion", world.Creatures.Single(c => c.GodId == world.Gods[0].Id).Species);
        Assert.Equal(world.Creatures.Count, world.Creatures.Select(c => c.Species).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AWorldWithBornGods_SavesAndLoadsThemAndPlaysTheSame()
    {
        var options = new NewWorldOptions();
        var engine = TestContent.Small.CreateEngine();
        var straight = WorldMaker.Generate(TestContent.Small, Seed, options with { Born = PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, options) }).State;
        var resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(straight));

        Assert.Equal(straight.Gods.Select(g => g.Persona), resumed.Gods.Select(g => g.Persona));
        engine.Run(straight, 1200);
        engine.Run(resumed, 600);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 600);
        WorldAssert.Equal(straight, resumed);
    }

    [Fact]
    public void ASaveWithATemperOutOfRange_IsRefused()
    {
        var options = new NewWorldOptions();
        var world = WorldMaker.Generate(TestContent.Small, Seed, options with { Born = PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, Seed, options) }).State;
        world.Gods[1].Persona = world.Gods[1].Persona! with { Temper = new GodTemper { Aggression = 4f } };

        var exception = Assert.Throws<SaveFormatException>(() => WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(world)));

        Assert.Contains("temper leans outside", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATemper_LeansTheGodsProfile()
    {
        var engine = TestContent.Small.CreateEngine();
        var world = WorldMaker.Generate(TestContent.Small, Seed, new NewWorldOptions()).State;
        var god = world.Gods[1];
        var plain = engine.Gods!.Ai.ProfileOf(god)!;

        god.Persona = new GodPersona { Epithet = "the Test", History = "A test.", Voice = "plain", Desire = "tests", Dread = "failures", Temper = new GodTemper { Aggression = -1f, Grudge = 1f }, Author = "a test" };
        var leaning = engine.Gods!.Ai.ProfileOf(god)!;

        Assert.Equal(plain.Aggression - TestContent.Shipped.GodAi.TemperReach, leaning.Aggression, 4);
        Assert.Equal(Math.Min(1f, plain.Grudge + TestContent.Shipped.GodAi.TemperReach), leaning.Grudge, 4);
        Assert.Equal(plain.Convert, leaning.Convert);
    }

    [Fact]
    public void AnEvenTemper_LeavesTheProfileAsItIs()
    {
        var profile = TestContent.Shipped.GodAi.Profiles.First();

        Assert.Same(profile, GodTempers.Apply(profile, null, 0.15f));
        Assert.Same(profile, GodTempers.Apply(profile, GodTemper.Even, 0.15f));
        Assert.Same(profile, GodTempers.Apply(profile, new GodTemper { Aggression = 1f }, 0f));
    }

    [Fact]
    public void ALeaning_KeepsEveryWeightFromZeroToOne()
    {
        var profile = TestContent.Shipped.GodAi.Profiles.First() with { Aggression = 0.95f, Cooperation = 0.05f };

        var leaning = GodTempers.Apply(profile, new GodTemper { Aggression = 1f, Cooperation = -1f, Treachery = float.NaN }, 0.5f);

        Assert.Equal(1f, leaning.Aggression);
        Assert.Equal(0f, leaning.Cooperation);
        Assert.Equal(profile.Treachery, leaning.Treachery);
    }
}
