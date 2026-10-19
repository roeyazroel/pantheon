using System;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The shipped data files parse strictly and pass validation.</summary>
public sealed class ContentTests
{
    [Fact]
    public void ShippedContent_Parses()
    {
        var content = TestContent.Shipped;

        Assert.Equal(1024, content.WorldGen.HeightmapResolution);
        Assert.Equal(32, content.WorldGen.RegionsPerSide);
        Assert.True(content.WorldGen.Biomes[^1].IsCatchAll());
        Assert.Equal(new[] { Biome.Sea, Biome.Highland, Biome.Forest, Biome.Grassland }, content.WorldGen.Biomes.Select(rule => rule.Biome));
        Assert.NotEmpty(content.WorldGen.StartingSettlement.Names);
        Assert.Equal(10, content.SettlementRules.StepIntervalTicks);
    }

    [Fact]
    public void ShippedDivineContent_Parses()
    {
        var content = TestContent.Shipped;

        Assert.Equal(10, content.DivineRules.StepIntervalTicks);
        Assert.Equal(["food", "heal", "bless_crops", "lightning", "mass_healing", "rain", "fire", "shield", "earthquake", "water"], content.Miracles.Miracles.Where(m => !m.RequiresDomain).Select(m => m.Id));
        Assert.NotEmpty(content.Pantheon.Gods);
        Assert.Equal(Pantheon.Domain.Gods.GodController.Human, content.Pantheon.Gods[0].Controller);
        Assert.All(content.Miracles.Miracles, m => Assert.Equal(m.Id, TestContent.DivineView.EffectFor(m.Id).MiracleId));
        Assert.Equal(content.WorldGen.StartingSettlement.Names.Count, content.WorldGen.StartingSettlement.Names.Distinct().Count());
    }

    [Theory]
    [InlineData("divine_rules.json", "\"ConversionPerStep\": 0.05", "\"ConversionPerStep\": 1.5", "ConversionPerStep")]
    [InlineData("miracles.json", "\"InfluenceRequirement\": 0.1,", "\"InfluenceRequirement\": 4.0,", "InfluenceRequirement")]
    [InlineData("miracles.json", "\"Id\": \"heal\"", "\"Id\": \"food\"", "used more than once")]
    [InlineData("gods.json", "\"Name\": \"Ilyra\"", "\"Name\": \" \"", "blank")]
    [InlineData("worldgen.json", "\"Count\": 8", "\"Count\": 80", "Names must hold a name for every settlement")]
    public void DivineContent_WithOutOfRangeValue_FailsValidation(string file, string from, string to, string expected)
    {
        var text = TestContent.ReadText(file);
        Assert.Contains(from, text, StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == file ? text.Replace(from, to, StringComparison.Ordinal) : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));
        Assert.Contains(file, exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedCreatureRules_Parse_WithARuleForEveryAction()
    {
        var rules = TestContent.Shipped.Creatures;

        Assert.Equal("Mossback", rules.Species[0].Name);
        Assert.Equal(Enum.GetValues<Pantheon.Domain.Creatures.CreatureAction>().Length - 1, rules.Actions.Count);
        Assert.Contains(rules.StartingPreferences, p => p.Action == Pantheon.Domain.Creatures.CreatureAction.Feed && p.Weight == 0.4f);
    }

    [Theory]
    [InlineData("\"RewardLearningRate\": 0.25", "\"RewardLearningRate\": 1.25", "RewardLearningRate")]
    [InlineData("{ \"Action\": \"Rest\", \"Seconds\": 25.0", "{ \"Action\": \"Wander\", \"Seconds\": 25.0", "no rule for Rest")]
    [InlineData("\"LevelExperience\": [40.0, 100.0", "\"LevelExperience\": [100.0, 40.0", "must rise")]
    public void CreatureRules_WithBadValues_FailValidation(string from, string to, string expected)
    {
        var text = TestContent.ReadText(ContentJson.CreaturesFileName);
        Assert.Contains(from, text, StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == ContentJson.CreaturesFileName ? text.Replace(from, to, StringComparison.Ordinal) : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Miracles_WithUnknownEffect_Throw()
    {
        var json = TestContent.ReadText("miracles.json").Replace("\"Kind\": \"Smite\"", "\"Kind\": \"Meteor\"", StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => ContentJson.ParseMiracles(json));
    }

    [Fact]
    public void ShippedPalette_Parses()
    {
        var palette = TestContent.Palette;

        Assert.Equal(RgbColor.ParseHex(palette.Grassland.ToHex()), palette.Grassland);
        Assert.NotEqual(palette.Grassland, palette.Forest);
    }

    [Fact]
    public void Content_WithUnknownMember_Throws()
    {
        var json = TestContent.ReadText(ContentJson.SettlementRulesFileName).Replace("\"WorkforceShare\"", "\"WorkForceShare\"", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => ContentJson.ParseSettlementRules(json));
        Assert.Contains(ContentJson.SettlementRulesFileName, exception.Message);
    }

    [Fact]
    public void Content_WithOutOfRangeValue_FailsValidation()
    {
        var rules = TestContent.ReadText(ContentJson.SettlementRulesFileName).Replace("\"WorkforceShare\": 0.6", "\"WorkforceShare\": 2.5", StringComparison.Ordinal);

        var files = GameContentFiles.Read(TestContent.ReadText) with { SettlementRules = rules };

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));
        Assert.Contains("WorkforceShare", exception.Message);
    }

    [Fact]
    public void Content_WithInvalidColour_Throws()
    {
        var json = TestContent.ReadText(BiomePalette.FileName).Replace("\"#7ea44a\"", "\"green\"", StringComparison.Ordinal);

        Assert.Throws<ContentFormatException>(() => BiomePalette.Parse(json));
    }

    [Fact]
    public void Palette_WithOutOfRangeThreshold_FailsValidation()
    {
        var json = TestContent.ReadText(BiomePalette.FileName).Replace("\"ColorVariation\": 0.05", "\"ColorVariation\": 0.9", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => BiomePalette.Parse(json));
        Assert.Contains(BiomePalette.FileName, exception.Message);
        Assert.Contains("ColorVariation", exception.Message);
    }

    [Fact]
    public void ShippedSettlementView_Parses()
    {
        var layout = TestContent.SettlementView.Layout;

        Assert.Equal(3, layout.HousesPerField);
        Assert.NotEmpty(layout.CropColors);
        // Neighbouring slots of a sunflower spiral lie at least 1.67 spacings apart, and a square
        // field turned to any yaw reaches half its diagonal from its centre.
        Assert.True(layout.FieldSize * MathF.Sqrt(2f) <= 1.67f * layout.FieldSpacing, "Neighbouring fields must not overlap.");
    }

    [Fact]
    public void ShippedCitizens_Parse()
    {
        var citizens = TestContent.SettlementView.Citizens;

        Assert.Equal(300, citizens.MaxRepresentatives);
        Assert.True(citizens.RegionalTierExitDistance > citizens.RegionalTierEnterDistance, "The tier needs a band between its distances, or it flickers.");
        Assert.True(citizens.CatchUpSpeed > citizens.WalkSpeed, "Rerouted citizens must be able to catch up with their routine.");
        Assert.NotEqual(citizens.FarmerColor, citizens.BuilderColor);
        Assert.NotEqual(citizens.FarmerColor, citizens.VillagerColor);
        Assert.NotEqual(citizens.BuilderColor, citizens.VillagerColor);
    }

    [Fact]
    public void SettlementView_WithUnknownMember_Throws()
    {
        var json = TestContent.ReadText(SettlementViewSettings.FileName).Replace("\"FieldSize\"", "\"FieldSise\"", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
        Assert.Contains(SettlementViewSettings.FileName, exception.Message);
    }

    [Fact]
    public void SettlementView_WithOutOfRangeValues_ListsEveryProblem()
    {
        var json = TestContent.ReadText(SettlementViewSettings.FileName)
            .Replace("\"HousesPerField\": 3", "\"HousesPerField\": 0", StringComparison.Ordinal)
            .Replace("\"FieldSize\": 10.0", "\"FieldSize\": -1.0", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
        Assert.Contains(SettlementViewSettings.FileName, exception.Message);
        Assert.Contains("HousesPerField", exception.Message);
        Assert.Contains("FieldSize", exception.Message);
    }

    [Fact]
    public void SettlementView_WithOutOfRangeCitizenValues_ListsEveryProblemOfEveryPart()
    {
        var json = TestContent.ReadText(SettlementViewSettings.FileName)
            .Replace("\"FieldSize\": 10.0", "\"FieldSize\": -1.0", StringComparison.Ordinal)
            .Replace("\"RegionalTierExitDistance\": 550.0", "\"RegionalTierExitDistance\": 300.0", StringComparison.Ordinal)
            .Replace("\"CatchUpSpeed\": 4.0", "\"CatchUpSpeed\": 1.0", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
        Assert.Contains("Layout: ", exception.Message);
        Assert.Contains("FieldSize", exception.Message);
        Assert.Contains("Citizens: ", exception.Message);
        Assert.Contains("RegionalTierExitDistance", exception.Message);
        Assert.Contains("CatchUpSpeed", exception.Message);
    }
}
