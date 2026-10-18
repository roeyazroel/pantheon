using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;
using Xunit;
using static Pantheon.Tests.Support.IslandSettlements;

namespace Pantheon.Tests.Game;

/// <summary>Representative citizens follow the aggregates, keep their identities as the settlement changes and stand where their role puts them.</summary>
public sealed class RepresentativeRosterTests
{
    private const float Tolerance = 0.001f;
    private static readonly CitizenSettings Citizens = View.Citizens;

    [Theory]
    [InlineData(0, 300, 0)]
    [InlineData(1, 300, 1)]
    [InlineData(299, 300, 299)]
    [InlineData(300, 300, 300)]
    [InlineData(5000, 300, 300)]
    [InlineData(-3, 300, 0)]
    [InlineData(50, 0, 0)]
    public void ComputeCount_IsOnePerInhabitant_UpToTheCap(int population, int maxRepresentatives, int expected)
    {
        Assert.Equal(expected, RepresentativeRoster.ComputeCount(population, maxRepresentatives));
    }

    [Theory]
    [InlineData(300, 600, 360f, 10f, 5, 175, 120)]
    [InlineData(58, 58, 34.8f, 6.96f, 7, 28, 23)]
    [InlineData(3, 6, 3f, 1f, 1, 1, 1)]
    [InlineData(0, 600, 360f, 10f, 0, 0, 0)]
    [InlineData(10, 0, 0f, 0f, 0, 0, 0)]
    public void SplitRoles_RoundsEachRoleBoundary(int count, int population, float workers, float builders, int expectedBuilders, int expectedFarmers, int expectedVillagers)
    {
        var roles = RepresentativeRoster.SplitRoles(count, population, new LabourAllocation(workers, workers - builders, builders));

        Assert.Equal(new RoleCounts(expectedBuilders, expectedFarmers, expectedVillagers), roles);
    }

    [Fact]
    public void SplitRoles_KeepsEveryRoleWithinOneOfItsShare()
    {
        for (var population = 1; population <= 1200; population++)
        {
            var labour = Labour(Create(population, housing: population, constructionProgress: 50f, food: 1_000_000f));
            var count = RepresentativeRoster.ComputeCount(population, Citizens.MaxRepresentatives);
            var perPerson = (double)count / population;

            var roles = RepresentativeRoster.SplitRoles(count, population, labour);

            Assert.Equal(count, roles.Total);
            Assert.InRange(roles.Builders - (labour.Builders * perPerson), -1.0, 1.0);
            Assert.InRange(roles.Farmers - (labour.Farmers * perPerson), -1.0, 1.0);
            Assert.InRange(roles.Villagers - (labour.NotWorking(population) * perPerson), -1.0, 1.0);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(58)]
    [InlineData(300)]
    [InlineData(541)]
    public void Build_GivesEachRoleItsCount(int population)
    {
        var settlement = Create(population, housing: population, constructionProgress: 50f, food: 1_000_000f);
        var count = RepresentativeRoster.ComputeCount(population, Citizens.MaxRepresentatives);
        var roles = RepresentativeRoster.SplitRoles(count, population, Labour(settlement));

        var roster = Roster(settlement);

        Assert.Equal(Enumerable.Range(0, count), roster.Select(citizen => citizen.Index));
        Assert.Equal(roles.Builders, roster.Count(citizen => citizen.Role == CitizenRole.Builder));
        Assert.Equal(roles.Farmers, roster.Count(citizen => citizen.Role == CitizenRole.Farmer));
        Assert.Equal(roles.Villagers, roster.Count(citizen => citizen.Role == CitizenRole.Villager));
    }

    [Fact]
    public void Build_IsDeterministic_AndKeyedByTheWorldSeed()
    {
        var settlement = Create(population: 420, housing: 400, constructionProgress: 30f, food: 5000f);
        var site = Site(settlement);
        var labour = Labour(settlement);

        var first = RepresentativeRoster.Build(Seed, settlement, labour, site, View);
        var second = RepresentativeRoster.Build(Seed, settlement, labour, site, View);
        var otherSeed = RepresentativeRoster.Build(new WorldSeed(99), settlement, labour, site, View);

        Assert.Equal(first, second);
        Assert.NotEqual(first.Select(citizen => citizen.Home), otherSeed.Select(citizen => citizen.Home));
    }

    [Fact]
    public void OneMoreInhabitant_ChangesAtMostTwoRoles_AndNobodysHome()
    {
        // Housing stays put, so only the count and the role boundaries move; representatives whose
        // role stays the same must not change at all.
        var previous = CrowdedRoster(population: 20);
        for (var population = 21; population <= 320; population++)
        {
            var next = CrowdedRoster(population);
            var changedRoles = 0;
            for (var i = 0; i < Math.Min(previous.Count, next.Count); i++)
            {
                if (next[i].Role == previous[i].Role)
                {
                    Assert.Equal(previous[i], next[i]);
                    continue;
                }

                changedRoles++;
                Assert.Equal(previous[i].Home, next[i].Home);
            }

            Assert.InRange(changedRoles, 0, 2);
            previous = next;
        }

        static IReadOnlyList<RepresentativeCitizen> CrowdedRoster(int population) =>
            Roster(Create(population, housing: 20, constructionProgress: 50f, food: 5000f));
    }

    [Theory]
    [InlineData(60f, 5f, 60f, 6f, CitizenRole.Farmer, CitizenRole.Builder)]
    [InlineData(60f, 5f, 61f, 5f, CitizenRole.Villager, CitizenRole.Farmer)]
    public void OneMoreBuilderOrFarmer_ChangesExactlyOneRole(float workers, float builders, float moreWorkers, float moreBuilders, CitizenRole from, CitizenRole to)
    {
        var settlement = Create(population: 100, housing: 100, constructionProgress: 50f, food: 5000f);
        var site = Site(settlement);

        var before = RepresentativeRoster.Build(Seed, settlement, new LabourAllocation(workers, workers - builders, builders), site, View);
        var after = RepresentativeRoster.Build(Seed, settlement, new LabourAllocation(moreWorkers, moreWorkers - moreBuilders, moreBuilders), site, View);

        var changed = Assert.Single(Enumerable.Range(0, before.Count), i => before[i].Role != after[i].Role);
        Assert.Equal(from, before[changed].Role);
        Assert.Equal(to, after[changed].Role);
    }

    [Fact]
    public void NewHouse_OnlyDrawsResidentsIntoItself()
    {
        // Without stores no builders are taken out of the fields, so only the layout changes.
        var before = Roster(Create(population: 300, housing: 60));
        var after = Roster(Create(population: 300, housing: 65));
        const int NewHouse = 12;

        var movedIn = 0;
        for (var i = 0; i < before.Count; i++)
        {
            Assert.InRange(before[i].House, 0, NewHouse - 1);
            if (after[i].House == before[i].House)
            {
                Assert.Equal(before[i].Home, after[i].Home);
                continue;
            }

            Assert.Equal(NewHouse, after[i].House);
            movedIn++;
        }

        // Rendezvous hashing sends about one in thirteen representatives to the thirteenth house.
        Assert.InRange(movedIn, 1, 2 * before.Count / 13);
    }

    [Fact]
    public void Everyone_RestsByTheirDoor_AndGathersAroundTheShrine()
    {
        var settlement = Building();
        var site = Site(settlement);
        var center = Ground(site.Center);

        Assert.All(Roster(settlement), citizen =>
        {
            var door = site.DoorOf(citizen.House, View.Layout.DoorDistance);
            Assert.InRange(Vector2.Distance(door, citizen.Home), 0f, (Citizens.DoorSpread * MathF.Sqrt(2f)) + Tolerance);
            Assert.InRange(Vector2.Distance(center, citizen.Gathering), Citizens.GatherRadiusMin - Tolerance, Citizens.GatherRadiusMax + Tolerance);
            AssertFaces(citizen.Gathering, center, citizen.GatherYaw);
            Assert.InRange(citizen.Phase, 0f, 0.99999994f);
        });
    }

    [Fact]
    public void Builders_WorkAroundTheBuildingSite_FacingIt()
    {
        var settlement = Building();
        var construction = Assert.NotNull(Site(settlement).Construction).House;
        var house = Ground(construction.Position);

        var builders = Roster(settlement).Where(citizen => citizen.Role == CitizenRole.Builder).ToList();

        Assert.Equal(10, builders.Count);
        Assert.All(builders, builder =>
        {
            Assert.Equal(Citizens.BuilderRingRadius * construction.Scale, Vector2.Distance(house, builder.Work), 3);
            AssertFaces(builder.Work, house, builder.WorkYaw);
        });
    }

    [Fact]
    public void Farmers_WorkInsideAField_AlongItsRows()
    {
        var settlement = Building();
        var fields = Site(settlement).Fields;
        var half = Citizens.WorkSpread * View.Layout.FieldSize * 0.5f;

        var farmers = Roster(settlement).Where(citizen => citizen.Role == CitizenRole.Farmer).ToList();

        Assert.NotEmpty(fields);
        Assert.NotEmpty(farmers);
        Assert.All(farmers, farmer =>
        {
            var field = Assert.Single(fields, field => IsInside(field, farmer.Work, half));
            var along = Vector2.Dot(SettlementSite.Facing(field.Yaw), SettlementSite.Facing(farmer.WorkYaw));
            Assert.Equal(1f, MathF.Abs(along), 3);
        });
    }

    [Fact]
    public void Villagers_DoChoresNearHome()
    {
        var villagers = Roster(Building()).Where(citizen => citizen.Role == CitizenRole.Villager).ToList();

        Assert.NotEmpty(villagers);
        Assert.All(villagers, villager => Assert.InRange(Vector2.Distance(villager.Home, villager.Work), 0f, Citizens.WanderRadius + Tolerance));
    }

    [Fact]
    public void WithoutHousesFieldsOrBuildingSite_EveryoneLivesAroundTheShrine_AndDoesChores()
    {
        // Twelve people, no housing yet and stores for builders: the builders have no site to work on.
        var settlement = Create(population: 12, housing: 0, food: 100f);
        var site = Site(settlement);
        var center = Ground(site.Center);

        var roster = Roster(settlement);

        Assert.Empty(site.Houses);
        Assert.Empty(site.Fields);
        Assert.Null(site.Construction);
        Assert.Contains(roster, citizen => citizen.Role == CitizenRole.Builder);
        Assert.Contains(roster, citizen => citizen.Role == CitizenRole.Farmer);
        Assert.All(roster, citizen =>
        {
            Assert.Equal(-1, citizen.House);
            Assert.Equal(Citizens.GatherRadiusMax + Citizens.DoorSpread, Vector2.Distance(center, citizen.Home), 3);
            Assert.InRange(Vector2.Distance(citizen.Home, citizen.Work), 0f, Citizens.WanderRadius + Tolerance);
        });
    }

    /// <summary>A crowded settlement of 300 with a house under way and stores to fund it: ten builders, sixty houses, twenty fields.</summary>
    private static SettlementState Building() => Create(population: 300, housing: 300, constructionProgress: 50f, food: 5000f);

    /// <summary>Whether a ground point lies within <paramref name="half"/> of a field's centre along both of its axes.</summary>
    private static bool IsInside(FieldPlacement field, Vector2 point, float half)
    {
        var forward = SettlementSite.Facing(field.Yaw);
        var right = new Vector2(forward.Y, -forward.X);
        var offset = point - Ground(field.Position);
        return MathF.Abs(Vector2.Dot(offset, forward)) <= half + Tolerance && MathF.Abs(Vector2.Dot(offset, right)) <= half + Tolerance;
    }

    /// <summary>Asserts that a figure at <paramref name="from"/> turned to <paramref name="yaw"/> faces <paramref name="target"/>.</summary>
    private static void AssertFaces(Vector2 from, Vector2 target, float yaw)
    {
        var facing = SettlementSite.Facing(yaw);
        var towards = Vector2.Normalize(target - from);
        Assert.Equal(towards.X, facing.X, 3);
        Assert.Equal(towards.Y, facing.Y, 3);
    }
}
