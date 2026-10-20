using System;
using System.Linq;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Minds;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds;

/// <summary>The scribes write a world's rival gods from its seed (ADR-065).</summary>
public sealed class PersonaScribeTests
{
    private static BornPantheon Write(ulong seed, NewWorldOptions? options = null) =>
        PantheonBirth.Write(TestContent.Shipped, TestContent.Minds, new WorldSeed(seed), options ?? new NewWorldOptions());

    [Fact]
    public void TheSameSeed_WritesTheSameGods()
    {
        Assert.Equal(Write(42).Gods, Write(42).Gods);
    }

    [Fact]
    public void AnotherSeed_WritesOtherGods()
    {
        var names = Enumerable.Range(1, 6).Select(seed => string.Join(",", Write((ulong)seed).Gods.Select(g => g.Name))).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ThePlayersGod_KeepsItsNameAndCreature_WithNoPersona()
    {
        var player = TestContent.Shipped.Pantheon.Gods[0];
        var born = Write(7).Gods[0];

        Assert.Equal(new BornGod(player.Name, player.CreatureName, GodPersonality.None, null), born);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(99UL)]
    [InlineData(123456UL)]
    public void EveryRival_HasANameOfItsOwnStartAPersonalityAndACreature(ulong seed)
    {
        var gods = Write(seed).Gods;
        var rivals = gods.Skip(1).ToList();

        Assert.Equal(gods.Count, gods.Select(g => g.Name[..3]).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(gods.Count, gods.Select(g => g.CreatureName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(rivals.Count, rivals.Select(g => g.Personality).Distinct().Count());
        Assert.All(rivals, g => Assert.NotEqual(GodPersonality.None, g.Personality));
    }

    [Fact]
    public void AHistory_NamesTheGod_WithEveryPlaceholderFilled()
    {
        var gods = Write(5).Gods;

        for (var i = 1; i < gods.Count; i++)
        {
            var persona = gods[i].Persona!;
            Assert.Contains(gods[i].Name, persona.History, StringComparison.Ordinal);
            Assert.DoesNotContain("{", persona.History, StringComparison.Ordinal);
            Assert.Equal(BornPantheon.Scribes, persona.Author);
            Assert.False(string.IsNullOrWhiteSpace(persona.Epithet));
        }
    }

    [Fact]
    public void ATemper_LeansALittle_AndOneWayMore()
    {
        foreach (var god in Write(11).Gods.Skip(1))
        {
            var temper = god.Persona!.Temper;
            var leanings = new[] { temper.Aggression, temper.Cooperation, temper.Treachery, temper.Grudge, temper.Protect, temper.Convert, temper.Intimidate };

            Assert.All(leanings, l => Assert.InRange(l, -1f, 1f));
            Assert.Single(leanings, l => MathF.Abs(l) >= 0.8f - 1e-4f);
        }
    }

    [Fact]
    public void ALargeWorld_GivesSevenRivalsSevenPersonalities()
    {
        var large = TestContent.Shipped.Sizes.Sizes.MaxBy(s => s.Gods)!;
        var gods = Write(3, new NewWorldOptions(Size: large.Name)).Gods;

        Assert.Equal(large.Gods, gods.Count);
        Assert.Equal(gods.Count - 1, gods.Skip(1).Select(g => g.Personality).Distinct().Count());
    }
}
