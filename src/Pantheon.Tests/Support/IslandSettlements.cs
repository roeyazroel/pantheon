using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Tests.Support;

/// <summary>
/// The starting settlement of the small seed 1234 island in chosen states, laid out with the pinned
/// rules (<see cref="SettlementWorlds.Rules"/>) and the shipped settlement view settings.
/// </summary>
internal static class IslandSettlements
{
    /// <summary>Seed of the island.</summary>
    public static WorldSeed Seed { get; } = new(1234);

    /// <summary>The island, generated once for every test that lays out its settlement.</summary>
    public static GeneratedWorld Island { get; } = WorldGenerator.Generate(Seed, TestContent.SmallWorldGen);

    /// <summary>Pinned settlement rules.</summary>
    public static SettlementRules Rules => SettlementWorlds.Rules;

    /// <summary>Shipped settlement view settings.</summary>
    public static SettlementViewSettings View => TestContent.SettlementView;

    /// <summary>Id of the island's starting settlement.</summary>
    public static int SettlementId => Island.State.Settlements[0].Id;

    /// <summary>The island's starting settlement in the given state, in full health.</summary>
    /// <param name="population">Inhabitants.</param>
    /// <param name="housing">Housing capacity.</param>
    /// <param name="constructionProgress">Work done on the house under construction; above 0 a building site shows.</param>
    /// <param name="food">Stored food; construction only takes builders out of the fields with a reserve.</param>
    /// <returns>A new settlement state.</returns>
    public static SettlementState Create(int population, int housing, float constructionProgress = 0f, float food = 0f)
    {
        var founded = Island.State.Settlements[0];
        return new SettlementState
        {
            Id = founded.Id,
            Name = founded.Name,
            RegionId = founded.RegionId,
            Position = founded.Position,
            Population = population,
            Food = food,
            Housing = housing,
            Health = 1f,
            ConstructionProgress = constructionProgress,
            Ruins = 0,
            ShieldedUntilTick = 0,
            Emigration = 0f,
            OldFaith = null,
            SanctuaryGodId = null,
            SanctuaryUntilTick = 0,
            Beliefs = [],
        };
    }

    /// <summary>Lays the settlement out on the island.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Its site.</returns>
    public static SettlementSite Site(SettlementState settlement) => SettlementLayout.ComputeSite(settlement, Rules, Island.Terrain, View.Layout);

    /// <summary>The settlement's labour split under the pinned rules.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>Workers, farmers and builders.</returns>
    public static LabourAllocation Labour(SettlementState settlement) => SettlementEconomy.AllocateLabour(Rules, settlement);

    /// <summary>The settlement's representative citizens, as the client derives them.</summary>
    /// <param name="settlement">Settlement state.</param>
    /// <returns>The roster.</returns>
    public static IReadOnlyList<RepresentativeCitizen> Roster(SettlementState settlement) =>
        RepresentativeRoster.Build(Seed, settlement, Labour(settlement), Site(settlement), View);

    /// <summary>Ground (X, Z) coordinates of a world position.</summary>
    /// <param name="position">World position.</param>
    /// <returns>Its ground point.</returns>
    public static Vector2 Ground(Vector3 position) => new(position.X, position.Z);
}
