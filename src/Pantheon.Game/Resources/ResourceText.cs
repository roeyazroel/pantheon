using System;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Resources;
using Pantheon.Simulation.Roads;

namespace Pantheon.Game.Resources;

/// <summary>Words for wood, stone and roads (ADR-033). Pure and culture-invariant.</summary>
public static class ResourceText
{
    /// <summary>A village's stores of wood and stone against what it wants, and its gatherers.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="gatherers">Its gatherers this step.</param>
    /// <returns>For example "Wood 23 of 30   stone 8 of 20   3 gathering".</returns>
    public static string Stores(SettlementRules rules, SettlementState settlement, float gatherers)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var (wood, stone) = ResourceModel.Wanted(rules.Resources, settlement);
        return Format("Wood {0:0} of {1:0}   stone {2:0} of {3:0}   {4}", settlement.Wood, wood, settlement.Stone, stone, gatherers >= 0.5f ? Format("{0:0} gathering", gatherers) : "nobody gathering");
    }

    /// <summary>A village's roads, to each neighbour, with how far each is built or whether it is cut.</summary>
    /// <param name="rules">Road rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>For example "Roads: to Elar, to Valen (60% built), to Kell (cut)".</returns>
    public static string Roads(RoadRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        var roads = world.Roads.Where(r => r.FromSettlementId == settlement.Id || r.ToSettlementId == settlement.Id).OrderBy(r => r.Id)
            .Select(r => "to " + Village(world, r.FromSettlementId == settlement.Id ? r.ToSettlementId : r.FromSettlementId) + State(rules, r)).ToList();
        return roads.Count == 0 ? "Roads: none yet" : "Roads: " + string.Join(", ", roads);
    }

    /// <summary>A road's name.</summary>
    /// <param name="world">World state.</param>
    /// <param name="road">The road.</param>
    /// <returns>For example "the road from Elar to Valen".</returns>
    public static string Name(WorldState world, RoadState road)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(road);
        return Format("the road from {0} to {1}", Village(world, road.FromSettlementId), Village(world, road.ToSettlementId));
    }

    /// <summary>How far a road is built, or whether it is cut, as a suffix; nothing for an open road.</summary>
    /// <param name="rules">Road rules.</param>
    /// <param name="road">The road.</param>
    /// <returns>For example " (60% built)" or " (cut)".</returns>
    public static string State(RoadRules rules, RoadState road)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(road);
        return road.Built < road.Length ? Format(" ({0:0%} built)", road.Length > 0f ? road.Built / road.Length : 0f)
            : RoadModel.IsOpen(rules, road) ? string.Empty : " (cut)";
    }

    /// <summary>The island's forests and roads in a line, for the book.</summary>
    /// <param name="world">World state.</param>
    /// <returns>For example "Forests: 82% standing. 6 roads join the villages, and 2 more are being built."</returns>
    public static string World(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var forest = 0f;
        var timber = 0f;
        foreach (var region in world.Regions.Cells)
        {
            (forest, timber) = (forest + region.Forest, timber + region.Timber);
        }

        var built = world.Roads.FindAll(r => r.Built >= r.Length).Count;
        return Format(
            "Forests: {0:0%} standing. {1} the villages{2}.",
            forest > 0f ? timber / forest : 0f,
            built switch { 0 => "No road joins", 1 => "1 road joins", _ => Format("{0} roads join", built) },
            world.Roads.Count > built ? Format(", and {0} more are being built", world.Roads.Count - built) : string.Empty);
    }

    /// <summary>A village's name.</summary>
    internal static string Village(WorldState world, int id) => world.Settlements.Find(s => s.Id == id)?.Name ?? "a lost village";

    /// <summary>Invariant-culture formatting.</summary>
    internal static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
