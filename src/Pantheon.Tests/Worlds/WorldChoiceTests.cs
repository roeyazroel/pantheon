using System;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>The New World page's choices (ADR-082): how many gods and villages, the player's names and colour, the rivals' temper and strength, and what a world keeps of them.</summary>
public sealed class WorldChoiceTests
{
    private static readonly WorldSeed Seed = new(4242);

    private static GameContent Content => TestContent.Small;

    [Fact]
    public void TheLimits_FollowTheSizeAndItsVillages()
    {
        var content = TestContent.Shipped;

        Assert.Equal([6, 8, 10], new[] { VillageCount.Few, VillageCount.Normal, VillageCount.Many }.Select(v => WorldChoices.VillagesAt(content, "Small", v)));
        Assert.Equal([60], new[] { WorldChoices.VillagesAt(content, "Vast", VillageCount.Many) });
        Assert.Equal([3, 4, 5], new[] { VillageCount.Few, VillageCount.Normal, VillageCount.Many }.Select(v => WorldChoices.MaxGods(content, "Small", v)));
        Assert.Equal(8, WorldChoices.MaxGods(content, "Large", VillageCount.Normal));
        Assert.Equal(4, WorldChoices.GodsFor(content, new NewWorldOptions()));
        Assert.Equal(6, WorldChoices.GodsFor(content, new NewWorldOptions(Size: "Large")));
        Assert.Equal(3, WorldChoices.GodsFor(content, new NewWorldOptions(Size: "Small") { Villages = VillageCount.Few }));
        Assert.Equal(2, WorldChoices.GodsFor(content, new NewWorldOptions(Size: "Huge") { Gods = 1 }));
        Assert.Equal(8, WorldChoices.GodsFor(content, new NewWorldOptions(Size: "Huge") { Gods = 12 }));
        Assert.Equal([1, 1, 2, 3, 4], new[] { 2, 3, 4, 6, 8 }.Select(g => WorldChoices.PantheonMaxGods(content.Sizes, g)));
        Assert.Equal([10, 20, 40], WorldChoices.HoldMinutes(content));
        Assert.Equal(10, WorldChoices.DefaultHoldMinutes(content));
    }

    [Fact]
    public void ANameTyped_IsTrimmed_AndRefusedWhenEmptyOrTooLong()
    {
        Assert.Equal("Morwen", WorldChoices.CleanName("  Morwen \n"));
        Assert.Null(WorldChoices.CleanName("   "));
        Assert.Null(WorldChoices.CleanName(null));
        Assert.Null(WorldChoices.CleanName(new string('a', WorldChoices.NameLength + 1)));
        Assert.Equal(new string('a', WorldChoices.NameLength), WorldChoices.CleanName(new string('a', WorldChoices.NameLength)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AWorld_HoldsTheGodsAsked_EachWithAHome_AName_AndAnAnimalOfItsOwn(int gods)
    {
        // Seed 1234's small test island has room for four villages, a home for every god.
        var world = WorldMaker.Generate(Content, new WorldSeed(1234), new NewWorldOptions { Gods = gods }).State;

        Assert.Equal(gods, world.Gods.Count);
        Assert.Equal(GodController.Human, world.Gods[0].Controller);
        Assert.Equal(gods, world.Gods.Select(g => g.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(gods, world.Creatures.Count);
        Assert.Equal(gods, world.Creatures.Select(c => c.Species).Distinct(StringComparer.Ordinal).Count());
        Assert.All(world.Gods, god => Assert.Contains(world.Settlements, s => s.Beliefs.Exists(b => b.GodId == god.Id)));
        Assert.Equal(WorldChoices.PantheonMaxGods(Content.Sizes, gods), world.Victory.PantheonMaxGods);
    }

    [Fact]
    public void ALargerPantheon_TakesTheExtraGods_InOrder()
    {
        var setup = WorldMaker.PantheonFor(TestContent.Shipped, new NewWorldOptions(Size: "Large") { Gods = 8 });

        Assert.Equal(["Ilyra", "Vael", "Seren", "Kaio", "Aurel", "Maren", "Thorn", "Ysolde"], setup.Gods.Select(g => g.Name));
        Assert.Same(TestContent.Shipped.Pantheon, WorldMaker.PantheonFor(TestContent.Shipped, new NewWorldOptions()));
    }

    [Fact]
    public void FewOrManyVillages_FoundFewerOrMore_WithNamesEnough()
    {
        var content = TestContent.Shipped;
        var few = WorldMaker.AtSize(content, "Small", VillageCount.Few).WorldGen.StartingSettlement;
        var many = WorldMaker.AtSize(content, "Vast", VillageCount.Many).WorldGen.StartingSettlement;

        Assert.Equal(6, few.Count);
        Assert.Same(content.WorldGen.StartingSettlement.Names, few.Names);
        Assert.Equal(60, many.Count);
        Assert.True(many.Names.Count >= 60);
        Assert.Equal(many.Names.Count, many.Names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(WorldMaker.AtSize(content, "Huge").WorldGen.StartingSettlement.Names, WorldMaker.AtSize(content, "Huge", VillageCount.Many).WorldGen.StartingSettlement.Names);

        // The shipped small island, which has room for them all.
        var sparse = WorldMaker.Generate(content, Seed, new NewWorldOptions { Villages = VillageCount.Few }).State;
        var crowded = WorldMaker.Generate(content, Seed, new NewWorldOptions { Villages = VillageCount.Many }).State;
        Assert.Equal(6, sparse.Settlements.Count);
        Assert.Equal(3, sparse.Gods.Count);
        Assert.Equal(10, crowded.Settlements.Count);
        Assert.Equal(VillageCount.Many, crowded.Setup.Villages);
    }

    [Fact]
    public void ThePlayersNames_AreGiven_AndARivalWithOneTakesThePlayersOld()
    {
        var named = WorldMaker.Generate(Content, Seed, new NewWorldOptions { GodName = "Morwen", CreatureName = "Pebble" }).State;
        Assert.Equal("Morwen", named.Gods[0].Name);
        Assert.Equal("Pebble", named.Creatures.Single(c => c.GodId == 1).Name);

        var taken = WorldMaker.PantheonFor(Content, new NewWorldOptions { GodName = "vael", CreatureName = "Gristle" });
        Assert.Equal(["vael", "Ilyra", "Seren", "Kaio"], taken.Gods.Select(g => g.Name));
        Assert.Equal(["Gristle", "Bramble", "Thistle", "Quill"], taken.Gods.Select(g => g.CreatureName));
        Assert.Equal("Ilyra", WorldMaker.PantheonFor(Content, new NewWorldOptions { GodName = "  " }).Gods[0].Name);
    }

    [Fact]
    public void TheScribes_NeverGiveARivalThePlayersName()
    {
        var options = new NewWorldOptions { GodName = "Morwen", CreatureName = "Pebble", Gods = 4 };
        var born = PantheonBirth.Write(Content, TestContent.Minds, Seed, options);
        var world = WorldMaker.Generate(Content, Seed, options with { Born = born }).State;

        Assert.Equal("Morwen", world.Gods[0].Name);
        Assert.DoesNotContain(world.Gods.Skip(1), g => string.Equals(g.Name, "Morwen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(world.Creatures.Where(c => c.GodId != 1), c => string.Equals(c.Name, "Pebble", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AColourChosen_IsThePlayers_AndTheGodThatWoreItTakesThePlayers()
    {
        var world = WorldMaker.Generate(Content, Seed, new NewWorldOptions { Color = 2 }).State;
        var view = TestContent.DivineView;

        Assert.Equal([2, null, 0, null], world.Gods.Select(g => g.Color));
        Assert.Equal(view.GodColors[2], view.GodColor(world, 1));
        Assert.Equal(view.GodColors[0], view.GodColor(world, 3));
        Assert.Equal(view.GodColors[1], view.GodColor(world, 2));
        Assert.All(WorldMaker.Generate(Content, Seed, new NewWorldOptions { Color = 0 }).State.Gods, g => Assert.Null(g.Color));
    }

    [Fact]
    public void TheRivalsStrength_ScalesTheirStartingPower_AndNeverThePlayers()
    {
        var start = Content.Pantheon.StartingDivinePower;
        var strengths = Content.GodAi.Strengths;

        var weak = WorldMaker.Generate(Content, Seed, new NewWorldOptions { Rivals = RivalStrength.Weak }).State;
        var strong = WorldMaker.Generate(Content, Seed, new NewWorldOptions { Rivals = RivalStrength.Strong }).State;

        Assert.Equal(start, weak.Gods[0].DivinePower);
        Assert.Equal(start, strong.Gods[0].DivinePower);
        Assert.All(weak.Gods.Skip(1), g => Assert.Equal(start * strengths.Weak.Power, g.DivinePower));
        Assert.All(strong.Gods.Skip(1), g => Assert.Equal(start * strengths.Strong.Power, g.DivinePower));
        Assert.Equal(RivalStrength.Strong, strong.Setup.Rivals);
    }

    [Theory]
    [InlineData(RivalTemper.Peaceful)]
    [InlineData(RivalTemper.Warlike)]
    public void ATemper_BearsRivalsOfItsPersonalitiesOnly(RivalTemper temper)
    {
        var allowed = Content.Sizes.PersonalitiesOf(temper);
        var options = new NewWorldOptions { Temper = temper };

        var plain = WorldMaker.Generate(Content, Seed, options).State;
        var born = WorldMaker.Generate(Content, Seed, options with { Born = PantheonBirth.Write(Content, TestContent.Minds, Seed, options) }).State;

        Assert.All(plain.Gods.Skip(1), g => Assert.Contains(g.Personality, allowed));
        Assert.All(born.Gods.Skip(1), g => Assert.Contains(g.Personality, allowed));
        Assert.Equal(temper, born.Setup.Temper);
    }

    [Fact]
    public void AMixedTemper_WritesTheGodsTheScribesAlwaysWrote()
    {
        var setup = WorldMaker.PantheonFor(Content, new NewWorldOptions());

        Assert.Equal(PersonaScribe.Write(TestContent.Minds.Lore, setup, Seed).Gods, PantheonBirth.Write(Content, TestContent.Minds, Seed, new NewWorldOptions()).Gods);
        Assert.Equal(Enum.GetValues<GodPersonality>().Length - 1, Content.Sizes.PersonalitiesOf(RivalTemper.Mixed).Count);
    }

    [Fact]
    public void TheWorld_KeepsItsChoices_AndTheHoldChosen()
    {
        var world = WorldMaker.Generate(Content, Seed, new NewWorldOptions { Calamities = CalamityLevel.Harsh, HoldMinutes = 20 }).State;
        var plain = WorldMaker.Generate(Content, Seed, new NewWorldOptions()).State;

        Assert.Equal(CalamityLevel.Harsh, world.Setup.Calamities);
        Assert.Equal(1200f, world.Victory.HoldSeconds);
        Assert.Equal(WorldSetup.Default, plain.Setup);
        Assert.Equal(Content.DivineRules.VictorySeconds, plain.Victory.HoldSeconds);
    }

    [Fact]
    public void TheSameSeedAndChoices_MakeTheSameWorld()
    {
        var options = new NewWorldOptions(Species: "Lion") { Gods = 3, Villages = VillageCount.Many, Color = 4, Temper = RivalTemper.Warlike, Rivals = RivalStrength.Weak, Calamities = CalamityLevel.Gentle, GodName = "Morwen" };

        WorldAssert.Equal(WorldMaker.Generate(Content, Seed, options).State, WorldMaker.Generate(Content, Seed, options).State);
    }
}
