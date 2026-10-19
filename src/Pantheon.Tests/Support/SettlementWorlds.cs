using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Tests.Support;

/// <summary>Small hand-built worlds with one settlement, for focused settlement tests.</summary>
internal static class SettlementWorlds
{
    /// <summary>Regions per side of the grid. With the shipped farmland radius of 2 the settlement farms all of it.</summary>
    public const int RegionsPerSide = 5;

    /// <summary>Side of one region in metres.</summary>
    public const float RegionSize = 64f;

    /// <summary>Id of the centre region, where the settlement stands.</summary>
    public const int CenterRegionId = 12;

    /// <summary>
    /// Rules with fixed values (the balance shipped with Milestone 2), so rule tests keep their
    /// meaning when <c>settlement_rules.json</c> is retuned.
    /// </summary>
    public static SettlementRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        WorkforceShare = 0.6f,
        FarmlandRadius = 2,
        FarmersPerRegion = 12f,
        BlessingYieldBonus = 0.6f,
        WellWateredSoil = 0.3f,
        DroughtYieldFloor = 0.25f,
        DroughtThreshold = 0.35f,
        ScorchYieldLoss = 0.7f,
        HomelessHealthLossPerStep = 0.02f,
        RuinWorkShare = 0.5f,
        WorshipWorkforceShare = 0.15f,
        FoodPerFarmerPerStep = 0.3f,
        FoodPerPersonPerStep = 0.05f,
        FoodStoragePerHousing = 8f,
        SurplusFoodPerPerson = 2f,
        StarvationHealthLossPerStep = 0.02f,
        HealthRecoveryPerStep = 0.005f,
        BirthChancePerPersonPerStep = 0.004,
        DeathChancePerPersonPerStep = 0.0004,
        IllHealthDeathChancePerPersonPerStep = 0.02,
        HousingPerHouse = 5,
        ConstructionStartCrowding = 0.85f,
        MaxBuilderShare = 0.2f,
        BuildersPerSite = 10f,
        WorkPerHouse = 100f,
        ConstructionFoodReserveSteps = 20f,
        MaxHousing = 1000,

        // Nobody drinks, so the rule tests of Milestone 2 never meet thirst (ADR-022).
        FloodYieldLoss = 0.8f,
        WaterPerPersonPerStep = 0f,
        WaterStoragePerHousing = 6f,
        WaterGatherFactor = 1.5f,
        FreshWaterAccess = 1f,
        NeighbourWaterAccess = 0.8f,
        WellFloor = 0.35f,
        WellAccess = 0.6f,
        ThirstHealthLossPerStep = 0.015f,
    };

    /// <summary><see cref="Rules"/> without births or deaths, so food, health and construction can be tested alone.</summary>
    public static SettlementRules NoBirthsOrDeaths => Rules with
    {
        BirthChancePerPersonPerStep = 0.0,
        DeathChancePerPersonPerStep = 0.0,
        IllHealthDeathChancePerPersonPerStep = 0.0,
    };

    /// <summary>Creates a 5x5 grid of grassland regions with one settlement on the centre region.</summary>
    /// <param name="population">Inhabitants.</param>
    /// <param name="housing">Housing capacity.</param>
    /// <param name="food">Stored food.</param>
    /// <param name="fertility">Fertility of every region.</param>
    /// <param name="health">Starting health.</param>
    /// <param name="constructionProgress">Work already done on a house.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(int population, int housing, float food, float fertility, float health = 1f, float constructionProgress = 0f)
    {
        var cells = Enumerable.Range(0, RegionsPerSide * RegionsPerSide)
            .Select(id => new RegionState
            {
                Id = id,
                Biome = fertility > 0f ? Biome.Grassland : Biome.Highland,
                Elevation = 10f,
                Temperature = 0.6f,
                Moisture = 0.5f,
                Fertility = fertility,
                Vegetation = 0.5f,
                Blessing = 0f,
                SoilMoisture = 0.5f,
                Fire = 0f,
                Scorch = 0f,
                FreshWater = 0f,
                Flood = 0f,
            })
            .ToList();
        var grid = new RegionGrid { Columns = RegionsPerSide, Rows = RegionsPerSide, RegionSize = RegionSize, Cells = cells };

        return new WorldState
        {
            Seed = new WorldSeed(1),
            Generation = TestContent.SmallWorldGen,
            Tick = 0,
            Random = new SimulationRandom(42).GetState(),
            Regions = grid,
            Settlements = new List<SettlementState>
            {
                new()
                {
                    Id = SettlementFounder.StartingSettlementId,
                    Name = "Testvale",
                    RegionId = CenterRegionId,
                    Position = grid.GetRegionCenter(CenterRegionId),
                    Population = population,
                    Food = food,
                    Water = 100f,
                    Housing = housing,
                    Health = health,
                    ConstructionProgress = constructionProgress,
                    Ruins = 0,
                    ShieldedUntilTick = 0,
                    Emigration = 0f,
                    OldFaith = null,
                    SanctuaryGodId = null,
                    SanctuaryUntilTick = 0,
                    Beliefs = [],
                },
            },
            Gods = [],
            Creatures = [],
            Diplomacy = new Pantheon.Domain.Diplomacy.DiplomacyState { Relations = [], Stances = [], Proposals = [] },
            Weather = WeatherWorlds.Calm(),
            Disasters = new Pantheon.Domain.Disasters.DisasterState { Earthquakes = [], NextEarthquakeId = 1 },
            Migration = new MigrationState { Groups = [], NextGroupId = 1 },
            WinnerGodIds = [],
            History = [],
            HolySites = [],
            Water = new Pantheon.Domain.Water.WaterState { Rivers = [], Lakes = [] },
        };
    }

    /// <summary>The world's only settlement.</summary>
    /// <param name="world">World made by <see cref="Create"/>.</param>
    /// <returns>The settlement.</returns>
    public static SettlementState Settlement(WorldState world) => world.Settlements[0];

    /// <summary>Runs whole settlement steps through the engine, as the game does.</summary>
    /// <param name="world">World to advance.</param>
    /// <param name="rules">Rules to run with.</param>
    /// <param name="steps">Number of settlement steps.</param>
    public static void RunSteps(WorldState world, SettlementRules rules, int steps) =>
        new SimulationEngine(rules).Run(world, steps * rules.StepIntervalTicks);
}
