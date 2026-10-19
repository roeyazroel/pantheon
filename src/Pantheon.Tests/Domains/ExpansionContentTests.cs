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
    public void ShippedContent_OffersAllThirteenDomains_EachWithASignatureMiracle_AndFourHolySites()
    {
        var content = TestContent.Shipped;

        Assert.Equal(System.Enum.GetValues<GodDomain>().Length, content.Domains.Domains.Count);
        Assert.All(content.Domains.Domains, d => Assert.Single(content.Miracles.Miracles, m => m.RequiresDomain && m.Domain == d.Domain));
        Assert.Equal(4, content.HolySites.Sites.Count);
        Assert.Equal(["Mossback", "Stonehide", "Swiftclaw", "Lumen"], content.Creatures.Species.Select(s => s.Name));
        Assert.All(content.GodAi.Profiles!, p => Assert.Equal(3, p.Domains.Count));
        Assert.Equal(0.6f, content.DivineRules.Schism.Doubt);
    }

    [Fact]
    public void Content_ThatNeedsADomainNotOffered_IsRefused()
    {
        var domains = TestContent.ReadText(ContentJson.DomainsFileName);
        var start = domains.IndexOf(",\n    { \"Domain\": \"Ocean\"", StringComparison.Ordinal);
        var end = domains.IndexOf("\n  ],", start, StringComparison.Ordinal);
        Assert.True(start > 0 && end > start);
        var files = GameContentFiles.Read(name => name == ContentJson.DomainsFileName ? domains.Remove(start, end - start) : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));

        Assert.Contains("tidal_wave needs Ocean", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ContentJson.WaterFileName, "\"FloodLevel\": 1.0", "\"FloodLevel\": 2.0", "MaxFloodLevel")]
    [InlineData(ContentJson.WaterFileName, "\"SiltBlessing\": 0.4", "\"SiltBlessing\": 0.4, \"Dams\": 1", "Dams")]
    [InlineData(ContentJson.WorldGenFileName, "\"HeadwaterCatchment\": 700.0", "\"HeadwaterCatchment\": 70000.0", "HeadwaterCatchment")]
    public void BrokenWaterContent_IsRefused_WithTheReason(string file, string from, string to, string expected) =>
        BrokenExpansionContent_IsRefused_WithTheReason(file, from, to, expected);

    [Theory]
    [InlineData(ContentJson.DomainsFileName, "\"DomainCostFactor\": 0.8", "\"DomainCostFactor\": 1.5", "DomainCostFactor")]
    [InlineData(ContentJson.DomainsFileName, "{ \"Rank\": \"LesserGod\", \"Domains\": 2 }", "{ \"Rank\": \"LesserGod\", \"Domains\": 0 }", "Slots")]
    [InlineData(ContentJson.HolySitesFileName, "\"ClaimInfluence\": 0.35", "\"ClaimInfluence\": 1.35", "ClaimInfluence")]
    [InlineData(ContentJson.HolySitesFileName, "\"Kind\": \"HolySpring\"", "\"Kind\": \"SacredPeak\"", "listed twice")]
    [InlineData(ContentJson.DivineRulesFileName, "\"ReturnPerStep\": 0.02", "\"ReturnPerStep\": 2.0", "Schism.ReturnPerStep")]
    [InlineData(ContentJson.CreaturesFileName, "\"Toughness\": 1.5", "\"Toughness\": 0.0", "Stonehide.Toughness")]
    [InlineData(ContentJson.GodsFileName, "\"CreatureSpecies\": \"Lumen\"", "\"CreatureSpecies\": \"Griffin\"", "Griffin")]
    [InlineData(ContentJson.DomainsFileName, "{ \"Domain\": \"Ocean\",", "{ \"Domain\": \"Fire\",", "listed twice")]
    [InlineData(ContentJson.DomainsFileName, "\"OceanMaxWaterRegions\": 8", "\"OceanMaxWaterRegions\": -1", "OceanMaxWaterRegions")]
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
