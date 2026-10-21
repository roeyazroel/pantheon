using System;
using System.IO;
using System.Linq;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Angel;
using Pantheon.Persistence;
using Pantheon.Tests.Minds.Agents;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Angel;

/// <summary>The angel's look (ADR-074): read from what the world remembers of its god, never a score; and its content.</summary>
public sealed class AngelLookTests
{
    private static AngelContent Content => TestContent.Angel;

    private static GodState God(float help = 0f, float harm = 0f, float cruelty = 0f, params GodTrait[] traits)
    {
        var (session, _, me) = AgentWorlds.Setup();
        var god = session.World.Gods.Single(g => g.Id == me);
        (god.Conduct.Help, god.Conduct.Harm, god.Conduct.Cruelty) = (help, harm, cruelty);
        god.Traits.Clear();
        god.Traits.AddRange(traits);
        return god;
    }

    [Fact]
    public void AGodThatHasDoneNothing_HasAnUnformedAngel()
    {
        var look = AngelLooks.Of(God(), Content);

        Assert.Equal(AngelStage.Unformed, look.Stage);
        Assert.Equal(0f, look.Lean);
        Assert.Equal(0f, look.Formed);
        Assert.Equal(AngelTone.Silver, look.Tone);
        Assert.Equal(AngelMarks.None, look.Marks);
    }

    [Fact]
    public void Kindness_BrightensTheAngel_FromKindToRadiant_AsItGrows()
    {
        var little = AngelLooks.Of(God(help: 3f), Content);
        var much = AngelLooks.Of(God(help: 40f), Content);

        Assert.Equal(AngelStage.Kind, little.Stage);
        Assert.Equal(AngelStage.Radiant, much.Stage);
        Assert.True(much.Lean > little.Lean && much.Formed > little.Formed);
        Assert.Equal(AngelTone.Light, much.Tone);
    }

    [Fact]
    public void Harm_DarkensIt_AndCrueltyToItsOwnWeighsMore()
    {
        var harm = AngelLooks.Of(God(harm: 5f), Content);
        var cruelty = AngelLooks.Of(God(cruelty: 5f), Content);
        var fallen = AngelLooks.Of(God(cruelty: 40f), Content);

        Assert.Equal(AngelStage.Stern, harm.Stage);
        Assert.True(cruelty.Lean < harm.Lean);
        Assert.Equal(AngelStage.Fallen, fallen.Stage);
        Assert.Equal(AngelTone.Dark, fallen.Tone);
    }

    [Fact]
    public void MuchGoodAndMuchHarm_TearIt_ButLittleOfEitherDoesNot()
    {
        var torn = AngelLooks.Of(God(help: 20f, harm: 18f), Content);
        var slight = AngelLooks.Of(God(help: 1f, harm: 1f), Content);

        Assert.Equal(AngelStage.Torn, torn.Stage);
        Assert.Equal(AngelTone.Torn, torn.Tone);
        Assert.True(torn.Conflict >= Content.TornConflict);
        Assert.Equal(AngelStage.Unformed, slight.Stage);
    }

    [Fact]
    public void TheGodsTraits_LeaveTheirMarks()
    {
        var look = AngelLooks.Of(God(help: 1f, traits: [GodTrait.Vengeful, GodTrait.Deceptive, GodTrait.Cruel, GodTrait.Ambitious, GodTrait.Chaotic, GodTrait.Generous, GodTrait.Merciful, GodTrait.Honest, GodTrait.Compassionate]), Content);

        Assert.Equal(AngelMarks.Blade | AngelMarks.Veil | AngelMarks.Chains | AngelMarks.Crown | AngelMarks.Cracks | AngelMarks.Gold | AngelMarks.OpenHands | AngelMarks.ClearEyes, look.Marks);
    }

    [Theory]
    [InlineData(AngelStage.Radiant)]
    [InlineData(AngelStage.Kind)]
    [InlineData(AngelStage.Unformed)]
    [InlineData(AngelStage.Torn)]
    [InlineData(AngelStage.Stern)]
    [InlineData(AngelStage.Fallen)]
    public void AForcedLook_HoldsItsStage_AndItsMarks(AngelStage stage)
    {
        var look = AngelLooks.Forced(stage, AngelMarks.Blade);

        Assert.Equal(stage, look.Stage);
        Assert.Equal(AngelMarks.Blade, look.Marks);
        Assert.True(Math.Abs(look.Lean) <= 1f && look.Formed is >= 0f and <= 1f && look.Conflict is >= 0f and <= 1f);
    }

    [Fact]
    public void TheShippedAngel_HasEveryStage_AndLinesForEveryMomentInEveryVoice()
    {
        Assert.All(Enum.GetValues<AngelStage>(), stage => Assert.False(string.IsNullOrWhiteSpace(Content.Stage(stage).Voice)));
        Assert.All(Enum.GetValues<AngelMoment>(), moment =>
            Assert.All(Enum.GetValues<AngelTone>(), tone => Assert.NotEmpty(Content.LinesFor(moment, tone))));
    }

    [Fact]
    public void AnAngelsName_ComesFromTheSeed_TheSameEveryTime()
    {
        var name = Content.NameFor(new WorldSeed(77), 1);

        Assert.Equal(name, Content.NameFor(new WorldSeed(77), 1));
        Assert.Contains(name, Content.Names);
        Assert.True(Enumerable.Range(1, 40).Select(s => Content.NameFor(new WorldSeed((ulong)s), 1)).Distinct().Count() > 3);
    }

    [Theory]
    [InlineData("\"Softness\": 6.0", "\"Softness\": 0.0")]
    [InlineData("\"TornConflict\": 0.45", "\"TornConflict\": 2.0")]
    [InlineData("\"WordsSeconds\": [150.0, 70.0, 35.0]", "\"WordsSeconds\": [150.0, 70.0]")]
    [InlineData("\"Fallen\": {", "\"Fell\": {")]
    [InlineData("\"Rank\": {", "\"Promotion\": {")]
    [InlineData("\"Help\": 1.0", "\"Help\": 1.0, \"Mercy\": 1.0")]
    [InlineData("I am {name}, {god}, and I will keep watch", "I am {name}, {lord}, and I will keep watch")]
    public void ABrokenAngel_IsRefused(string shipped, string broken)
    {
        var text = File.ReadAllText(Path.Combine(TestContent.DataDirectory, AngelContent.FileName));
        Assert.Contains(shipped, text, StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => AngelContent.Parse(text.Replace(shipped, broken, StringComparison.Ordinal)));
    }
}
