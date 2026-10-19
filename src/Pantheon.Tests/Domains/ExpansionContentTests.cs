using System;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Persistence;
using Pantheon.Simulation.Creatures;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Domains;

/// <summary>Milestone 15's content (ADR-021): domains, holy sites, schisms and species, parsed strictly and checked across files; and the launch options that drive them.</summary>
public sealed class ExpansionContentTests
{
    [Fact]
    public void ShippedContent_OffersElevenDomains_EachWithASignatureMiracle_AndFourHolySites()
    {
        var content = TestContent.Shipped;

        Assert.Equal(11, content.Domains.Domains.Count);
        Assert.DoesNotContain(content.Domains.Domains, d => d.Domain is GodDomain.Ocean or GodDomain.Justice);
        Assert.All(content.Domains.Domains, d => Assert.Single(content.Miracles.Miracles, m => m.RequiresDomain && m.Domain == d.Domain));
        Assert.Equal(4, content.HolySites.Sites.Count);
        Assert.Equal(["Mossback", "Stonehide", "Swiftclaw", "Lumen"], content.Creatures.Species.Select(s => s.Name));
        Assert.All(content.GodAi.Profiles!, p => Assert.Equal(3, p.Domains.Count));
        Assert.Equal(0.6f, content.DivineRules.Schism.Doubt);
    }

    [Theory]
    [InlineData(ContentJson.DomainsFileName, "\"DomainCostFactor\": 0.8", "\"DomainCostFactor\": 1.5", "DomainCostFactor")]
    [InlineData(ContentJson.DomainsFileName, "{ \"Rank\": \"LesserGod\", \"Domains\": 2 }", "{ \"Rank\": \"LesserGod\", \"Domains\": 0 }", "Slots")]
    [InlineData(ContentJson.HolySitesFileName, "\"ClaimInfluence\": 0.35", "\"ClaimInfluence\": 1.35", "ClaimInfluence")]
    [InlineData(ContentJson.HolySitesFileName, "\"Kind\": \"HolySpring\"", "\"Kind\": \"SacredPeak\"", "listed twice")]
    [InlineData(ContentJson.DivineRulesFileName, "\"ReturnPerStep\": 0.02", "\"ReturnPerStep\": 2.0", "Schism.ReturnPerStep")]
    [InlineData(ContentJson.CreaturesFileName, "\"Toughness\": 1.5", "\"Toughness\": 0.0", "Stonehide.Toughness")]
    [InlineData(ContentJson.GodsFileName, "\"CreatureSpecies\": \"Lumen\"", "\"CreatureSpecies\": \"Griffin\"", "Griffin")]
    [InlineData(ContentJson.GodAiFileName, "\"Domains\": [\"War\", \"Fire\", \"Death\"]", "\"Domains\": [\"War\", \"Ocean\", \"Death\"]", "Ocean")]
    [InlineData(ContentJson.MiraclesFileName, "\"Domain\": \"Trickery\"", "\"Domain\": \"Justice\"", "sow_discord needs Justice")]
    public void BrokenExpansionContent_IsRefused_WithTheReason(string file, string from, string to, string expected)
    {
        var text = TestContent.ReadText(file);
        Assert.Contains(from, text, StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == file ? text.Replace(from, to, StringComparison.Ordinal) : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EachGodsCreature_IsOfItsSpecies_WithItsTemperament_AndThePlayerMayChooseAnother()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(42));
        var chosen = GameSession.CreateNew(TestContent.Small, new WorldSeed(42), species: "Swiftclaw");
        var rules = TestContent.Shipped.Creatures;

        Assert.All(session.World.Creatures, c => Assert.Equal(TestContent.Shipped.Pantheon.Gods[c.GodId - 1].CreatureSpecies, c.Species));
        Assert.Equal("Swiftclaw", chosen.LocalCreature!.Species);
        Assert.Equal(session.World.Creatures.Skip(1).Select(c => c.Species), chosen.World.Creatures.Skip(1).Select(c => c.Species));
        var plain = session.LocalCreature!;
        var swift = chosen.LocalCreature!;
        Assert.Equal(Math.Clamp(plain.Personality.Aggression + 0.15f, 0f, 1f), swift.Personality.Aggression, 4);
        Assert.Equal(plain.Personality.Laziness, swift.Personality.Laziness);
        Assert.Equal(CreatureModel.Speed(rules, plain) * 1.4f, CreatureModel.Speed(rules, swift), 3);
        Assert.Equal(CreatureModel.Strength(rules, plain) * 1.1f, CreatureModel.Strength(rules, swift), 3);
    }

    [Fact]
    public void ACreatureOfAnUnlistedSpecies_PlaysAsTheFirst()
    {
        var (_, creature) = CreatureWorlds.Create(300f);
        var rules = CreatureWorlds.Rules;
        var plain = CreatureModel.Speed(rules, creature);
        var stranger = new CreatureState
        {
            Id = creature.Id, GodId = creature.GodId, Name = creature.Name, Species = "Griffin", Position = creature.Position, Heading = 0f, Level = creature.Level,
            Experience = 0f, Health = 1f, Loyalty = 1f, Morality = 0f, Needs = creature.Needs, Personality = creature.Personality, Activity = creature.Activity,
            Preferences = [], Memories = [], HomeSettlementId = creature.HomeSettlementId, CombatSkill = 0f, Scars = 0, Relations = [],
        };

        Assert.Equal("Mossback", CreatureModel.SpeciesOf(rules, stranger).Name);
        Assert.Equal(plain, CreatureModel.Speed(rules, stranger));
    }

    [Fact]
    public void LaunchOptions_ReadTheSpeciesRankDomainsAndPanels()
    {
        var options = LaunchOptions.Parse(["--species=Lumen", "--rank=God", "--domains=Storm, war", "--domains-panel=on", "--settings-page=on", "--focus=site2"]);
        var broken = LaunchOptions.Parse(["--rank=7", "--domains=Storm,Sky", "--species="]);

        Assert.Equal("Lumen", options.Species);
        Assert.Equal(DivineRank.God, options.Rank);
        Assert.Equal([GodDomain.Storm, GodDomain.War], options.Domains);
        Assert.True(options.DomainsPanel);
        Assert.True(options.SettingsPage);
        Assert.Equal(2, options.FocusSite);
        Assert.Empty(options.Warnings);
        Assert.Equal(3, broken.Warnings.Count);
        Assert.Empty(broken.Domains);
    }
}
