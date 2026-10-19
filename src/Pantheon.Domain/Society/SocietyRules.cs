using System.Collections.Generic;

namespace Pantheon.Domain.Society;

/// <summary>
/// Data-driven tuning of settlements' society (spec §29, §30, §36, ADR-023), loaded from
/// <c>game/Data/society.json</c>. Rates are per simulated second; values without units are 0..1.
/// </summary>
public sealed record SocietyRules
{
    /// <summary>Ticks between society steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Share of the gap to its target that safety, happiness and stability close per second.</summary>
    public required float DriftPerSecond { get; init; }

    /// <summary>How far war over the settlement lowers its safety, beside its danger.</summary>
    public required float WarUnsafety { get; init; }

    /// <summary>Safety walls or watchmen (a Defence priority) add to its target.</summary>
    public required float DefenceSafety { get; init; }

    /// <summary>How happiness is made: the weights of what makes people content and unhappy.</summary>
    public required HappinessWeights Happiness { get; init; }

    /// <summary>How stability is made.</summary>
    public required StabilityWeights Stability { get; init; }

    /// <summary>Wealth per food unit harvested beyond what is eaten, while the stores are at least <see cref="FullStores"/> full.</summary>
    public required float SurplusWealth { get; init; }

    /// <summary>Share of storage at which the stores count as full, so surplus becomes goods.</summary>
    public required float FullStores { get; init; }

    /// <summary>Wealth per second traded with each other settlement within <see cref="TradeRange"/> regions.</summary>
    public required float TradePerNeighbour { get; init; }

    /// <summary>How far, in regions, trade and the spread of knowledge reach.</summary>
    public required int TradeRange { get; init; }

    /// <summary>Share of its wealth a settlement uses up per second.</summary>
    public required float WealthUsePerSecond { get; init; }

    /// <summary>Education gained per second at full stability with <see cref="LearnedWealth"/> per person.</summary>
    public required float EducationPerSecond { get; init; }

    /// <summary>Wealth per person at which learning goes at its full rate.</summary>
    public required float LearnedWealth { get; init; }

    /// <summary>Share of education forgotten per second.</summary>
    public required float EducationLossPerSecond { get; init; }

    /// <summary>Levels per second a field gains at full education.</summary>
    public required float TechnologyPerSecond { get; init; }

    /// <summary>How much faster the fields a settlement's priority and buildings favour grow.</summary>
    public required float FocusFactor { get; init; }

    /// <summary>Share of the gap to the most advanced neighbour within trade range closed per second in each field.</summary>
    public required float DiffusionPerSecond { get; init; }

    /// <summary>Highest level of any field: the world stays one of fantasy (§36).</summary>
    public required float MaxTechnology { get; init; }

    /// <summary>Work per second each person of the workforce puts into the building under way.</summary>
    public required float ProjectWorkPerWorker { get; init; }

    /// <summary>Least share of the people that a god must hold to set a settlement's priority.</summary>
    public required float PriorityShare { get; init; }

    /// <summary>Seconds before a settlement's priority may be changed again.</summary>
    public required float PriorityCooldownSeconds { get; init; }

    /// <summary>The buildings, what each costs and which priority raises it.</summary>
    public required IReadOnlyList<BuildingDefinition> Buildings { get; init; }

    /// <summary>How settlements found new villages under an Expansion priority.</summary>
    public required ExpansionRules Expansion { get; init; }
}

/// <summary>The weights of a settlement's happiness: its target is the weighted sum of what is good less what is bad.</summary>
public sealed record HappinessWeights
{
    /// <summary>Weight of food security (stores against need).</summary>
    public required float Food { get; init; }

    /// <summary>Weight of health.</summary>
    public required float Health { get; init; }

    /// <summary>Weight of water (wells not running dry).</summary>
    public required float Water { get; init; }

    /// <summary>Weight of safety.</summary>
    public required float Safety { get; init; }

    /// <summary>Weight of the love the people feel for their gods.</summary>
    public required float Love { get; init; }

    /// <summary>How much fear of their gods takes away.</summary>
    public required float Fear { get; init; }

    /// <summary>How much being without a roof takes away.</summary>
    public required float Homeless { get; init; }
}

/// <summary>The weights of a settlement's stability.</summary>
public sealed record StabilityWeights
{
    /// <summary>Weight of happiness.</summary>
    public required float Happiness { get; init; }

    /// <summary>Weight of unity of faith: how much of the settlement follows its largest religion.</summary>
    public required float Unity { get; init; }

    /// <summary>Weight of peace: no war over the settlement.</summary>
    public required float Peace { get; init; }

    /// <summary>How much an Old Faith's share takes away (spec §34).</summary>
    public required float Schism { get; init; }
}

/// <summary>A building: what it takes and which priority raises it.</summary>
public sealed record BuildingDefinition
{
    /// <summary>The building.</summary>
    public required BuildingKind Kind { get; init; }

    /// <summary>The priority under which it is built.</summary>
    public required SettlementPriority Priority { get; init; }

    /// <summary>Work it takes.</summary>
    public required float Work { get; init; }

    /// <summary>Wealth it takes, paid when the work begins.</summary>
    public required float Wealth { get; init; }

    /// <summary>The field of technology it favours.</summary>
    public required TechnologyField Field { get; init; }

    /// <summary>Wood it takes from the stores when it is begun (ADR-033).</summary>
    public float Wood { get; init; }

    /// <summary>Stone it takes from the stores when it is begun (ADR-033).</summary>
    public float Stone { get; init; }
}

/// <summary>How settlements found new villages (spec §29's "expand territory").</summary>
public sealed record ExpansionRules
{
    /// <summary>Least population a settlement must have to send settlers.</summary>
    public required int MinPopulation { get; init; }

    /// <summary>People who leave to found the new village.</summary>
    public required int Settlers { get; init; }

    /// <summary>Food each settler takes along.</summary>
    public required float FoodPerSettler { get; init; }

    /// <summary>Farthest, in regions, the settlers go.</summary>
    public required int Range { get; init; }

    /// <summary>Least distance, in regions, from every other settlement.</summary>
    public required int Spacing { get; init; }

    /// <summary>Seconds between two foundings from one settlement.</summary>
    public required float CooldownSeconds { get; init; }
}
