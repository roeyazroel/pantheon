using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.HolySites;

/// <summary>
/// Finds the world's holy sites in its land (spec §51, §93, ADR-021): the highest summit, the
/// thickest forest, the wettest land and the open land furthest from any village, one site of
/// each kind, apart from each other and from the settlements. It reads only the regions and the
/// settlements, so a world finds the same sites whenever it is asked.
/// </summary>
public static class HolySitePlacer
{
    // A site stands at least this many regions from a settlement's own region, beyond its farmland, where there is room.
    private const int SettlementClearance = 3;

    /// <summary>Gives a world its holy sites if it has none yet, as a world from an older save (ADR-021).</summary>
    /// <param name="rules">Holy site rules.</param>
    /// <param name="world">World state.</param>
    public static void EnsurePlaced(HolySiteRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.HolySites.Count == 0)
        {
            world.HolySites.AddRange(Place(rules, world));
        }
    }

    /// <summary>The holy sites of a world, in the rules' order, ids from 1.</summary>
    /// <param name="rules">Holy site rules.</param>
    /// <param name="world">World state with its regions and settlements.</param>
    /// <returns>The sites; fewer than the rules list when the land has no room for them.</returns>
    public static List<HolySite> Place(HolySiteRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var grid = world.Regions;
        var sites = new List<HolySite>();
        foreach (var definition in rules.Sites)
        {
            // Where the land has no room, a site may stand nearer the others and the villages.
            RegionState? best = null;
            for (var relax = 0; relax <= SettlementClearance && best is null; relax++)
            {
                best = Best(definition.Kind, world, sites, Math.Max(1, SettlementClearance - relax), Math.Max(1, rules.MinimumSpacing - (2 * relax)));
            }

            if (best is not null)
            {
                var position = grid.GetRegionCenter(best.Id);
                sites.Add(new HolySite
                {
                    Id = sites.Count + 1,
                    Kind = definition.Kind,
                    Name = $"{definition.Name} of {NearestSettlement(world, position)}",
                    RegionId = best.Id,
                    Position = position,
                    KeeperGodId = null,
                    ClaimantGodId = null,
                    ClaimTicks = 0,
                });
            }
        }

        return sites;
    }

    /// <summary>The land region that suits a kind of site best, clear of the sites so far and the villages, if any.</summary>
    private static RegionState? Best(HolySiteKind kind, WorldState world, List<HolySite> sites, int clearance, int spacing)
    {
        RegionState? best = null;
        var bestScore = float.NegativeInfinity;
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome == Biome.Sea || !IsClear(world.Regions, region.Id, sites, world, spacing, clearance))
            {
                continue;
            }

            var score = Score(kind, region, world.Regions, world);
            if (score > bestScore)
            {
                bestScore = score;
                best = region;
            }
        }

        return best;
    }

    /// <summary>How well a region suits a kind of site; a kind's own biome always beats any other.</summary>
    private static float Score(HolySiteKind kind, RegionState region, RegionGrid grid, WorldState world) => kind switch
    {
        HolySiteKind.SacredPeak => region.Elevation,
        HolySiteKind.AncientGrove => region.Vegetation + (region.Biome == Biome.Forest ? 10f : 0f),
        HolySiteKind.HolySpring => region.Moisture + (region.Biome == Biome.Highland ? 0f : 10f),
        HolySiteKind.StandingStones => (DistanceToSettlements(grid.GetRegionCenter(region.Id), world) / 1000f) + (region.Biome == Biome.Grassland ? 10f : 0f),
    };

    /// <summary>Whether a region is far enough from the sites placed so far and from every settlement.</summary>
    private static bool IsClear(RegionGrid grid, int regionId, List<HolySite> sites, WorldState world, int spacing, int clearance)
    {
        foreach (var site in sites)
        {
            if (RegionDistance(grid, regionId, site.RegionId) < spacing)
            {
                return false;
            }
        }

        foreach (var settlement in world.Settlements)
        {
            if (RegionDistance(grid, regionId, settlement.RegionId) < clearance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Chebyshev distance between two regions, in regions.</summary>
    private static int RegionDistance(RegionGrid grid, int a, int b)
    {
        var (ac, ar) = grid.GetCoordinates(a);
        var (bc, br) = grid.GetCoordinates(b);
        return Math.Max(Math.Abs(ac - bc), Math.Abs(ar - br));
    }

    /// <summary>Distance from a point to the nearest settlement, in metres; far when there is none.</summary>
    private static float DistanceToSettlements(GroundPosition point, WorldState world)
    {
        var nearest = 1_000_000f;
        foreach (var settlement in world.Settlements)
        {
            nearest = Math.Min(nearest, HolySiteModel.Distance(point, settlement.Position));
        }

        return nearest;
    }

    /// <summary>The name of the settlement nearest a point, or "the Wilds" without one.</summary>
    private static string NearestSettlement(WorldState world, GroundPosition point)
    {
        var name = "the Wilds";
        var nearest = float.MaxValue;
        foreach (var settlement in world.Settlements)
        {
            var distance = HolySiteModel.Distance(point, settlement.Position);
            if (distance < nearest)
            {
                nearest = distance;
                name = settlement.Name;
            }
        }

        return name;
    }
}
