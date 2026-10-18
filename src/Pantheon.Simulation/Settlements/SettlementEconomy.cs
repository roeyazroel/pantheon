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
    /// <returns>The number of farmable regions and their mean fertility. The square is clipped at the grid edge.</returns>
    public static FarmlandSurvey SurveyFarmland(RegionGrid grid, int regionId, int radius)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        _ = grid.GetRegion(regionId);

        var (column, row) = grid.GetCoordinates(regionId);
        var count = 0;
        var fertility = 0f;
        for (var r = Math.Max(0, row - radius); r <= Math.Min(grid.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(grid.Columns - 1, column + radius); c++)
            {
                var region = grid.Cells[(r * grid.Columns) + c];
                if (IsFarmland(region))
                {
                    count++;
                    fertility += SimMath.Clamp01(region.Fertility);
                }
            }
        }

        return new FarmlandSurvey(count, count > 0 ? fertility / count : 0f);
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

    /// <summary>Number of houses that provide a housing capacity; a part-filled last house still counts.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="housing">Housing capacity in people.</param>
    /// <returns><c>ceil(housing / HousingPerHouse)</c>, at least 0.</returns>
    public static int ComputeHouseCount(SettlementRules rules, int housing)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return housing <= 0 ? 0 : ((housing - 1) / rules.HousingPerHouse) + 1;
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
    /// Splits the workforce for one step. Builders work only while a house is wanted and funded, and
    /// are capped by the builder share, the builders one site takes and the work left on the house.
    /// Everyone else who works farms.
    /// </summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Workers, farmers and builders.</returns>
    public static LabourAllocation AllocateLabour(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var workers = Math.Max(0, settlement.Population) * rules.WorkforceShare;
        var builders = 0f;
        if (WantsConstruction(rules, settlement) && CanFundConstruction(rules, settlement))
        {
            var remainingWork = Math.Max(0f, rules.WorkPerHouse - settlement.ConstructionProgress);
            builders = Math.Min(Math.Min(workers * rules.MaxBuilderShare, rules.BuildersPerSite), remainingWork);
        }

        return new LabourAllocation(workers, workers - builders, builders);
    }

    /// <summary>
    /// Food harvested in one step with diminishing returns:
    /// <c>FoodPerFarmerPerStep * meanFertility * capacity * (1 - e^(-farmers / capacity))</c>, where
    /// <c>capacity = FarmersPerRegion * land regions</c>. A few farmers each harvest about
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
        return maximumYield * (1f - MathF.Exp(-farmers / capacity));
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
    /// <returns>A probability.</returns>
    public static double ComputeDeathChance(SettlementRules rules, float health)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var illness = 1.0 - SimMath.Clamp01(health);
        return rules.DeathChancePerPersonPerStep + (rules.IllHealthDeathChancePerPersonPerStep * illness * illness);
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
        var production = ComputeProduction(rules, labour.Farmers, land);
        var consumption = ComputeConsumption(rules, settlement.Population);
        var storage = ComputeStorage(rules, settlement.Housing);
        var available = settlement.Food + production;
        var fedShare = ComputeFedShare(available, consumption);
        var foodAfter = Math.Clamp(available - consumption, 0f, storage);
        var healthAfter = ComputeHealth(rules, settlement.Health, fedShare);
        var blocker = FindBirthBlocker(rules, settlement, production, consumption, foodAfter);
        var birthChance = ComputeBirthChance(rules, healthAfter);
        var deathChance = ComputeDeathChance(rules, healthAfter);
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
