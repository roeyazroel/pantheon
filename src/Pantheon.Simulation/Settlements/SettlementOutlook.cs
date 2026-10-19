namespace Pantheon.Simulation.Settlements;

/// <summary>
/// The deterministic part of a settlement's next step, from <see cref="SettlementEconomy.Assess"/>.
/// <see cref="SettlementSystem"/> applies it and draws the births and deaths; readouts and the
/// representative citizens show it.
/// </summary>
public sealed record SettlementOutlook
{
    /// <summary>The land the settlement farms.</summary>
    public required FarmlandSurvey Land { get; init; }

    /// <summary>The split of the workforce between the fields and the building site.</summary>
    public required LabourAllocation Labour { get; init; }

    /// <summary>Food harvested in the step.</summary>
    public required float Production { get; init; }

    /// <summary>Food the population needs in the step.</summary>
    public required float Consumption { get; init; }

    /// <summary>Food storage capacity.</summary>
    public required float Storage { get; init; }

    /// <summary>Share of the population's need that is met (0..1); below 1 the settlement starves.</summary>
    public required float FedShare { get; init; }

    /// <summary>Stored food after the step.</summary>
    public required float FoodAfter { get; init; }

    /// <summary>How readily the people find water (0..1, ADR-022).</summary>
    public required float WaterAccess { get; init; }

    /// <summary>Water drawn from rivers, lakes and wells in the step.</summary>
    public required float WaterDrawn { get; init; }

    /// <summary>Water the people drink in the step.</summary>
    public required float WaterNeeded { get; init; }

    /// <summary>Water the wells and cisterns hold at most.</summary>
    public required float WaterStorage { get; init; }

    /// <summary>Share of the people's thirst that is quenched (0..1); below 1 they go thirsty.</summary>
    public required float WateredShare { get; init; }

    /// <summary>Water stored after the step.</summary>
    public required float WaterAfter { get; init; }

    /// <summary>Health after the step.</summary>
    public required float HealthAfter { get; init; }

    /// <summary>Whether a house is wanted: one is under way or the settlement is crowded, and housing is below the cap.</summary>
    public required bool ConstructionWanted { get; init; }

    /// <summary>Whether the stores hold enough food to spare builders from the fields.</summary>
    public required bool ConstructionFunded { get; init; }

    /// <summary>Why no births can happen, or <see cref="BirthBlocker.None"/>.</summary>
    public required BirthBlocker BirthBlocker { get; init; }

    /// <summary>Chance per person of a birth in the step, at the health after the step; it applies only without a blocker.</summary>
    public required double BirthChance { get; init; }

    /// <summary>Chance per person of a death in the step, at the health after the step.</summary>
    public required double DeathChance { get; init; }

    /// <summary>Mean number of births in the step, after the blocker and the free housing.</summary>
    public required double ExpectedBirths { get; init; }

    /// <summary>Mean number of deaths in the step.</summary>
    public required double ExpectedDeaths { get; init; }

    /// <summary>Food gained (positive) or lost (negative) by the harvest and meals of one step, before storage limits.</summary>
    public float NetFood => Production - Consumption;

    /// <summary>Whether the people go short of water in the step.</summary>
    public bool IsThirsty => WateredShare < 1f;

    /// <summary>Whether the population goes short of food in the step.</summary>
    public bool IsStarving => FedShare < 1f;

    /// <summary>Whether a house is wanted but waits for the stores to refill.</summary>
    public bool ConstructionWaitsForFood => ConstructionWanted && !ConstructionFunded;
}
