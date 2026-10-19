using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;

namespace Pantheon.Tests.Support;

/// <summary>Pinned society rules for society tests (ADR-023).</summary>
internal static class SocietyWorlds
{
    /// <summary>The balance shipped with Milestone 15, part 3, so tests keep their meaning when <c>society.json</c> is retuned.</summary>
    public static SocietyRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        DriftPerSecond = 0.02f,
        WarUnsafety = 0.8f,
        DefenceSafety = 0.2f,
        Happiness = new HappinessWeights { Food = 0.25f, Health = 0.2f, Water = 0.1f, Safety = 0.2f, Love = 0.25f, Fear = 0.3f, Homeless = 0.4f },
        Stability = new StabilityWeights { Happiness = 0.5f, Unity = 0.3f, Peace = 0.2f, Schism = 0.6f },
        SurplusWealth = 0.3f,
        FullStores = 0.9f,
        TradePerNeighbour = 0.02f,
        TradeRange = 6,
        WealthUsePerSecond = 0.001f,
        EducationPerSecond = 0.0015f,
        LearnedWealth = 2f,
        EducationLossPerSecond = 0.0003f,
        TechnologyPerSecond = 0.0012f,
        FocusFactor = 2.5f,
        DiffusionPerSecond = 0.002f,
        MaxTechnology = 5f,
        ProjectWorkPerWorker = 0.05f,
        PriorityShare = 0.4f,
        PriorityCooldownSeconds = 60f,
        Buildings =
        [
            new BuildingDefinition { Kind = BuildingKind.Temple, Priority = SettlementPriority.Temple, Work = 300f, Wealth = 60f, Field = TechnologyField.Religion },
            new BuildingDefinition { Kind = BuildingKind.Granary, Priority = SettlementPriority.Food, Work = 200f, Wealth = 30f, Field = TechnologyField.Agriculture },
            new BuildingDefinition { Kind = BuildingKind.Hospital, Priority = SettlementPriority.Health, Work = 250f, Wealth = 40f, Field = TechnologyField.Medicine },
            new BuildingDefinition { Kind = BuildingKind.Walls, Priority = SettlementPriority.Defence, Work = 300f, Wealth = 50f, Field = TechnologyField.Warfare },
            new BuildingDefinition { Kind = BuildingKind.Levee, Priority = SettlementPriority.Preparation, Work = 250f, Wealth = 35f, Field = TechnologyField.Engineering },
            new BuildingDefinition { Kind = BuildingKind.Firebreak, Priority = SettlementPriority.Preparation, Work = 150f, Wealth = 20f, Field = TechnologyField.Engineering },
            new BuildingDefinition { Kind = BuildingKind.Shelter, Priority = SettlementPriority.Preparation, Work = 200f, Wealth = 30f, Field = TechnologyField.Architecture },
            new BuildingDefinition { Kind = BuildingKind.Cistern, Priority = SettlementPriority.Preparation, Work = 150f, Wealth = 25f, Field = TechnologyField.Engineering },
        ],
        Expansion = new ExpansionRules { MinPopulation = 100, Settlers = 20, FoodPerSettler = 3f, Range = 8, Spacing = 4, CooldownSeconds = 240f },
    };

    /// <summary>The pinned settlement rules with the shipped civic effects.</summary>
    public static SettlementRules Civic { get; } = SettlementWorlds.Rules with { Civics = TestContent.Shipped.SettlementRules.Civics };
}
