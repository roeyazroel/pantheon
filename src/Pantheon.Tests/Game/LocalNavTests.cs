using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Local navigation (spec §85, ADR-046): figures walk round huts, the shrine and the temple.</summary>
public sealed class LocalNavTests
{
    private static readonly CitizenSettings Citizens = TestContent.SettlementView.Citizens;

    [Fact]
    public void AWay_GoesRoundAHutInTheWay_KeepsOffItsWalls_AndIsFoundOnce()
    {
        var hut = new HutPlacement(new Vector3(100f, 5f, 100f), 0f, 1f);
        var grid = LocalNavGrid.Build(Site([hut]), Village(), FlatTerrain(), Citizens);
        var (from, to) = (new Vector2(90f, 100f), new Vector2(110f, 100f));

        var way = grid.Way(from, to);
        grid.Way(from, to);

        Assert.NotNull(way);
        Assert.Equal(to, way![^1]);
        Assert.All(Walk(from, way), point => Assert.True(Vector2.Distance(point, new Vector2(100f, 100f)) >= LocalNavGrid.HutRadius - 0.05f, $"{point} is inside the hut"));
        Assert.Equal((1, 1), (grid.Searches, grid.Detours));
        Assert.True(grid.IsBlocked(new Vector2(100f, 100f)) && grid.IsBlocked(new Vector2(78f, 78f)) == false);
    }

    [Fact]
    public void AClearWay_IsAStraightLine_AndAWayFromBesideAWallStillLeavesIt()
    {
        var hut = new HutPlacement(new Vector3(100f, 5f, 100f), 0f, 1f);
        var grid = LocalNavGrid.Build(Site([hut]), Village(), FlatTerrain(), Citizens);

        Assert.Null(grid.Way(new Vector2(90f, 90f), new Vector2(110f, 90f)));
        var fromTheWall = grid.Way(new Vector2(100f, 102.3f), new Vector2(100f, 92f));
        Assert.NotNull(fromTheWall);
        Assert.Equal(0, grid.Searches - 1);
    }

    [Fact]
    public void ATemple_StandsInTheWay_OnceItIsBuilt()
    {
        var site = Site([]);
        var village = Village();
        var terrain = FlatTerrain();
        var (temple, _) = CivicLayout.Temple(site, terrain, village.Id);
        var before = LocalNavGrid.Build(site, village, terrain, Citizens);
        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = 1 });

        var after = LocalNavGrid.Build(site, village, terrain, Citizens);

        Assert.False(before.IsBlocked(new Vector2(temple.X, temple.Z)));
        Assert.True(after.IsBlocked(new Vector2(temple.X, temple.Z)));
        Assert.True(after.IsBlocked(new Vector2(site.Center.X, site.Center.Z)));
    }

    [Fact]
    public void AVillagesFigures_NeverWalkThroughAHut_AndKeepTheirRoundsTime()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var village = session.World.Settlements.OrderByDescending(s => s.Population).First();
        var site = SettlementLayout.ComputeSite(village, session.Content.SettlementRules, session.Terrain, TestContent.SettlementView.Layout);
        var crowd = new SettlementCrowd(session.World.Seed, village.Id, TestContent.SettlementView);
        crowd.SelectTier(10f);
        crowd.Refresh(village, SettlementEconomy.AllocateLabour(session.Content.SettlementRules, village), site);
        crowd.Advance(0.0, session.Terrain);
        var cycle = crowd.Roster.Max(c => CitizenRoutine.CycleSeconds(c, Citizens));
        var (inside, straightInside) = (0, 0);

        for (var t = 0.0; t < cycle; t += 0.5)
        {
            foreach (var citizen in crowd.Roster)
            {
                var pose = CitizenRoutine.Evaluate(citizen, t, Citizens, crowd);
                var straight = CitizenRoutine.Evaluate(citizen, t, Citizens);
                inside += pose.Activity == CitizenActivity.Walking && InAHut(site, pose.Ground) ? 1 : 0;
                straightInside += straight.Activity == CitizenActivity.Walking && InAHut(site, straight.Ground) ? 1 : 0;
                Assert.Equal(straight.Activity, pose.Activity);
            }
        }

        Assert.Equal(0, inside);
        Assert.True(straightInside > 0, "the straight lines should have crossed some hut");
        Assert.True(crowd.Navigation!.Detours > 0);
        var status = crowd.GetStatus(village.Population, 10f);
        Assert.Equal((crowd.Navigation.Cells, crowd.Navigation.Detours), (status.Navigation!.Value.Cells, status.Navigation.Value.Detours));
    }

    /// <summary>Whether a point lies inside a hut's walls.</summary>
    private static bool InAHut(SettlementSite site, Vector2 point) =>
        site.Houses.Any(h => Vector2.Distance(new Vector2(h.Position.X, h.Position.Z), point) < (LocalNavGrid.HutRadius * h.Scale) - 0.15f);

    /// <summary>Points every 10 cm along a way.</summary>
    private static IEnumerable<Vector2> Walk(Vector2 from, IReadOnlyList<Vector2> way)
    {
        var at = from;
        foreach (var next in way)
        {
            var steps = (int)MathF.Ceiling(Vector2.Distance(at, next) / 0.1f);
            for (var k = 1; k <= steps; k++)
            {
                yield return Vector2.Lerp(at, next, (float)k / steps);
            }

            at = next;
        }
    }

    /// <summary>A layout with a shrine at (100, 60) and the given huts.</summary>
    private static SettlementSite Site(IReadOnlyList<HutPlacement> huts) =>
        new(new Vector3(100f, 5f, 60f), huts, null, [], 50f, []);

    /// <summary>A village with no buildings.</summary>
    private static Domain.Settlements.SettlementState Village() => SettlementWorlds.Settlement(SettlementWorlds.Create(100, 120, 400f, 0.6f));

    /// <summary>Flat dry ground 5 m above the sea, 256 m across.</summary>
    private static TerrainField FlatTerrain()
    {
        const int side = 129;
        var count = side * side;
        return new TerrainField(side, 2f, Enumerable.Repeat(5f, count).ToArray(), new float[count], new float[count], Enumerable.Repeat(Biome.Grassland, count).ToArray());
    }
}
