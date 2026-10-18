namespace Pantheon.Domain.Settlements;

/// <summary>
/// Data-driven tuning of the statistical settlement model, loaded from
/// <c>game/Data/settlement_rules.json</c>. The model steps once every <see cref="StepIntervalTicks"/>
/// ticks, and every rate is per step (ADR-008).
/// </summary>
public sealed record SettlementRules
{
    /// <summary>Ticks between settlement steps; 10 ticks make one simulated second.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Share of the population that works (0..1); the rest are children, elders and carers.</summary>
    public required float WorkforceShare { get; init; }

    /// <summary>Radius in regions (Chebyshev distance) of the land a settlement farms around its own region.</summary>
    public required int FarmlandRadius { get; init; }

    /// <summary>Farmers a land region can usefully employ; past this, extra farmers add less and less food.</summary>
    public required float FarmersPerRegion { get; init; }

    /// <summary>Extra yield share of fully blessed farmland: production scales by <c>1 + bonus × mean blessing</c>.</summary>
    public required float BlessingYieldBonus { get; init; }

    /// <summary>Soil moisture (0..1] at and above which crops have all the water they need (ADR-013).</summary>
    public required float WellWateredSoil { get; init; }

    /// <summary>Share of the yield parched farmland still gives; it rises to 1 at <see cref="WellWateredSoil"/>.</summary>
    public required float DroughtYieldFloor { get; init; }

    /// <summary>Drought (0..1) from which a settlement counts as being in drought: alerts, the HUD and labels say so.</summary>
    public required float DroughtThreshold { get; init; }

    /// <summary>Share of the workers who worship instead of farming, in a settlement whose people all follow a god with full Faith.</summary>
    public required float WorshipWorkforceShare { get; init; }

    /// <summary>Food per farmer per step on land of fertility 1 while farmland is plentiful.</summary>
    public required float FoodPerFarmerPerStep { get; init; }

    /// <summary>Food eaten per person per step.</summary>
    public required float FoodPerPersonPerStep { get; init; }

    /// <summary>Food storage per unit of housing; a harvest beyond it spoils.</summary>
    public required float FoodStoragePerHousing { get; init; }

    /// <summary>Stored food per person required before births occur.</summary>
    public required float SurplusFoodPerPerson { get; init; }

    /// <summary>Health lost in a step in which nobody eats; a partial shortfall loses the unfed share of it.</summary>
    public required float StarvationHealthLossPerStep { get; init; }

    /// <summary>Health regained in a step in which everyone eats.</summary>
    public required float HealthRecoveryPerStep { get; init; }

    /// <summary>Chance per person per step of a birth at full health, while food is in surplus and housing is free.</summary>
    public required double BirthChancePerPersonPerStep { get; init; }

    /// <summary>Baseline chance per person per step of a death.</summary>
    public required double DeathChancePerPersonPerStep { get; init; }

    /// <summary>Extra chance per person per step of a death at zero health; it scales with (1 - health) squared.</summary>
    public required double IllHealthDeathChancePerPersonPerStep { get; init; }

    /// <summary>People one house shelters.</summary>
    public required int HousingPerHouse { get; init; }

    /// <summary>Population as a share of housing (0..1] at which the settlement starts building a house.</summary>
    public required float ConstructionStartCrowding { get; init; }

    /// <summary>Largest share of the workforce that leaves the fields to build (0..1).</summary>
    public required float MaxBuilderShare { get; init; }

    /// <summary>Most builders that can work on the house under construction.</summary>
    public required float BuildersPerSite { get; init; }

    /// <summary>Builder-steps of work one house needs.</summary>
    public required float WorkPerHouse { get; init; }

    /// <summary>Steps of the population's consumption the stores must hold before builders leave the fields.</summary>
    public required float ConstructionFoodReserveSteps { get; init; }

    /// <summary>Housing at which the settlement stops building.</summary>
    public required int MaxHousing { get; init; }
}
