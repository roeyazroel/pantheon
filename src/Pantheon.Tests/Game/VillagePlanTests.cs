using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>How villages lay out their lanes and houses (spec §61, ADR-077).</summary>
public sealed class VillagePlanTests
{
    private static readonly SettlementLayoutSettings Layout = TestContent.SettlementView.Layout;
    private static readonly Vector2 Centre = new(1000f, 1000f);

    [Fact]
    public void TheLanes_RunTheEightWaysARoadCanLeaveARegion()
    {
        var directions = Enumerable.Range(0, VillagePlan.Spokes).Select(VillagePlan.Direction).ToList();

        Assert.Equal(8, directions.Count);
        Assert.All(directions, d => Assert.Equal(1f, d.Length(), 4));
        Assert.All(directions, d => Assert.True(MathF.Abs(MathF.Abs(d.X) - MathF.Abs(d.Y)) < 1e-4f || MathF.Abs(d.X * d.Y) < 1e-4f, "a compass point"));
    }

    [Fact]
    public void AVillagesPlots_AreTheSameWhateverTheReach_SoGrowthOnlyAddsHouses()
    {
        var near = VillagePlan.Plots(3, Centre, Layout, 60f);
        var far = VillagePlan.Plots(3, Centre, Layout, 200f);

        Assert.NotEmpty(near);
        Assert.True(far.Count > near.Count);
        Assert.Equal(near, far.Take(near.Count));
    }

    [Fact]
    public void NoHouse_StandsWhereARoadWouldRun_SoARoadBuiltLaterMovesNoHouse()
    {
        foreach (var id in Enumerable.Range(1, 12))
        {
            foreach (var plot in VillagePlan.Plots(id, Centre, Layout, 120f))
            {
                var ground = plot.Ground - Centre;
                foreach (var direction in Enumerable.Range(0, VillagePlan.Spokes).Select(VillagePlan.Direction))
                {
                    var off = MathF.Abs(Vector2.Dot(ground, new Vector2(-direction.Y, direction.X)));
                    Assert.True(Vector2.Dot(ground, direction) < 0f || off >= Layout.Setback - (Layout.PlotJitter / 2f) - 0.001f, "the house keeps its setback from every lane out");
                }
            }
        }
    }

    [Fact]
    public void Houses_DoNotCrowdEachOther()
    {
        var plots = VillagePlan.Plots(5, Centre, Layout, 120f);

        for (var i = 0; i < plots.Count; i++)
        {
            for (var j = i + 1; j < plots.Count; j++)
            {
                Assert.True(Vector2.Distance(plots[i].Ground, plots[j].Ground) >= 4f, "two houses stand clear of each other");
            }
        }
    }

    [Fact]
    public void TwoVillages_BuildOnDifferentSidesOfTheirLanes()
    {
        var first = VillagePlan.Plots(1, Vector2.Zero, Layout, 90f);
        var second = VillagePlan.Plots(2, Vector2.Zero, Layout, 90f);

        Assert.NotEqual(first.Select(plot => plot.Ground), second.Select(plot => plot.Ground));
    }

    [Fact]
    public void AVillagesLanes_ReachItsHouses_AndAHouseHasAPathToItsDoor()
    {
        var site = IslandSettlements.Site(IslandSettlements.Create(population: 30, housing: 200, constructionProgress: 0f));
        var lanes = VillagePlan.Lanes(site, Layout);
        var paths = VillagePlan.Paths(site, Layout);

        Assert.NotEmpty(lanes);
        Assert.All(lanes, lane => Assert.True(lane.Count >= 2));
        Assert.Equal(site.Houses.Count, paths.Count);
        Assert.All(site.Houses.Zip(paths), pair =>
        {
            var (house, path) = pair;
            var ground = new Vector2(house.Position.X, house.Position.Z);
            Assert.Equal(Layout.Setback, Vector2.Distance(ground, path[^1]), 3);
        });
    }

    [Fact]
    public void AVillageWithoutHouses_HasNoLanes()
    {
        var site = IslandSettlements.Site(IslandSettlements.Create(population: 30, housing: 0, constructionProgress: 0f));

        Assert.Empty(VillagePlan.Lanes(site, Layout));
        Assert.Empty(VillagePlan.Paths(site, Layout));
    }

    [Fact]
    public void ALanesTrack_LiesOnTheGround_ItsSectionsCloseTogether()
    {
        var terrain = IslandSettlements.Island.Terrain;
        var at = new Vector2(terrain.WorldSize / 2f, terrain.WorldSize / 2f);

        var strip = RoadLayout.Strip([at, at + new Vector2(30f, 0f), at + new Vector2(30f, 20f)], Layout.LaneWidth, 4, terrain);

        Assert.True(strip.Count >= 30);
        Assert.All(strip, section => Assert.Equal(Math.Max(terrain.SampleHeight(section.Centre.X, section.Centre.Z), 0f) + RoadLayout.Lift, section.Centre.Y, 3));
        Assert.All(strip.Zip(strip.Skip(1)), pair => Assert.InRange(pair.Second.Along - pair.First.Along, 0.01f, RoadLayout.Step + 0.01f));
    }

    [Fact]
    public void AFlowerBedsBlooms_StayOnItsMound()
    {
        var blooms = FlowerBedMesh.Build();

        Assert.NotEmpty(blooms.Indices);
        Assert.All(blooms.Positions, p => Assert.InRange(new Vector2(p.X, p.Z).Length(), 0f, FlowerBedMesh.Radius + 0.2f));
        Assert.All(blooms.Positions, p => Assert.InRange(p.Y, -0.1f, FlowerBedMesh.Height + 0.4f));
    }
}
