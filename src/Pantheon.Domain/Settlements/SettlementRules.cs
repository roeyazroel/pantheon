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

    /// <summary>Share of the yield fully scorched farmland loses until it regrows (ADR-014).</summary>
    public required float ScorchYieldLoss { get; init; }

    /// <summary>Health lost in a step in which nobody has a roof; a partly homeless settlement loses that share of it.</summary>
    public required float HomelessHealthLossPerStep { get; init; }

    /// <summary>Share of <see cref="WorkPerHouse"/> rebuilding a ruined house takes.</summary>
    public required float RuinWorkShare { get; init; }

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

    /// <summary>Share of a harvest lost on flooded farmland at full flood (ADR-022).</summary>
    public required float FloodYieldLoss { get; init; }

    /// <summary>Water each person drinks per step (ADR-022).</summary>
    public required float WaterPerPersonPerStep { get; init; }

    /// <summary>Water a settlement's wells and cisterns hold per unit of housing.</summary>
    public required float WaterStoragePerHousing { get; init; }

    /// <summary>Water the people draw per step at full access, as a multiple of what they drink.</summary>
    public required float WaterGatherFactor { get; init; }

    /// <summary>Access to water a river or lake in the settlement's own region gives, times its <c>FreshWater</c>.</summary>
    public required float FreshWaterAccess { get; init; }

    /// <summary>Access a river or lake in a neighbouring region gives, times its <c>FreshWater</c>.</summary>
    public required float NeighbourWaterAccess { get; init; }

    /// <summary>Access to water deep wells give whatever the weather.</summary>
    public required float WellFloor { get; init; }

    /// <summary>Access shallow wells add on well-watered ground, falling with the soil's water.</summary>
    public required float WellAccess { get; init; }

    /// <summary>Health lost per step by the share of the people who go without water.</summary>
    public required float ThirstHealthLossPerStep { get; init; }

    /// <summary>What a settlement's technology and buildings do for its life (ADR-023); all zero for none.</summary>
    public required CivicEffects Civics { get; init; }
}

/// <summary>What a settlement's technology (per level) and buildings do for its harvests, health, houses, stores and defence (spec §21, §36, ADR-023).</summary>
public sealed record CivicEffects
{
    /// <summary>Extra share of the harvest per level of agriculture.</summary>
    public required float AgriculturePerLevel { get; init; }

    /// <summary>Health per step per level of medicine, closing that share of the gap to full health.</summary>
    public required float MedicinePerLevel { get; init; }

    /// <summary>Share of the deaths from ill health prevented per level of medicine.</summary>
    public required float MedicineDeathsPerLevel { get; init; }

    /// <summary>Share of a house's work saved per level of architecture.</summary>
    public required float ArchitecturePerLevel { get; init; }

    /// <summary>Factor on food storage with a granary.</summary>
    public required float GranaryStorage { get; init; }

    /// <summary>Factor on water storage with a cistern.</summary>
    public required float CisternStorage { get; init; }

    /// <summary>Health per step a hospital adds, closing that share of the gap to full health.</summary>
    public required float HospitalRecovery { get; init; }

    /// <summary>Share of the deaths from ill health a hospital prevents.</summary>
    public required float HospitalDeaths { get; init; }

    /// <summary>Share of the harm an attack does that walls turn away.</summary>
    public required float WallsDefence { get; init; }

    /// <summary>Share of the harm an attack does turned away per level of warfare.</summary>
    public required float WarfarePerLevel { get; init; }

    /// <summary>Extra share of the harvest under a Food priority (spec §29).</summary>
    public required float FoodPriorityYield { get; init; }

    /// <summary>Health per step under a Health priority, closing that share of the gap to full health.</summary>
    public required float HealthPriorityRecovery { get; init; }

    /// <summary>Extra share of births under a Growth priority.</summary>
    public required float GrowthPriorityBirths { get; init; }

    /// <summary>Factor on the share of the day spent worshipping under a Worship priority.</summary>
    public required float WorshipPriorityFactor { get; init; }

    /// <summary>Share of the farmland's missing soil water that irrigation supplies (ADR-029).</summary>
    public float IrrigationWater { get; init; }

    /// <summary>No effects at all: for rules built before technology and buildings.</summary>
    public static CivicEffects None { get; } = new()
    {
        AgriculturePerLevel = 0f, MedicinePerLevel = 0f, MedicineDeathsPerLevel = 0f, ArchitecturePerLevel = 0f, GranaryStorage = 1f,
        CisternStorage = 1f, HospitalRecovery = 0f, HospitalDeaths = 0f, WallsDefence = 0f, WarfarePerLevel = 0f,
        FoodPriorityYield = 0f, HealthPriorityRecovery = 0f, GrowthPriorityBirths = 0f, WorshipPriorityFactor = 1f,
    };
}
