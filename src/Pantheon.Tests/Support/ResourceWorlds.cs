using Pantheon.Domain.Ecology;
using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Tests.Support;

/// <summary>Worlds and rules for wood, stone and roads (ADR-033).</summary>
internal static class ResourceWorlds
{
    /// <summary>The shipped resource rules.</summary>
    public static ResourceRules Resources => TestContent.Shipped.SettlementRules.Resources;

    /// <summary>The shipped road rules.</summary>
    public static RoadRules Roads => TestContent.Shipped.SettlementRules.Roads;

    /// <summary>The pinned settlement rules with the shipped gathering and roads.</summary>
    public static SettlementRules Rules { get; } = SettlementWorlds.Rules with { Resources = TestContent.Shipped.SettlementRules.Resources, Roads = TestContent.Shipped.SettlementRules.Roads };

    /// <summary>Gives every land region a whole forest of the given timber and the given stone.</summary>
    /// <param name="world">World state.</param>
    /// <param name="timber">Forest and timber.</param>
    /// <param name="stone">Stone.</param>
    /// <returns>The world.</returns>
    public static WorldState Wooded(WorldState world, float timber, float stone)
    {
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome != Biome.Sea)
            {
                (region.Forest, region.Timber, region.Stone) = (timber, timber, stone);
            }
        }

        return world;
    }
}
