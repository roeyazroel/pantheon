using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Society;

/// <summary>The rules of settlements' society (spec §29, §30, §36, ADR-023) as pure functions of the rules and the world.</summary>
public static class SocietyModel
{
    // A god must hold this share of a settlement to count as one of two at war over it.
    private const float WarPresence = 0.1f;

    /// <summary>How far gods at war over a settlement contest it: 1 when two at war each hold half of its people.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>War over it (0..1).</returns>
    public static float War(WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        var war = 0f;
        var beliefs = settlement.Beliefs;
        for (var i = 0; i < beliefs.Count; i++)
        {
            for (var j = i + 1; j < beliefs.Count; j++)
            {
                var weaker = Math.Min(beliefs[i].Adherence, beliefs[j].Adherence);
                if (weaker >= WarPresence && DiplomacyModel.AreAtWar(world, beliefs[i].GodId, beliefs[j].GodId))
                {
                    war = Math.Max(war, SimMath.Clamp01(2f * weaker));
                }
            }
        }

        return war;
    }

    /// <summary>The safety the settlement tends to: danger and war lower it; walls and watchmen raise it.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="danger">Its danger (0..1).</param>
    /// <param name="war">War over it (0..1).</param>
    /// <returns>The target (0..1).</returns>
    public static float SafetyTarget(SocietyRules rules, SettlementState settlement, float danger, float war)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var guarded = CivicModel.Has(settlement, BuildingKind.Walls) || CivicModel.Puts(settlement, SettlementPriority.Defence);
        return SimMath.Clamp01(1f - danger - (rules.WarUnsafety * war) + (guarded ? rules.DefenceSafety : 0f));
    }

    /// <summary>The happiness the settlement tends to: the weighted goods of its life less fear and homelessness.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="foodNeed">How empty its stores are (0..1).</param>
    /// <param name="thirst">How low its wells run (0..1).</param>
    /// <returns>The target (0..1).</returns>
    public static float HappinessTarget(SocietyRules rules, SettlementState settlement, float foodNeed, float thirst)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var weights = rules.Happiness;
        var feelings = MigrationModel.FollowersFeelings(settlement.Beliefs);
        var good = (weights.Food * (1f - SimMath.Clamp01(foodNeed)))
            + (weights.Health * SimMath.Clamp01(settlement.Health))
            + (weights.Water * (1f - SimMath.Clamp01(thirst)))
            + (weights.Safety * SimMath.Clamp01(settlement.Society.Safety))
            + (weights.Love * SimMath.Clamp01(feelings.Love));
        var bad = (weights.Fear * SimMath.Clamp01(feelings.Fear)) + (weights.Homeless * SettlementEconomy.ComputeHomelessShare(settlement));
        return SimMath.Clamp01(good - bad);
    }

    /// <summary>How united the settlement's faith is: the share of its people in its largest group, those who follow no god included.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Unity (0..1).</returns>
    public static float Unity(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        var followed = 0f;
        var largest = 0f;
        foreach (var belief in settlement.Beliefs)
        {
            followed += SimMath.Clamp01(belief.Adherence);
            largest = Math.Max(largest, SimMath.Clamp01(belief.Adherence));
        }

        return Math.Max(largest, SimMath.Clamp01(1f - followed));
    }

    /// <summary>The stability the settlement tends to: happiness, unity of faith and peace, less the share keeping an Old Faith.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="war">War over it (0..1).</param>
    /// <returns>The target (0..1).</returns>
    public static float StabilityTarget(SocietyRules rules, SettlementState settlement, float war)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var weights = rules.Stability;
        var schism = settlement.OldFaith?.Share ?? 0f;
        return SimMath.Clamp01((weights.Happiness * settlement.Society.Happiness) + (weights.Unity * Unity(settlement)) + (weights.Peace * (1f - war)) - (weights.Schism * schism));
    }

    /// <summary>The other living settlements within trade range of one (Chebyshev distance in regions).</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Its neighbours, in world order.</returns>
    public static List<SettlementState> Neighbours(SocietyRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var (column, row) = world.Regions.GetCoordinates(settlement.RegionId);
        return world.Settlements.FindAll(other =>
        {
            var (c, r) = world.Regions.GetCoordinates(other.RegionId);
            return other.Id != settlement.Id && other.Population > 0 && Math.Max(Math.Abs(c - column), Math.Abs(r - row)) <= rules.TradeRange;
        });
    }

    /// <summary>
    /// Wealth gained in a step: goods made from the harvest beyond what is eaten while the stores are
    /// full, and trade with each neighbour (more with navigation); less what is used up.
    /// </summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="surplus">Food harvested beyond what was eaten, per second.</param>
    /// <param name="storesFull">Whether the stores are at least <see cref="SocietyRules.FullStores"/> full.</param>
    /// <param name="neighbours">Living settlements within trade range.</param>
    /// <param name="seconds">Seconds of the step.</param>
    /// <returns>The change in wealth.</returns>
    public static float WealthChange(SocietyRules rules, SettlementState settlement, float surplus, bool storesFull, int neighbours, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var navigation = 1f + (settlement.Society.Technology.Navigation / rules.MaxTechnology);
        var made = storesFull ? rules.SurplusWealth * Math.Max(0f, surplus) : 0f;
        var trade = rules.TradePerNeighbour * neighbours * navigation;
        return ((made + trade) * seconds) - (rules.WealthUsePerSecond * settlement.Society.Wealth * seconds);
    }

    /// <summary>Education gained in a step: more with stability, wealth to spare and science; some is forgotten.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="seconds">Seconds of the step.</param>
    /// <returns>The change in education.</returns>
    public static float EducationChange(SocietyRules rules, SettlementState settlement, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var society = settlement.Society;
        var perPerson = society.Wealth / Math.Max(1, settlement.Population);
        var means = Math.Min(1f, perPerson / rules.LearnedWealth);
        var science = 1f + (society.Technology.Science / rules.MaxTechnology);
        var gained = rules.EducationPerSecond * SimMath.Clamp01(society.Stability) * means * science * (1f - society.Education);
        return (gained - (rules.EducationLossPerSecond * society.Education)) * seconds;
    }

    /// <summary>The field a settlement's priority and the building under way favour, if any.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The field, or <c>null</c>.</returns>
    public static TechnologyField? Focus(SocietyRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var society = settlement.Society;
        if (society.Project is { } project && Find(rules, project) is { } building)
        {
            return building.Field;
        }

        return society.Priority switch
        {
            SettlementPriority.Worship => TechnologyField.Religion,
            SettlementPriority.Growth => TechnologyField.Medicine,
            SettlementPriority.Expansion => TechnologyField.Navigation,
            SettlementPriority.None => null,
            SettlementPriority.Food or SettlementPriority.Health or SettlementPriority.Defence or SettlementPriority.Preparation or SettlementPriority.Temple =>
                rules.Buildings is { Count: > 0 } && FirstFor(rules, society.Priority) is { } first ? first.Field : null,
        };
    }

    /// <summary>The definition of a building.</summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="kind">The building.</param>
    /// <returns>Its definition, or <c>null</c> when the rules have none.</returns>
    public static BuildingDefinition? Find(SocietyRules rules, BuildingKind kind)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var building in rules.Buildings)
        {
            if (building.Kind == kind)
            {
                return building;
            }
        }

        return null;
    }

    /// <summary>
    /// The building a settlement's priority calls for next, if it has not raised it: the temple of the
    /// god that set the priority, or the first useful one of the priority's buildings (levees, dams
    /// and irrigation only beside water, sea walls only by the sea).
    /// </summary>
    /// <param name="rules">Society rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The building, or <c>null</c>.</returns>
    public static BuildingDefinition? NextProject(SocietyRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var society = settlement.Society;
        foreach (var building in rules.Buildings)
        {
            if (building.Priority != society.Priority)
            {
                continue;
            }

            var built = building.Kind == BuildingKind.Temple
                ? society.PriorityGodId is not { } god || CivicModel.HasTemple(settlement, god)
                : CivicModel.Has(settlement, building.Kind);
            if (!built && Fits(world, settlement, building.Kind))
            {
                return building;
            }
        }

        return null;
    }

    /// <summary>Whether a building is of use where a settlement stands: by water for levees, dams and irrigation, by the sea for a sea wall.</summary>
    private static bool Fits(WorldState world, SettlementState settlement, BuildingKind kind) => kind switch
    {
        BuildingKind.Levee or BuildingKind.Dam or BuildingKind.Irrigation => NearWater(world, settlement),
        BuildingKind.SeaWall => NearSea(world, settlement),
        BuildingKind.Temple or BuildingKind.Granary or BuildingKind.Cistern or BuildingKind.Firebreak or BuildingKind.Shelter
            or BuildingKind.Hospital or BuildingKind.Walls or BuildingKind.Evacuation => true,
    };

    /// <summary>Whether the sea touches the settlement's region or one beside it.</summary>
    private static bool NearSea(WorldState world, SettlementState settlement)
    {
        var grid = world.Regions;
        var (column, row) = grid.GetCoordinates(settlement.RegionId);
        for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
        {
            for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
            {
                if (grid.Cells[(r * grid.Columns) + c].Biome == Domain.Ecology.Biome.Sea)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Whether a river or lake waters the settlement's region or one beside it.</summary>
    private static bool NearWater(WorldState world, SettlementState settlement)
    {
        var grid = world.Regions;
        var (column, row) = grid.GetCoordinates(settlement.RegionId);
        for (var r = Math.Max(0, row - 1); r <= Math.Min(grid.Rows - 1, row + 1); r++)
        {
            for (var c = Math.Max(0, column - 1); c <= Math.Min(grid.Columns - 1, column + 1); c++)
            {
                if (grid.Cells[(r * grid.Columns) + c].FreshWater > 0.2f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The first building raised under a priority.</summary>
    private static BuildingDefinition? FirstFor(SocietyRules rules, SettlementPriority priority)
    {
        foreach (var building in rules.Buildings)
        {
            if (building.Priority == priority)
            {
                return building;
            }
        }

        return null;
    }
}
