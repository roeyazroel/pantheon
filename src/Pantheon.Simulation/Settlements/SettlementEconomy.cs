using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Settlements;

/// <summary>
/// The rules of the statistical (Tier 1) settlement model as pure functions of the state and the
/// rules (ADR-008). <see cref="SettlementSystem"/> applies them once per step; readouts and the
/// representative citizens use <see cref="Assess"/> to show what the next step does.
/// </summary>
public static class SettlementEconomy
{
    /// <summary>Counts the farmable regions within <paramref name="radius"/> regions (Chebyshev distance) of a region.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="regionId">The settlement's region.</param>
    /// <param name="radius">Farmland radius in regions; 0 is the settlement's own region only.</param>
    /// <returns>The number of farmable regions and their mean fertility, blessing, soil moisture and scorch. The square is clipped at the grid edge.</returns>
    public static FarmlandSurvey SurveyFarmland(RegionGrid grid, int regionId, int radius)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        _ = grid.GetRegion(regionId);

        var (column, row) = grid.GetCoordinates(regionId);
        var count = 0;
        var fertility = 0f;
        var blessing = 0f;
        var soil = 0f;
        var scorch = 0f;
        var flood = 0f;
        for (var r = Math.Max(0, row - radius); r <= Math.Min(grid.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(grid.Columns - 1, column + radius); c++)
            {
                var region = grid.Cells[(r * grid.Columns) + c];
                if (IsFarmland(region))
                {
                    count++;
                    fertility += SimMath.Clamp01(region.Fertility);
                    blessing += SimMath.Clamp01(region.Blessing);
                    soil += SimMath.Clamp01(region.SoilMoisture);
                    scorch += SimMath.Clamp01(region.Scorch);
                    flood += SimMath.Clamp01(region.Flood);
                }
            }
        }

        return count > 0 ? new FarmlandSurvey(count, fertility / count, blessing / count, soil / count, scorch / count, flood / count) : new FarmlandSurvey(0, 0f);
    }

    /// <summary>Whether a region can be farmed: dry land with fertile soil.</summary>
    /// <param name="region">Region to check.</param>
    /// <returns><c>true</c> for farmable land.</returns>
    public static bool IsFarmland(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Biome != Biome.Sea && region.Fertility > 0f;
    }

    /// <summary>Food the whole population eats in one step.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="population">Number of inhabitants.</param>
    /// <returns>Food units.</returns>
    public static float ComputeConsumption(SettlementRules rules, int population)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Math.Max(0, population) * rules.FoodPerPersonPerStep;
    }

    /// <summary>Most food a settlement can store; a harvest beyond it spoils.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="housing">Housing capacity in people.</param>
    /// <returns>Storage capacity in food units.</returns>
    public static float ComputeStorage(SettlementRules rules, int housing)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Math.Max(0, housing) * rules.FoodStoragePerHousing;
    }

    /// <summary>Most food a settlement can store, a granary included (ADR-023).</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Storage capacity in food units.</returns>
    public static float StorageOf(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return ComputeStorage(rules, settlement.Housing) * CivicModel.StorageFactor(rules, settlement);
    }

    /// <summary>Most water a settlement's wells and cisterns hold, a cistern included (ADR-023).</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Water storage.</returns>
    public static float WaterStorageOf(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return ComputeWaterStorage(rules, settlement.Housing) * CivicModel.WaterStorageFactor(rules, settlement);
    }

    /// <summary>Number of houses that provide a housing capacity; a part-filled last house still counts.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="housing">Housing capacity in people.</param>
    /// <returns><c>ceil(housing / HousingPerHouse)</c>, at least 0.</returns>
    public static int ComputeHouseCount(SettlementRules rules, int housing)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return housing <= 0 ? 0 : ((housing - 1) / rules.HousingPerHouse) + 1;
    }

    /// <summary>Work the next house takes: less when it rebuilds a ruin (ADR-014).</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Builder-steps of work.</returns>
    public static float ComputeHouseWork(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var work = settlement.Ruins > 0 ? rules.WorkPerHouse * rules.RuinWorkShare : rules.WorkPerHouse;
        return work * CivicModel.HouseWorkFactor(rules, settlement);
    }

    /// <summary>Share of the people without a roof: those beyond the housing, after a disaster or an influx.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Homeless share (0..1).</returns>
    public static float ComputeHomelessShare(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.Population > 0 ? Math.Max(0f, settlement.Population - settlement.Housing) / settlement.Population : 0f;
    }

    /// <summary>Whether the settlement wants a house built: one is under way or it is crowded, and housing is below the cap.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <returns><c>true</c> when builders should work, food permitting.</returns>
    public static bool WantsConstruction(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        if (settlement.Housing >= rules.MaxHousing)
        {
            return false;
        }

        return settlement.ConstructionProgress > 0f ||
            settlement.Population >= rules.ConstructionStartCrowding * settlement.Housing;
    }

    /// <summary>Whether the stores hold enough food (the construction reserve) to take builders out of the fields.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <returns><c>true</c> when construction can go ahead.</returns>
    public static bool CanFundConstruction(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.Food >= rules.ConstructionFoodReserveSteps * ComputeConsumption(rules, settlement.Population);
    }

    /// <summary>
    /// How devoted the settlement is as a whole: the sum over the gods it knows of their followers'
    /// share times their Faith, from 0 (nobody believes) to 1 (everyone follows a god with full Faith).
    /// </summary>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Devotion (0..1).</returns>
    public static float ComputeDevotion(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var devotion = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            devotion += SimMath.Clamp01(belief.Adherence) * SimMath.Clamp01(belief.Faith);
        }

        return SimMath.Clamp01(devotion);
    }

    /// <summary>
    /// Splits the workforce for one step. Builders work only while a house is wanted and funded, and
    /// are capped by the builder share, the builders one site takes and the work left on the house.
    /// Worshippers are <c>WorshipWorkforceShare</c> of the workers, scaled by the settlement's devotion.
    /// Everyone else who works farms.
    /// </summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Workers, farmers, builders and worshippers.</returns>
    public static LabourAllocation AllocateLabour(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var workers = Math.Max(0, settlement.Population) * rules.WorkforceShare;
        var builders = 0f;
        if (WantsConstruction(rules, settlement) && CanFundConstruction(rules, settlement))
        {
            var remainingWork = Math.Max(0f, ComputeHouseWork(rules, settlement) - settlement.ConstructionProgress);
            builders = Math.Min(Math.Min(workers * rules.MaxBuilderShare, rules.BuildersPerSite), remainingWork);
        }

        var worshippers = Math.Min(workers - builders, workers * rules.WorshipWorkforceShare * ComputeDevotion(settlement) * CivicModel.WorshipFactor(rules, settlement));
        return new LabourAllocation(workers, workers - builders - worshippers, builders, worshippers);
    }

    /// <summary>
    /// How dry the farmland is (ADR-013): 0 while its mean soil moisture is at least
    /// <see cref="SettlementRules.WellWateredSoil"/>, rising to 1 on parched soil.
    /// </summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="land">The land being farmed.</param>
    /// <returns>Drought (0..1).</returns>
    public static float ComputeDrought(SettlementRules rules, FarmlandSurvey land)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return 1f - Math.Min(1f, SimMath.Clamp01(land.MeanSoilMoisture) / rules.WellWateredSoil);
    }

    /// <summary>How dry a settlement's farmland is now (0..1, <see cref="ComputeDrought"/>).</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="regions">Region grid.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Drought (0..1).</returns>
    public static float DroughtOf(SettlementRules rules, RegionGrid regions, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        return ComputeDrought(rules, SurveyFarmland(regions, settlement.RegionId, rules.FarmlandRadius));
    }

    /// <summary>Share of the full yield the farmland's water allows: 1 when well watered, down to <see cref="SettlementRules.DroughtYieldFloor"/> when parched.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="land">The land being farmed.</param>
    /// <returns>Yield factor (0..1).</returns>
    public static float ComputeWaterYield(SettlementRules rules, FarmlandSurvey land)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return 1f - ((1f - rules.DroughtYieldFloor) * ComputeDrought(rules, land));
    }

    /// <summary>
    /// Food harvested in one step with diminishing returns:
    /// <c>FoodPerFarmerPerStep * meanFertility * capacity * (1 - e^(-farmers / capacity))</c>, where
    /// <c>capacity = FarmersPerRegion * land regions</c>, times <c>1 + BlessingYieldBonus * meanBlessing</c>,
    /// the water yield (<see cref="ComputeWaterYield"/>), <c>1 - ScorchYieldLoss * meanScorch</c> and
    /// <c>1 - FloodYieldLoss * meanFlood</c>. A few farmers each harvest about
    /// <c>FoodPerFarmerPerStep * meanFertility</c>; the land caps the total.
    /// </summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="farmers">Workers in the fields.</param>
    /// <param name="land">The land being farmed.</param>
    /// <returns>Food units.</returns>
    public static float ComputeProduction(SettlementRules rules, float farmers, FarmlandSurvey land)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var capacity = rules.FarmersPerRegion * land.LandRegions;
        if (capacity <= 0f || farmers <= 0f)
        {
            return 0f;
        }

        var maximumYield = rules.FoodPerFarmerPerStep * SimMath.Clamp01(land.MeanFertility) * capacity;
        var blessing = 1f + (rules.BlessingYieldBonus * SimMath.Clamp01(land.MeanBlessing));
        var scorch = 1f - (rules.ScorchYieldLoss * SimMath.Clamp01(land.MeanScorch));
        var flood = 1f - (rules.FloodYieldLoss * SimMath.Clamp01(land.MeanFlood));
        return maximumYield * blessing * ComputeWaterYield(rules, land) * scorch * flood * (1f - MathF.Exp(-farmers / capacity));
    }

    /// <summary>
    /// How readily the people find water (0..1, ADR-022): a river or lake in their region or beside
    /// it, plus deep wells, plus shallow wells that dry up with the soil of their own region.
    /// </summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="grid">Region grid.</param>
    /// <param name="regionId">The settlement's region.</param>
    /// <returns>Access to water (0..1).</returns>
    public static float ComputeWaterAccess(SettlementRules rules, RegionGrid grid, int regionId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(grid);
        var own = grid.GetRegion(regionId);
        var fresh = SimMath.Clamp01(own.FreshWater) * rules.FreshWaterAccess;
        var (column, row) = grid.GetCoordinates(regionId);
        for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
        {
            for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
            {
                fresh = Math.Max(fresh, SimMath.Clamp01(grid.Cells[(r * grid.Columns) + c].FreshWater) * rules.NeighbourWaterAccess);
            }
        }

        var shallow = rules.WellAccess * Math.Min(1f, SimMath.Clamp01(own.SoilMoisture) / rules.WellWateredSoil);
        return SimMath.Clamp01(fresh + rules.WellFloor + shallow);
    }

    /// <summary>Most water a settlement's wells and cisterns hold.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="housing">Housing capacity in people.</param>
    /// <returns>Water storage.</returns>
    public static float ComputeWaterStorage(SettlementRules rules, int housing)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Math.Max(0, housing) * rules.WaterStoragePerHousing;
    }

    /// <summary>Share of the population's food need that is met.</summary>
    /// <param name="available">Food on hand for the step: the stores plus the harvest.</param>
    /// <param name="consumption">Food the population needs.</param>
    /// <returns>0 (nothing to eat) to 1 (everyone fed).</returns>
    public static float ComputeFedShare(float available, float consumption) =>
        consumption > 0f ? Math.Min(1f, Math.Max(0f, available) / consumption) : 1f;

    /// <summary>Health after one step: starvation lowers it by the unfed share of the loss rate; fed villagers recover.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="health">Health before the step (0..1).</param>
    /// <param name="fedShare">Share of the need that is met (0..1).</param>
    /// <returns>Health after the step, within 0..1.</returns>
    public static float ComputeHealth(SettlementRules rules, float health, float fedShare)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return fedShare < 1f
            ? Math.Max(0f, health - (rules.StarvationHealthLossPerStep * (1f - SimMath.Clamp01(fedShare))))
            : Math.Min(1f, health + rules.HealthRecoveryPerStep);
    }

    /// <summary>Chance per person per step of a birth: the full-health chance scaled by health.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="health">Population health (0..1).</param>
    /// <returns>A probability.</returns>
    public static double ComputeBirthChance(SettlementRules rules, float health)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.BirthChancePerPersonPerStep * SimMath.Clamp01(health);
    }

    /// <summary>Chance per person per step of a death: the baseline plus the ill-health chance times (1 - health) squared.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="health">Population health (0..1).</param>
    /// <param name="illnessFactor">Share of the deaths from ill health that happen: medicine and a hospital prevent the rest (ADR-023).</param>
    /// <returns>A probability.</returns>
    public static double ComputeDeathChance(SettlementRules rules, float health, float illnessFactor = 1f)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var illness = 1.0 - SimMath.Clamp01(health);
        return rules.DeathChancePerPersonPerStep + (rules.IllHealthDeathChancePerPersonPerStep * illness * illness * illnessFactor);
    }

    /// <summary>
    /// Why the settlement can have no births in a step: nobody lives there, the houses are full, or
    /// there is no surplus (the harvest is below consumption, or the stores after the step hold less
    /// than <c>SurplusFoodPerPerson</c> per person).
    /// </summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state before the step.</param>
    /// <param name="production">Harvest of the step.</param>
    /// <param name="consumption">Consumption of the step.</param>
    /// <param name="foodAfter">Stored food after the step.</param>
    /// <returns>The blocker, or <see cref="BirthBlocker.None"/>.</returns>
    public static BirthBlocker FindBirthBlocker(SettlementRules rules, SettlementState settlement, float production, float consumption, float foodAfter)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        if (settlement.Population <= 0)
        {
            return BirthBlocker.NoPeople;
        }

        if (settlement.Housing <= settlement.Population)
        {
            return BirthBlocker.NoFreeHousing;
        }

        return production < consumption || foodAfter < settlement.Population * rules.SurplusFoodPerPerson
            ? BirthBlocker.NoFoodSurplus
            : BirthBlocker.None;
    }

    /// <summary>Computes everything about the next step except the random births and deaths.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state before the step.</param>
    /// <param name="regions">Region grid, for the farmland.</param>
    /// <returns>The step's outlook.</returns>
    public static SettlementOutlook Assess(SettlementRules rules, SettlementState settlement, RegionGrid regions)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(regions);

        var land = SurveyFarmland(regions, settlement.RegionId, rules.FarmlandRadius);
        var labour = AllocateLabour(rules, settlement);
        var production = ComputeProduction(rules, labour.Farmers, land) * CivicModel.YieldFactor(rules, settlement);
        var consumption = ComputeConsumption(rules, settlement.Population);
        var storage = StorageOf(rules, settlement);
        var available = settlement.Food + production;
        var fedShare = ComputeFedShare(available, consumption);
        var foodAfter = Math.Clamp(available - consumption, 0f, storage);
        var exposure = rules.HomelessHealthLossPerStep * ComputeHomelessShare(settlement);
        var waterAccess = ComputeWaterAccess(rules, regions, settlement.RegionId);
        var drinking = Math.Max(0, settlement.Population) * rules.WaterPerPersonPerStep;
        var drawn = drinking * rules.WaterGatherFactor * waterAccess;
        var waterStorage = WaterStorageOf(rules, settlement);
        var wateredShare = ComputeFedShare(settlement.Water + drawn, drinking);
        var waterAfter = Math.Clamp(settlement.Water + drawn - drinking, 0f, waterStorage);
        var thirst = rules.ThirstHealthLossPerStep * (1f - wateredShare);
        var healed = ComputeHealth(rules, settlement.Health, fedShare);
        healed += (1f - healed) * CivicModel.HealingShare(rules, settlement);
        var healthAfter = Math.Max(0f, healed - exposure - thirst);
        var blocker = FindBirthBlocker(rules, settlement, production, consumption, foodAfter);
        var birthChance = ComputeBirthChance(rules, healthAfter) * CivicModel.BirthFactor(rules, settlement);
        var deathChance = ComputeDeathChance(rules, healthAfter, CivicModel.IllnessDeathFactor(rules, settlement));
        var population = Math.Max(0, settlement.Population);

        return new SettlementOutlook
        {
            Land = land,
            Labour = labour,
            Production = production,
            Consumption = consumption,
            Storage = storage,
            FedShare = fedShare,
            FoodAfter = foodAfter,
            WaterAccess = waterAccess,
            WaterDrawn = drawn,
            WaterNeeded = drinking,
            WaterStorage = waterStorage,
            WateredShare = wateredShare,
            WaterAfter = waterAfter,
            HealthAfter = healthAfter,
            ConstructionWanted = WantsConstruction(rules, settlement),
            ConstructionFunded = CanFundConstruction(rules, settlement),
            BirthBlocker = blocker,
            BirthChance = birthChance,
            DeathChance = deathChance,
            ExpectedBirths = blocker == BirthBlocker.None ? Math.Min(settlement.Housing - population, population * birthChance) : 0.0,
            ExpectedDeaths = Math.Min(population, population * deathChance),
        };
    }
}
