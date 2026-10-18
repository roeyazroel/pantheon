using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Tests.Support;

/// <summary>Hand-built worlds with a god and settlements in a row, for belief, influence and miracle tests.</summary>
internal static class DivineWorlds
{
    /// <summary>Regions per side of the grid.</summary>
    public const int RegionsPerSide = 16;

    /// <summary>Side of one region in metres.</summary>
    public const float RegionSize = 64f;

    /// <summary>Id of the god of these worlds.</summary>
    public const int GodId = 1;

    /// <summary>
    /// Divine rules with fixed values (the balance shipped with Milestone 3), so rule tests keep
    /// their meaning when <c>divine_rules.json</c> is retuned.
    /// </summary>
    public static DivineRules Rules { get; } = new()
    {
        StepIntervalTicks = 10,
        PowerPerWorshipperPerStep = 1.5f,
        BaseWorshipIntensity = 0.5f,
        LoveWorshipIntensity = 0.3f,
        FearWorshipIntensity = 0.6f,
        ShrineInfrastructure = 1f,
        ComfortFoodPerPerson = 2f,
        DroughtWorry = 0.5f,
        DangerWorry = 0.6f,
        DisasterFear = 0.4f,
        DisasterDoubt = 0.3f,
        FaithFromWorship = 0.01f,
        FaithErosionFromDoubt = 0.01f,
        FaithForgettingPerStep = 0.008f,
        LoveFromComfort = 0.004f,
        LoveDecayPerStep = 0.002f,
        LoveLossFromHardship = 0.01f,
        FearDecayPerStep = 0.006f,
        SilenceDoubt = 0.25f,
        DoubtDriftPerStep = 0.004f,
        DoubtFromHardship = 0.02f,
        NewBelieverDoubt = 0.5f,
        ConversionPerStep = 0.05f,
        BaseConversion = 0.3f,
        LoveConversion = 0.5f,
        FearConversion = 0.9f,
        ApostasyPerStep = 0.04f,
        BaseApostasy = 0.5f,
        FearFragility = 1f,
        LoveLoyalty = 0.6f,
        ApostateFreedom = 0.3f,
        ForgottenFaith = 0.01f,
        InfluenceBaseRadius = 400f,
        InfluenceRadiusPerSqrtFollower = 20f,
        InfluenceStrength = 2.5f,
        CreatureInfluenceStrength = 0.5f,
        CreatureInfluenceReach = 220f,
        WitnessRadius = 350f,
        SettlementRadius = 60f,
        WeakInfluenceCostFactor = 1f,
        GratitudeFloor = 0.25f,
        RivalMiracleDoubt = 0.06f,
        ContestedCostFactor = 1f,
        VictoryShare = 0.6f,
        VictorySeconds = 600f,
        PantheonVictory = true,
        BlessingDecayPerStep = 0.008f,
        Ranks =
        [
            new RankThreshold { Rank = DivineRank.LesserGod, Followers = 100 },
            new RankThreshold { Rank = DivineRank.God, Followers = 400 },
        ],
    };

    /// <summary>The shipped miracles.</summary>
    public static MiracleBook Miracles => TestContent.Shipped.Miracles;

    /// <summary>
    /// A 16 × 16 grassland world with one settlement per given x position on the middle row and one
    /// human god with <paramref name="power"/> Divine Power. No settlement knows the god yet.
    /// </summary>
    /// <param name="power">The god's Divine Power.</param>
    /// <param name="positionsX">X positions of the settlements in metres; all stand at z = 512.</param>
    /// <returns>The world.</returns>
    public static WorldState Create(float power, params float[] positionsX)
    {
        var cells = Enumerable.Range(0, RegionsPerSide * RegionsPerSide)
            .Select(id => new RegionState
            {
                Id = id,
                Biome = Biome.Grassland,
                Elevation = 10f,
                Temperature = 0.6f,
                Moisture = 0.5f,
                Fertility = 0.6f,
                Vegetation = 0.5f,
                Blessing = 0f,
                SoilMoisture = 0.5f,
                Fire = 0f,
                Scorch = 0f,
            })
            .ToList();
        var grid = new RegionGrid { Columns = RegionsPerSide, Rows = RegionsPerSide, RegionSize = RegionSize, Cells = cells };
        var settlements = new List<SettlementState>();
        for (var i = 0; i < positionsX.Length; i++)
        {
            var position = new GroundPosition(positionsX[i], 512f);
            grid.TryGetRegionIdAt(position.X, position.Z, out var regionId);
            settlements.Add(new SettlementState
            {
                Id = i + 1,
                Name = "Town" + (i + 1),
                RegionId = regionId,
                Position = position,
                Population = 100,
                Food = 400f,
                Housing = 120,
                Health = 1f,
                ConstructionProgress = 0f,
                Ruins = 0,
                ShieldedUntilTick = 0,
                Beliefs = [],
            });
        }

        return new WorldState
        {
            Seed = new WorldSeed(3),
            Generation = TestContent.SmallWorldGen,
            Tick = 0,
            Random = new SimulationRandom(9).GetState(),
            Regions = grid,
            Settlements = settlements,
            Gods = [God(GodId, "Testa", GodController.Human, GodPersonality.None, power)],
            Creatures = [],
            Diplomacy = new Pantheon.Domain.Diplomacy.DiplomacyState { Relations = [], Stances = [], Proposals = [] },
            Weather = WeatherWorlds.Calm(),
            Disasters = new Pantheon.Domain.Disasters.DisasterState { Earthquakes = [], NextEarthquakeId = 1 },
            WinnerGodIds = [],
        };
    }

    /// <summary>A god with no Creature, no cooldowns and no dominance.</summary>
    /// <param name="id">Id.</param>
    /// <param name="name">Name.</param>
    /// <param name="controller">Player or AI.</param>
    /// <param name="personality">AI personality.</param>
    /// <param name="power">Divine Power.</param>
    /// <returns>The god.</returns>
    public static GodState God(int id, string name, GodController controller, GodPersonality personality, float power) => new()
    {
        Id = id,
        Name = name,
        Controller = controller,
        Personality = personality,
        DivinePower = power,
        Rank = DivineRank.MinorSpirit,
        DominantSinceTick = null,
        CreatureId = null,
        Cooldowns = [],
    };

    /// <summary>Gives a settlement a belief in the god.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <param name="adherence">Share of followers.</param>
    /// <param name="faith">Faith.</param>
    /// <param name="love">Love.</param>
    /// <param name="fear">Fear.</param>
    /// <param name="doubt">Doubt.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The belief.</returns>
    public static GodBelief Believe(SettlementState settlement, float adherence, float faith, float love = 0f, float fear = 0f, float doubt = 0f, int godId = GodId)
    {
        var belief = new GodBelief { GodId = godId, Adherence = adherence, Faith = faith, Love = love, Fear = fear, Doubt = doubt };
        settlement.Beliefs.Add(belief);
        return belief;
    }
}
