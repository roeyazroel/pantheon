using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Wildlife and bridges (spec §61, ADR-054).</summary>
public sealed class WildlifeTests
{
    private static readonly WildlifeSettings Settings = TestContent.SettlementView.Wildlife;

    [Fact]
    public void HerdsAndFlocks_LiveWhereTheLandsWildlifeDoes_AndNoneWhereItIsHuntedOut()
    {
        var (session, at) = World();
        session.World.Regions.Cells.ForEach(r => r.Wildlife = 1f);
        var teeming = Pose(session, at, 30.0);
        session.World.Regions.Cells.ForEach(r => r.Wildlife = 0f);
        var empty = Pose(session, at, 30.0);

        Assert.Contains(teeming, p => p.Kind == AnimalKind.Deer);
        Assert.Contains(teeming, p => p.Kind == AnimalKind.Bird);
        Assert.Empty(empty);
        Assert.True(teeming.Count(p => p.Kind == AnimalKind.Deer) <= Settings.MaxAnimals && teeming.Count(p => p.Kind == AnimalKind.Bird) <= Settings.MaxAnimals);
        Assert.All(teeming, p => Assert.True(Vector2.Distance(new Vector2(p.Position.X, p.Position.Z), at) <= Settings.Radius + 45f, "a flock wheels at most 42 m about its centre"));
    }

    [Fact]
    public void AHerd_GrazesThenWalksOn_TheSameEachTime_WhileBirdsFlyHighAboveTheGround()
    {
        var (session, at) = World();
        session.World.Regions.Cells.ForEach(r => r.Wildlife = 1f);
        var period = Settings.GrazeSeconds + Settings.WalkSeconds;

        var poses = Enumerable.Range(0, 40).SelectMany(i => Pose(session, at, i * period / 10.0)).ToList();

        Assert.Equal(Pose(session, at, 12.5), Pose(session, at, 12.5));
        Assert.Contains(poses, p => p.Motion == AnimalMotion.Grazing);
        Assert.Contains(poses, p => p.Motion == AnimalMotion.Walking);
        Assert.All(poses.Where(p => p.Kind == AnimalKind.Bird), p => Assert.True(p.Position.Y >= MathF.Max(0f, session.Terrain.SampleHeight(p.Position.X, p.Position.Z)) + 12f, "birds fly high"));
        Assert.All(poses.Where(p => p.Kind == AnimalKind.Deer), p => Assert.Equal(session.Terrain.SampleHeight(p.Position.X, p.Position.Z), p.Position.Y, 3));
        var everywhere = new List<Clearing> { new(at, 5000f) };
        var kept = new List<AnimalPose>();
        Wildlife.Pose(session.World, session.Terrain, at, 30.0, everywhere, Settings, kept);
        Assert.DoesNotContain(kept, p => p.Kind == AnimalKind.Deer);
    }

    [Fact]
    public void Animals_StandOnTheirFeet_WithTheirPartsTagged()
    {
        var deer = AnimalMesh.Build(AnimalKind.Deer);
        var bird = AnimalMesh.Build(AnimalKind.Bird);
        int[] Parts(TaggedMesh mesh) => mesh.Tags.Select(t => (int)t.X).Distinct().Order().ToArray();

        Assert.InRange(deer.Positions.Min(p => p.Y), -0.01f, 0.01f);
        Assert.InRange(deer.Positions.Max(p => p.Y), 2f, 2.5f);
        Assert.Equal([(int)AnimalPart.Body, (int)AnimalPart.Head, (int)AnimalPart.FrontLegs, (int)AnimalPart.HindLegs, (int)AnimalPart.Antlers], Parts(deer));
        Assert.Equal([(int)AnimalPart.Body, (int)AnimalPart.LeftWing, (int)AnimalPart.RightWing], Parts(bird));
        Assert.True(bird.Positions.Max(p => p.X) > 0.4f && bird.Positions.Min(p => p.X) < -0.4f, "wings spread");
    }

    [Fact]
    public void ARoadCrossingWater_IsCarriedOverIt_OnABridgeFromBankToBank()
    {
        const int side = 129;
        var count = side * side;
        var terrain = new TerrainField(side, 2f, Enumerable.Range(0, count).Select(i => i % side * 2f is > 100f and < 124f ? -2f : 5f).ToArray(), new float[count], new float[count], Enumerable.Repeat(Biome.Grassland, count).ToArray());
        var road = new RoadState { Id = 1, FromSettlementId = 1, ToSettlementId = 2, Path = [new GroundPosition(60f, 128f), new GroundPosition(180f, 128f)], Length = 120f, Built = 120f };

        var span = Assert.Single(BridgeLayout.Spans(road, terrain));

        Assert.InRange(span.From.X, 92f, 101f);
        Assert.InRange(span.To.X, 122f, 132f);
        Assert.True(span.DeckAt(0.5f) >= 5.5f);
        Assert.Empty(BridgeLayout.Spans(road, new TerrainField(side, 2f, Enumerable.Repeat(5f, count).ToArray(), new float[count], new float[count], Enumerable.Repeat(Biome.Grassland, count).ToArray())));
        Assert.True(LaunchOptions.Parse(["--wildlife=on"]).FocusWildlife);
    }

    [Theory]
    [InlineData(5f, 5f)]
    [InlineData(5f, 11f)]
    [InlineData(14f, 3f)]
    public void ABridge_MeetsTheRoadOnBothBanks_HoweverTheirHeightsDiffer(float west, float east)
    {
        var (terrain, road) = Crossing(west, east, rise: 0f);

        var span = Assert.Single(BridgeLayout.Spans(road, terrain));
        var ribbon = RoadLayout.Ribbon(road, terrain);

        Assert.Equal(GroundOf(terrain, span.From) + RoadLayout.Lift, span.DeckAt(0f), 3);
        Assert.Equal(GroundOf(terrain, span.To) + RoadLayout.Lift, span.DeckAt(1f), 3);
        Assert.Contains(ribbon, section => Vector3.Distance(section.Centre, span.From) < 0.01f);
        Assert.Contains(ribbon, section => Vector3.Distance(section.Centre, span.To) < 0.01f);
    }

    [Fact]
    public void ABanksHighGroundBeyondTheWater_DoesNotLiftTheBridgeOffTheRoad()
    {
        // The land climbs steeply from the river on the far side: the old deck took that height at both ends.
        var (terrain, road) = Crossing(5f, 5f, rise: 3f);

        var span = Assert.Single(BridgeLayout.Spans(road, terrain));

        Assert.True(span.DeckAt(0f) - GroundOf(terrain, span.From) < 0.2f, "the near end rests on the bank");
        Assert.True(span.DeckAt(1f) - GroundOf(terrain, span.To) < 0.2f, "the far end rests on the bank");
    }

    [Fact]
    public void ABridgesDeck_ClearsTheWater_AndCarriesWalkers_ButOnlyOnIt()
    {
        var (terrain, road) = Crossing(1f, 1f, rise: 0f);
        var span = Assert.Single(BridgeLayout.Spans(road, terrain));

        Assert.All(Enumerable.Range(1, 9), i => Assert.True(span.DeckAt(i / 10f) > 0.5f, "the deck stands over the water"));
        Assert.True(span.DeckAt(0.5f) > span.DeckAt(0.05f), "the deck arches");
        Assert.Equal(span.DeckAt(0.5f), span.DeckOver(span.PointAt(0.5f))!.Value, 3);
        Assert.Null(span.DeckOver(span.PointAt(0.5f) + new Vector2(0f, BridgeSpan.HalfWidth + 1f)));
        Assert.Null(span.DeckOver(span.PointAt(-0.2f)));
    }

    [Fact]
    public void TwoRoadsOverOneCrossing_ShareABridge_ButTwoCrossingsDoNot()
    {
        var there = new BridgeSpan(new Vector3(100f, 1f, 100f), new Vector3(130f, 1f, 110f), 1f);
        var back = new BridgeSpan(new Vector3(131f, 1.3f, 111f), new Vector3(101f, 1.2f, 99f), 1f);
        var elsewhere = new BridgeSpan(new Vector3(100f, 1f, 160f), new Vector3(130f, 1f, 170f), 1f);

        Assert.True(there.SameCrossing(back));
        Assert.False(there.SameCrossing(elsewhere));
    }

    /// <summary>A strip of water at x 100..124 with a road over it, the banks at the given heights, the land beyond the far bank climbing by a rise per metre.</summary>
    private static (TerrainField Terrain, RoadState Road) Crossing(float west, float east, float rise)
    {
        const int side = 129;
        var count = side * side;
        float Height(float x) => x <= 100f ? west : x < 124f ? -2f : east + ((x - 124f) * rise);
        var terrain = new TerrainField(side, 2f, Enumerable.Range(0, count).Select(i => Height(i % side * 2f)).ToArray(), new float[count], new float[count], Enumerable.Repeat(Biome.Grassland, count).ToArray());
        var road = new RoadState { Id = 1, FromSettlementId = 1, ToSettlementId = 2, Path = [new GroundPosition(60f, 128f), new GroundPosition(180f, 128f)], Length = 120f, Built = 120f };
        return (terrain, road);
    }

    private static float GroundOf(TerrainField terrain, Vector3 point) => terrain.SampleHeight(point.X, point.Z);

    /// <summary>A generated world, and a village to look about.</summary>
    private static (GameSession Session, Vector2 At) World()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var village = session.World.Settlements[0];
        return (session, new Vector2(village.Position.X, village.Position.Z));
    }

    private static List<AnimalPose> Pose(GameSession session, Vector2 at, double seconds)
    {
        var poses = new List<AnimalPose>();
        Wildlife.Pose(session.World, session.Terrain, at, seconds, [], Settings, poses);
        return poses;
    }
}
