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
        Assert.Equal(new[] { Biome.Sea, Biome.Highland, Biome.Tundra, Biome.Desert, Biome.Swamp, Biome.Forest, Biome.Grassland }, content.WorldGen.Biomes.Select(rule => rule.Biome));
        Assert.Equal(new[] { Biome.Coast, Biome.RiverValley, Biome.Island, Biome.Volcanic }, content.WorldGen.Landforms!.Traits.Select(t => t.Biome));
        Assert.NotEmpty(content.WorldGen.StartingSettlement.Names);
        Assert.Equal(10, content.SettlementRules.StepIntervalTicks);
    }

    [Fact]
    public void ShippedDivineContent_Parses()
    {
        var content = TestContent.Shipped;

        Assert.Equal(10, content.DivineRules.StepIntervalTicks);
        Assert.Equal(["food", "heal", "bless_crops", "lightning", "mass_healing", "rain", "fire", "shield", "earthquake", "water", "anoint"], content.Miracles.Miracles.Where(m => !m.RequiresDomain && m.Scale != Pantheon.Domain.Miracles.MiracleScale.DivineAct).Select(m => m.Id));
        Assert.Equal(["hallow", "awaken", "still", "raise_island", "split_mountain", "turn_river"], content.Miracles.Miracles.Where(m => m.Scale == Pantheon.Domain.Miracles.MiracleScale.DivineAct).Select(m => m.Id));
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
    [InlineData("world_sizes.json", "\"Gods\": 8,", "\"Gods\": 9,", "needs between 2 and 8 gods")]
    [InlineData("world_sizes.json", "\"Name\": \"Large\"", "\"Name\": \"small\"", "its own name")]
    [InlineData("world_sizes.json", "\"Settlements\": 8,", "\"Settlements\": 9,", "must describe the world")]
    [InlineData("world_sizes.json", "\"Settlements\": 40,", "\"Settlements\": 400,", "Names must hold a name for every settlement")]
    [InlineData("world_sizes.json", "\"PantheonMaxGods\": 3", "\"PantheonMaxGods\": 7", "between 2 and its 6 gods win together")]
    [InlineData("weather.json", "\"SnowTemperature\": 0.35", "\"SnowTemperature\": 1.5", "Extremes.SnowTemperature")]
    [InlineData("weather.json", "\"SeasonSeconds\": 150.0", "\"SeasonSeconds\": 0.0", "Seasons.SeasonSeconds")]
    [InlineData("ecology.json", "\"ScarThreshold\": 0.5", "\"ScarThreshold\": 2.0", "ScarThreshold")]
    [InlineData("settlement_rules.json", "\"GatherShare\": 0.15", "\"GatherShare\": 1.5", "Resources.GatherShare")]
    [InlineData("settlement_rules.json", "\"Speed\": 1.6", "\"Speed\": 0.5", "Roads.Speed")]
    [InlineData("settlement_rules.json", "\"FleeDanger\": 0.25", "\"FleeDanger\": 1.25", "Villagers.FleeDanger")]
    [InlineData("settlement_rules.json", "\"Mara\", \"Tobin\"", "\" \", \"Tobin\"", "Villagers.Names must not be blank")]
    [InlineData("settlement_rules.json", "\"MaxLifeYears\": 86.0", "\"MaxLifeYears\": 40.0", "lives from MinLifeYears to MaxLifeYears")]
    [InlineData("worldgen.json", "\"PatchStrength\": 0.6", "\"PatchStrength\": 1.6", "Resources.PatchStrength")]
    [InlineData("society.json", "\"Wood\": 25.0, \"Stone\": 15.0", "\"Wood\": 25.0, \"Stone\": 15.0, \"Iron\": 3", "Iron")]
    [InlineData("disasters.json", "\"Transmission\": 0.05", "\"Transmission\": 1.5", "GreatEvents.Plague.Transmission")]
    [InlineData("disasters.json", "\"HouseFloodResistance\": 0.25", "\"HouseFloodResistance\": 1.25", "HouseFloodResistance")]
    [InlineData("disasters.json", "\"Weakness\": 1.0", "\"Weakness\": -1.0", "Structure.Weakness")]
    [InlineData("disasters.json", "\"FirstMaxSeconds\": 2100.0", "\"FirstMaxSeconds\": 600.0", "longest waits must be at least its shortest")]
    [InlineData("disasters.json", "{ \"Kind\": \"Eclipse\", \"Names\"", "{ \"Kind\": \"Plague\", \"Names\"", "GreatEvents.Names must list known kinds, each once")]
    [InlineData("worldgen.json", "\"CoastMetres\": 32.0", "\"CoastMetres\": -4.0", "Landforms areas, distances and heights")]
    [InlineData("worldgen.json", "\"VolcanoMinElevation\": 60.0", "\"VolcanoMinElevation\": 50.0", "the volcano in disasters.json needs 60")]
    [InlineData("worldgen.json", "{ \"Biome\": \"Island\", \"BaseFertility\": 0.7", "{ \"Biome\": \"Coast\", \"BaseFertility\": 0.7", "Landforms.Traits must list Coast, RiverValley, Island and Volcanic")]
    [InlineData("god_ai.json", "\"Boost\": 2.0", "\"Boost\": 0.5", "Planning.Boost")]
    [InlineData("god_ai.json", "\"Doubt\": 0.3", "\"Doubt\": 1.3", "Planning.Doubt")]
    [InlineData("diplomacy.json", "\"SharedAdherence\": 0.05", "\"SharedAdherence\": 0.0", "Acquaintance.SharedAdherence")]
    [InlineData("diplomacy.json", "\"NeighbourMetres\": 500.0", "\"NeighbourMetres\": -1.0", "Acquaintance.NeighbourMetres")]
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
    [InlineData("\"GuardFight\": 0.2", "\"GuardFight\": -0.2", "Temperament.GuardFight")]
    [InlineData("\"CouragePerDefeat\": 0.08", "\"CouragePerDefeat\": 1.5", "Temperament.CouragePerDefeat")]
    [InlineData("\"Courage\": 0.15", "\"Courage\": 1.5", "Stonehide.Courage")]
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
            .Replace("\"FieldSize\": 10.0", "\"FieldSize\": -1.0", StringComparison.Ordinal)
            .Replace("\"MaxPieces\": 14", "\"MaxPieces\": 1", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
        Assert.Contains(SettlementViewSettings.FileName, exception.Message);
        Assert.Contains("HousesPerField", exception.Message);
        Assert.Contains("FieldSize", exception.Message);
        Assert.Contains("Style: Threshold must lie in 0..1, MaxPieces be at least 2", exception.Message);
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
