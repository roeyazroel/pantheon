using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Game.Presentation;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Houses, the building site and fields follow the settlement's state and stand on sensible ground.</summary>
public sealed class SettlementLayoutTests
{
    private static readonly SettlementRules Rules = IslandSettlements.Rules;
    private static readonly SettlementLayoutSettings Layout = TestContent.SettlementView.Layout;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(60, 12)]
    [InlineData(61, 13)]
    public void HouseCount_FollowsHousing(int housing, int houses)
    {
        Assert.Equal(houses, SettlementLayout.ComputeHouseCount(Settlement(housing), Rules));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(1000, 7)]
    public void FieldCount_IsOnePerHousesPerField_UpToTheMaximum(int houses, int fields)
    {
        var layout = Layout with { HousesPerField = 3, MaxFields = 7 };

        Assert.Equal(fields, SettlementLayout.ComputeFieldCount(houses, layout));
    }

    [Fact]
    public void Houses_AreAStablePrefixAsHousingGrows()
    {
        var small = Site(Settlement(housing: 60));
        var large = Site(Settlement(housing: 200));

        Assert.Equal(12, small.Houses.Count);
        Assert.Equal(40, large.Houses.Count);
        Assert.Equal(small.Houses, large.Houses.Take(12));
    }

    [Fact]
    public void BuildingSite_TakesTheNextHouseSlotWithItsProgress()
    {
        var building = Site(Settlement(housing: 60, constructionProgress: 45f));
        var built = Site(Settlement(housing: 65));

        Assert.Equal(12, building.Houses.Count);
        var construction = Assert.NotNull(building.Construction);
        Assert.Equal(built.Houses[12], construction.House);
        Assert.Equal(0.45f, construction.Progress, 5);
        Assert.Null(Site(Settlement(housing: 60)).Construction);
    }

    [Fact]
    public void Houses_StandOnDryLandAroundTheShrine_WithDoorsTowardsIt()
    {
        var site = Site(Settlement(housing: 300));
        var center = new Vector2(site.Center.X, site.Center.Z);

        Assert.Equal(60, site.Houses.Count);
        for (var i = 0; i < site.Houses.Count; i++)
        {
            var house = site.Houses[i];
            var ground = new Vector2(house.Position.X, house.Position.Z);
            var door = site.DoorOf(i, Layout.DoorDistance);
            Assert.True(house.Position.Y >= Layout.MinimumGroundHeight);
            Assert.InRange(Vector2.Distance(center, ground), Layout.ShrineClearance, site.HouseDiscRadius + 0.001f);
            Assert.Equal(Layout.DoorDistance, Vector2.Distance(ground, door), 3);
            Assert.True(Vector2.Distance(center, door) < Vector2.Distance(center, ground));
        }
    }

    [Fact]
    public void Fields_LieBeyondTheHousesOnDryGentleLand()
    {
        var site = Site(Settlement(housing: 200));
        var center = new Vector2(site.Center.X, site.Center.Z);
        var innerRadius = site.HouseDiscRadius + Layout.FieldClearance + (Layout.FieldSize * 0.7f);
        var steepestNormalY = 1f / MathF.Sqrt(1f + (Layout.FieldMaxSlope * Layout.FieldMaxSlope));

        Assert.Equal(SettlementLayout.ComputeFieldCount(40, Layout), site.Fields.Count);
        Assert.All(site.Fields, field =>
        {
            Assert.True(Vector2.Distance(center, new Vector2(field.Position.X, field.Position.Z)) >= innerRadius);
            Assert.True(field.Position.Y >= Layout.MinimumGroundHeight);
            Assert.Equal(1f, field.Normal.Length(), 4);
            Assert.True(field.Normal.Y >= steepestNormalY - 0.0001f);
            Assert.InRange(field.Variant, 0f, 1f);
        });
    }

    [Fact]
    public void Fields_MoveOutwardAsTheVillageGrowsOverThem()
    {
        var village = Site(Settlement(housing: 60));
        var town = Site(Settlement(housing: 600));

        Assert.True(town.HouseDiscRadius > village.HouseDiscRadius);
        Assert.True(NearestField(town) > NearestField(village));
        Assert.True(town.Fields.Count > village.Fields.Count);
    }

    [Fact]
    public void ComputeSite_IsDeterministic()
    {
        var settlement = Settlement(housing: 120, constructionProgress: 70f);

        var first = Site(settlement);
        var second = Site(settlement);

        Assert.Equal(first.Center, second.Center);
        Assert.Equal(first.Houses, second.Houses);
        Assert.Equal(first.Construction, second.Construction);
        Assert.Equal(first.Fields, second.Fields);
        Assert.Equal(first.HouseDiscRadius, second.HouseDiscRadius);
    }

    /// <summary>The island's starting settlement with the given housing and construction progress; the layout ignores the population.</summary>
    private static SettlementState Settlement(int housing, float constructionProgress = 0f) =>
        IslandSettlements.Create(population: 30, housing, constructionProgress);

    /// <summary>Lays out a settlement on the island with the pinned rules and the shipped layout.</summary>
    private static SettlementSite Site(SettlementState settlement) => IslandSettlements.Site(settlement);

    /// <summary>Distance from the shrine to the nearest field centre.</summary>
    private static float NearestField(SettlementSite site) =>
        site.Fields.Min(field => Vector2.Distance(new Vector2(site.Center.X, site.Center.Z), new Vector2(field.Position.X, field.Position.Z)));
}
