using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Resources;

/// <summary>Checks <see cref="ResourceRules"/> and <see cref="RoadRules"/> (ADR-033) into the settlement check's errors.</summary>
internal static class ResourceRulesValidator
{
    /// <summary>Adds an error for every invalid resource or road rule.</summary>
    /// <param name="errors">Errors so far.</param>
    /// <param name="resources">Resource rules.</param>
    /// <param name="roads">Road rules.</param>
    public static void Check(RuleErrors errors, ResourceRules resources, RoadRules roads)
    {
        errors.RequireAtLeast(resources.GatherRadius, 0, "Resources.GatherRadius");
        foreach (var (value, name) in new[]
        {
            (resources.GatherShare, "Resources.GatherShare"),
            (resources.TimberRegrowthPerSecond, "Resources.TimberRegrowthPerSecond"),
            (resources.ClearedWildlife, "Resources.ClearedWildlife"),
        })
        {
            errors.RequireShare(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (resources.WoodPerGathererPerStep, "Resources.WoodPerGathererPerStep"), (resources.StonePerGathererPerStep, "Resources.StonePerGathererPerStep"),
            (resources.WoodPerHousing, "Resources.WoodPerHousing"), (resources.StonePerHousing, "Resources.StonePerHousing"),
            (resources.MinWoodStore, "Resources.MinWoodStore"), (resources.MinStoneStore, "Resources.MinStoneStore"),
            (resources.WoodPerHouse, "Resources.WoodPerHouse"), (resources.TimberPerWood, "Resources.TimberPerWood"),
            (resources.StonePerStone, "Resources.StonePerStone"), (resources.TimberBurnPerSecond, "Resources.TimberBurnPerSecond"),
            (resources.ClearedLandslide, "Resources.ClearedLandslide"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        if (!roads.Enabled)
        {
            return;
        }

        errors.RequireAtLeast(roads.MinPopulation, 1, "Roads.MinPopulation");
        errors.RequireShare(roads.WorkShare, "Roads.WorkShare");
        errors.RequireShare(roads.RepairPerSecond, "Roads.RepairPerSecond");
        errors.RequireShare(roads.DecayPerSecond, "Roads.DecayPerSecond");
        errors.RequireShare(roads.CutWear, "Roads.CutWear");
        foreach (var (value, name) in new[]
        {
            (roads.Range, "Roads.Range"), (roads.MetresPerWorkerPerSecond, "Roads.MetresPerWorkerPerSecond"),
            (roads.StonePerMetre, "Roads.StonePerMetre"), (roads.Width, "Roads.Width"), (roads.TradeWealthPerSecond, "Roads.TradeWealthPerSecond"),
            (roads.FoodReliefPerSecond, "Roads.FoodReliefPerSecond"), (roads.HungerSteps, "Roads.HungerSteps"), (roads.WearPerSecond, "Roads.WearPerSecond"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        if (roads.Speed < 1f)
        {
            errors.Add("Roads.Speed must be at least 1.");
        }
    }
}
