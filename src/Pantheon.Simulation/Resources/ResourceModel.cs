using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Resources;

/// <summary>
/// Wood and stone as pure functions and state changes (spec §5, §29, ADR-033): what a village wants
/// in store, how many of its workers gather, what they bring in from the land around and what that
/// takes from the land, and the wood a house takes.
/// </summary>
public static class ResourceModel
{
    /// <summary>The wood and stone a village wants in store: some for each place of housing, and a least amount.</summary>
    /// <param name="rules">Resource rules.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>Wood and stone wanted.</returns>
    public static (float Wood, float Stone) Wanted(ResourceRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var housing = Math.Max(0, settlement.Housing);
        return (Math.Max(rules.MinWoodStore, rules.WoodPerHousing * housing), Math.Max(rules.MinStoneStore, rules.StonePerHousing * housing));
    }

    /// <summary>How far short of what it wants each store is (0 when full, 1 when empty).</summary>
    /// <param name="rules">Resource rules.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The wood's and the stone's shortfall.</returns>
    public static (float Wood, float Stone) Shortfall(ResourceRules rules, SettlementState settlement)
    {
        var (wood, stone) = Wanted(rules, settlement);
        return (wood > 0f ? SimMath.Clamp01(1f - (settlement.Wood / wood)) : 0f, stone > 0f ? SimMath.Clamp01(1f - (settlement.Stone / stone)) : 0f);
    }

    /// <summary>Workers who gather this step: <see cref="ResourceRules.GatherShare"/> of the workforce at the larger shortfall, within those free to.</summary>
    /// <param name="rules">Resource rules.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="workers">The workforce.</param>
    /// <param name="free">Workers not building or worshipping.</param>
    /// <returns>Gatherers.</returns>
    public static float Gatherers(ResourceRules rules, SettlementState settlement, float workers, float free)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!rules.Enabled || settlement.Population <= 0)
        {
            return 0f;
        }

        var (wood, stone) = Shortfall(rules, settlement);
        return Math.Clamp(workers * rules.GatherShare * Math.Max(wood, stone), 0f, Math.Max(0f, free));
    }

    /// <summary>The wood the next house takes when it is begun: less for a ruin rebuilt.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>Wood.</returns>
    public static float HouseWood(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        return rules.Resources.WoodPerHouse * (settlement.Ruins > 0 ? rules.RuinWorkShare : 1f);
    }

    /// <summary>Whether a house can be worked on: one is under way, or there is wood to begin one.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The village.</param>
    /// <returns><c>true</c> when builders may work.</returns>
    public static bool HasWoodForHouse(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        return !rules.Resources.Enabled || settlement.ConstructionProgress > 0f || settlement.Wood >= HouseWood(rules, settlement);
    }

    /// <summary>How much of the land's forest is cut or burnt (0..1); none where it grows none.</summary>
    /// <param name="region">The region.</param>
    /// <returns>The cleared share.</returns>
    public static float Cleared(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Forest > 0.01f ? SimMath.Clamp01(1f - (region.Timber / region.Forest)) : 0f;
    }

    /// <summary>The land a village gathers from: the land regions within the rules' radius of its own, and their mean timber and stone.</summary>
    /// <param name="rules">Resource rules.</param>
    /// <param name="grid">Region grid.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The regions' count and their mean timber and stone.</returns>
    public static (int Count, float Timber, float Stone) Survey(ResourceRules rules, RegionGrid grid, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settlement);
        var (count, timber, stone) = (0, 0f, 0f);
        foreach (var region in Around(rules, grid, settlement))
        {
            (count, timber, stone) = (count + 1, timber + SimMath.Clamp01(region.Timber), stone + SimMath.Clamp01(region.Stone));
        }

        return count == 0 ? (0, 0f, 0f) : (count, timber / count, stone / count);
    }

    /// <summary>
    /// The gatherers' work for one step: they split between wood and stone by the stores'
    /// shortfalls, bring in as much as the land around holds, and take it from the land in
    /// proportion to what each region holds.
    /// </summary>
    /// <param name="rules">Resource rules.</param>
    /// <param name="grid">Region grid; its timber and stone fall.</param>
    /// <param name="settlement">The village; its stores rise.</param>
    /// <param name="gatherers">Gatherers this step.</param>
    /// <returns>The wood and stone brought in.</returns>
    public static (float Wood, float Stone) Gather(ResourceRules rules, RegionGrid grid, SettlementState settlement, float gatherers)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settlement);
        var (woodShort, stoneShort) = Shortfall(rules, settlement);
        if (gatherers <= 0f || woodShort + stoneShort <= 0f)
        {
            return (0f, 0f);
        }

        var (count, timber, stone) = Survey(rules, grid, settlement);
        if (count == 0)
        {
            return (0f, 0f);
        }

        var woodGatherers = gatherers * woodShort / (woodShort + stoneShort);
        var wood = woodGatherers * rules.WoodPerGathererPerStep * timber;
        var quarried = (gatherers - woodGatherers) * rules.StonePerGathererPerStep * stone;
        var (timberTaken, stoneTaken) = (wood * rules.TimberPerWood / (timber * count), quarried * rules.StonePerStone / (stone * count));
        foreach (var region in Around(rules, grid, settlement))
        {
            // Each region gives in proportion to what it holds.
            region.Timber = timber > 0f ? Math.Max(0f, region.Timber - (region.Timber * timberTaken)) : region.Timber;
            region.Stone = stone > 0f ? Math.Max(0f, region.Stone - (region.Stone * stoneTaken)) : region.Stone;
        }

        settlement.Wood += wood;
        settlement.Stone += quarried;
        return (wood, quarried);
    }

    /// <summary>The land regions within the rules' radius of the village's own.</summary>
    private static System.Collections.Generic.IEnumerable<RegionState> Around(ResourceRules rules, RegionGrid grid, SettlementState settlement)
    {
        var (column, row) = grid.GetCoordinates(settlement.RegionId);
        var radius = Math.Max(0, rules.GatherRadius);
        for (var r = Math.Max(0, row - radius); r <= Math.Min(grid.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(grid.Columns - 1, column + radius); c++)
            {
                var region = grid.Cells[(r * grid.Columns) + c];
                if (region.Biome != Biome.Sea)
                {
                    yield return region;
                }
            }
        }
    }
}
