using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Xunit;
using static Pantheon.Tests.Support.IslandSettlements;

namespace Pantheon.Tests.Game;

/// <summary>
/// A settlement's crowd shows representatives only near the camera, keeps them on the ground and
/// moving continuously as the settlement changes, and never touches the world.
/// </summary>
public sealed class SettlementCrowdTests
{
    private const float Near = 100f;
    private const float Tolerance = 0.001f;

    // Pinned cap and tier distances, so the counts below keep their meaning when settlement_view.json is retuned.
    private static readonly SettlementViewSettings Settings = View with
    {
        Citizens = View.Citizens with { MaxRepresentatives = 300, RegionalTierEnterDistance = 450f, RegionalTierExitDistance = 550f },
    };

    private static readonly CitizenSettings Citizens = Settings.Citizens;
    private static readonly TerrainField Terrain = Island.Terrain;
    private static readonly float BetweenTiers = (Citizens.RegionalTierEnterDistance + Citizens.RegionalTierExitDistance) / 2f;
    private static readonly float Far = Citizens.RegionalTierExitDistance + 250f;

    [Fact]
    public void FarFromTheCamera_NobodyIsShown()
    {
        var settlement = Create(population: 540, housing: 540);
        var crowd = NewCrowd();

        Show(crowd, settlement, 100.0, Far);

        Assert.Equal(SimulationTier.Statistical, crowd.Tier);
        Assert.Empty(crowd.Roster);
        Assert.Empty(crowd.Poses);
        Assert.Equal(new CrowdStatus(SettlementId, SimulationTier.Statistical, 0, 0f, Far, 450f, 550f), crowd.GetStatus(540, Far));
    }

    [Fact]
    public void NearTheCamera_EveryoneAppearsOnTheirRoutine_StandingOnTheGround()
    {
        var settlement = Create(population: 540, housing: 540);
        var crowd = NewCrowd();

        Show(crowd, settlement, 1234.5, Near);

        Assert.Equal(SimulationTier.Regional, crowd.Tier);
        Assert.Equal(ExpectedRoster(settlement), crowd.Roster);
        Assert.Equal(300, crowd.Roster.Count);
        AssertOnRoutines(crowd, 1234.5);
    }

    [Fact]
    public void Status_TellsHowManyPeopleEachRepresentativeStandsFor()
    {
        var settlement = Create(population: 540, housing: 540);
        var crowd = NewCrowd();

        Show(crowd, settlement, 0.0, Near);

        Assert.Equal(new CrowdStatus(SettlementId, SimulationTier.Regional, 300, 1.8f, Near, 450f, 550f), crowd.GetStatus(540, Near));
    }

    [Fact]
    public void LeavingTier2_RemovesEveryone_AndComingBackPlacesThemAfresh()
    {
        var settlement = Create(population: 300, housing: 300);
        var crowd = NewCrowd();

        Show(crowd, settlement, 10.0, Near);
        Show(crowd, settlement, 11.0, BetweenTiers);
        Assert.Equal(SimulationTier.Regional, crowd.Tier);
        Assert.Equal(300, crowd.Poses.Count);

        Show(crowd, settlement, 12.0, Far);
        Assert.Equal(SimulationTier.Statistical, crowd.Tier);
        Assert.Empty(crowd.Roster);
        Assert.Empty(crowd.Poses);

        Show(crowd, settlement, 400.0, BetweenTiers);
        Assert.Equal(SimulationTier.Statistical, crowd.Tier);
        Assert.Empty(crowd.Poses);

        Show(crowd, settlement, 400.0, Near);
        Assert.Equal(SimulationTier.Regional, crowd.Tier);
        AssertOnRoutines(crowd, 400.0);
    }

    [Fact]
    public void Newcomer_StepsOutOfItsDoor()
    {
        var crowd = NewCrowd();
        Show(crowd, Create(population: 120, housing: 150), 500.0, Near);

        Show(crowd, Create(population: 121, housing: 150), 500.0, Near);
        var newcomer = crowd.Roster[120];
        Assert.Equal(121, crowd.Roster.Count);
        Assert.Equal(newcomer.Home, Ground(crowd.Poses[120].Position));

        Show(crowd, Create(population: 121, housing: 150), 501.0, Near);
        Assert.InRange(Vector2.Distance(newcomer.Home, Ground(crowd.Poses[120].Position)), 0f, Citizens.CatchUpSpeed + Tolerance);
    }

    [Fact]
    public void RosterChanges_NeverMakeAnyoneJump()
    {
        var crowd = NewCrowd();
        var changes = new[]
        {
            Create(population: 121, housing: 150),
            Create(population: 121, housing: 155),
            Create(population: 121, housing: 155, constructionProgress: 10f, food: 5000f),
            Create(population: 119, housing: 155, constructionProgress: 10f, food: 5000f),
        };
        Show(crowd, Create(population: 120, housing: 150), 300.0, Near);

        foreach (var changed in changes)
        {
            var before = Grounds(crowd);
            var version = crowd.RosterVersion;

            Show(crowd, changed, 300.0, Near);

            Assert.NotEqual(version, crowd.RosterVersion);
            var after = Grounds(crowd);
            Assert.Equal(before.Take(after.Length), after.Take(before.Length));
        }
    }

    [Fact]
    public void Rerouted_HurryToTheirNewRoutine_NoFasterThanTheCatchUpSpeed_AndOnTheGround()
    {
        // A thirteenth house draws some residents to it: they walk there instead of jumping.
        const double Step = 0.25;
        var thirteenHouses = Create(population: 300, housing: 65);
        var crowd = NewCrowd();
        Show(crowd, Create(population: 300, housing: 60), 0.0, Near);
        Show(crowd, thirteenHouses, 0.0, Near);
        Assert.Contains(Enumerable.Range(0, crowd.Roster.Count), i => !IsOnRoutine(crowd, i, 0.0));

        var previous = Grounds(crowd);
        for (var seconds = Step; seconds <= 120.0; seconds += Step)
        {
            Show(crowd, thirteenHouses, seconds, Near);

            var grounds = Grounds(crowd);
            for (var i = 0; i < grounds.Length; i++)
            {
                Assert.InRange(Vector2.Distance(previous[i], grounds[i]), 0f, (Citizens.CatchUpSpeed * (float)Step) + Tolerance);
            }

            Assert.All(crowd.Poses, pose => Assert.InRange(pose.Position.Y - Terrain.SampleHeight(pose.Position.X, pose.Position.Z), -Tolerance, Citizens.StepBounce + Tolerance));
            previous = grounds;
        }

        AssertOnRoutines(crowd, 120.0);
    }

    [Fact]
    public void Paused_TheCrowdStandsStill()
    {
        var thirteenHouses = Create(population: 300, housing: 65);
        var crowd = NewCrowd();
        Show(crowd, Create(population: 300, housing: 60), 50.0, Near);
        Show(crowd, thirteenHouses, 51.0, Near);
        var before = Grounds(crowd);

        Show(crowd, thirteenHouses, 51.0, Near);

        Assert.Equal(before, Grounds(crowd));
    }

    [Fact]
    public void RosterVersion_ChangesOnlyWhenTheRosterIsRebuiltOrCleared()
    {
        var crowd = NewCrowd();
        Show(crowd, Building(300), 0.0, Near);
        var version = crowd.RosterVersion;

        // Above the cap one more inhabitant leaves every role count, and so the roster, as it was.
        Show(crowd, Building(300), 1.0, Near);
        Show(crowd, Building(301), 2.0, Near);
        Assert.Equal(version, crowd.RosterVersion);

        Show(crowd, Building(400), 3.0, Near);
        Assert.Equal(version + 1, crowd.RosterVersion);

        Show(crowd, Building(400), 4.0, Far);
        Assert.Equal(version + 2, crowd.RosterVersion);
    }

    [Fact]
    public void Crowd_NeverChangesTheWorld()
    {
        var world = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(Island.State));
        var settlement = world.Settlements[0];
        settlement.Population = 300;
        settlement.Housing = 300;
        settlement.ConstructionProgress = 50f;
        settlement.Food = 5000f;
        var saved = WorldSaveSerializer.Serialize(world);
        var crowd = new SettlementCrowd(world.Seed, settlement.Id, Settings);

        for (var frame = 0; frame < 20; frame++)
        {
            Show(crowd, settlement, frame * 0.5, frame < 15 ? Near : Far);
        }

        Assert.Equal(saved, WorldSaveSerializer.Serialize(world));
    }

    /// <summary>A crowd for the island's settlement with the pinned settings.</summary>
    private static SettlementCrowd NewCrowd() => new(Seed, SettlementId, Settings);

    /// <summary>A crowded settlement with a house under way and stores to fund it, so ten builders work.</summary>
    private static SettlementState Building(int population) => Create(population, housing: 300, constructionProgress: 50f, food: 5000f);

    /// <summary>One client frame, as <c>SettlementView.UpdateCitizens</c> runs it.</summary>
    private static void Show(SettlementCrowd crowd, SettlementState settlement, double seconds, float cameraDistance)
    {
        crowd.SelectTier(cameraDistance);
        crowd.Refresh(settlement, Labour(settlement), Site(settlement));
        crowd.Advance(seconds, Terrain);
    }

    /// <summary>The roster the crowd should show for a settlement.</summary>
    private static IReadOnlyList<RepresentativeCitizen> ExpectedRoster(SettlementState settlement) =>
        RepresentativeRoster.Build(Seed, settlement, Labour(settlement), Site(settlement), Settings);

    /// <summary>Ground points of the crowd's figures.</summary>
    private static Vector2[] Grounds(SettlementCrowd crowd) => crowd.Poses.Select(pose => Ground(pose.Position)).ToArray();

    /// <summary>Whether a figure stands where its routine puts it.</summary>
    private static bool IsOnRoutine(SettlementCrowd crowd, int index, double seconds) =>
        Vector2.Distance(CitizenRoutine.Evaluate(crowd.Roster[index], seconds, Citizens).Ground, Ground(crowd.Poses[index].Position)) <= Tolerance;

    /// <summary>Asserts that every figure is posed exactly as its routine says, standing on the terrain.</summary>
    private static void AssertOnRoutines(SettlementCrowd crowd, double seconds)
    {
        Assert.Equal(crowd.Roster.Count, crowd.Poses.Count);
        for (var i = 0; i < crowd.Roster.Count; i++)
        {
            var routine = CitizenRoutine.Evaluate(crowd.Roster[i], seconds, Citizens);
            var pose = crowd.Poses[i];
            Assert.Equal(routine.Activity, pose.Activity);
            Assert.Equal(routine.Ground.X, pose.Position.X, 3);
            Assert.Equal(routine.Ground.Y, pose.Position.Z, 3);
            Assert.Equal(Terrain.SampleHeight(pose.Position.X, pose.Position.Z) + routine.Bounce, pose.Position.Y, 3);
            Assert.Equal(routine.Yaw, pose.Yaw, 3);
            Assert.Equal(routine.Lean, pose.Lean, 3);
        }
    }
}
