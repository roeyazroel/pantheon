using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Population;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>People walk home at the end of the day and come out in the morning (ADR-080).</summary>
public sealed class CitizenEveningTests
{
    private static readonly DayNightSettings Day = TestContent.DayNight;

    [Theory]
    [InlineData(12f, 0f, EveningPhase.Abroad)]
    [InlineData(12f, 1f, EveningPhase.Abroad)]
    [InlineData(18f, 0f, EveningPhase.Homeward)]
    [InlineData(17.5f, 1f, EveningPhase.Abroad)]
    [InlineData(21f, 1f, EveningPhase.Homeward)]
    [InlineData(23f, 0f, EveningPhase.Indoors)]
    [InlineData(23f, 1f, EveningPhase.Indoors)]
    [InlineData(3f, 0.5f, EveningPhase.Indoors)]
    [InlineData(6f, 0f, EveningPhase.Abroad)]
    [InlineData(6f, 1f, EveningPhase.Indoors)]
    public void EachPerson_KeepsTheirOwnHours(float hour, float share, EveningPhase phase)
    {
        Assert.Equal(phase, CitizenEvening.Of(Day, hour, share));
    }

    [Fact]
    public void AtDeadOfNight_NobodyIsAbroad_WhoeverTheyAre()
    {
        Assert.All(Enumerable.Range(0, 100), i => Assert.Equal(EveningPhase.Indoors, CitizenEvening.Of(Day, 2f, i / 100f)));
    }

    [Theory]
    [InlineData(0f, 0.1f, 1f)]
    [InlineData(0.1f, 0f, 1f)]
    public void WhenTheGameIsPausedOrTheHourHeld_NobodyHurries(float hours, float seconds, float hurry)
    {
        Assert.Equal(hurry, CitizenEvening.Hurry(Day, hours, seconds));
    }

    [Fact]
    public void WhenTheGameRunsFast_PeopleHurry_ButNeverPastTheLimit()
    {
        var usual = DayNight.Hours / Day.DaySeconds;

        Assert.Equal(1f, CitizenEvening.Hurry(Day, usual * 0.1f, 0.1f), 3);
        Assert.Equal(4f, CitizenEvening.Hurry(Day, usual * 0.4f, 0.1f), 3);
        Assert.Equal(Day.MostHurry, CitizenEvening.Hurry(Day, usual * 10f, 0.1f), 3);
    }

    [Fact]
    public void ThroughTheEvening_PeopleWalkHomeAlongTheGround_AndGoInAtTheirOwnDoors()
    {
        var (crowd, settlement, site) = Crowd();
        var seconds = 100.0;
        Show(crowd, settlement, site, seconds, 16f);
        var abroad = crowd.Poses.Count(pose => !pose.Indoors);
        Assert.Equal(crowd.Poses.Count, abroad);

        // From 17:00 to 22:00 at a day's usual pace, frame by frame.
        var hour = 16f;
        var last = crowd.Poses.Select(pose => Flat(pose.Position)).ToArray();
        while (hour < 21.99f)
        {
            (hour, seconds) = (hour + 0.01f, seconds + (0.01 * Day.DaySeconds / 24.0));
            Show(crowd, settlement, site, seconds, hour);
            for (var i = 0; i < crowd.Poses.Count; i++)
            {
                var now = Flat(crowd.Poses[i].Position);
                Assert.True(Vector2.Distance(last[i], now) < 3.5f, "nobody jumps across the village");
                last[i] = now;
            }
        }

        Assert.All(crowd.Poses, pose => Assert.True(pose.Indoors));
        Assert.All(Enumerable.Range(0, crowd.Roster.Count).Where(i => crowd.Roster[i].House >= 0), i =>
            Assert.Equal(0f, Vector2.Distance(site.DoorOf(crowd.Roster[i].House, Day.DoorReach), Flat(crowd.Poses[i].Position)), 2));
    }

    [Fact]
    public void AVillageFirstSeenAtNight_IsAlreadyAsleep()
    {
        var (crowd, settlement, site) = Crowd();

        Show(crowd, settlement, site, 100.0, 2f);

        Assert.All(crowd.Poses, pose => Assert.True(pose.Indoors));
        Assert.Equal(crowd.Poses.Count, crowd.GetStatus(120, 100f).Indoors);
    }

    [Fact]
    public void InTheMorning_PeopleComeOutOfTheirOwnDoors_AndTakeUpTheirDay()
    {
        var (crowd, settlement, site) = Crowd();
        Show(crowd, settlement, site, 100.0, 3f);
        var seconds = 101.0;
        Show(crowd, settlement, site, seconds, 9f);

        Assert.All(crowd.Poses, pose => Assert.False(pose.Indoors));
        Assert.All(Enumerable.Range(0, crowd.Roster.Count).Where(i => crowd.Roster[i].House >= 0), i =>
            Assert.True(Vector2.Distance(site.DoorOf(crowd.Roster[i].House, Day.DoorReach), Flat(crowd.Poses[i].Position)) < 3.2f, "they set out from the door"));
    }

    [Fact]
    public void WithoutTheHour_PeopleKeepToTheirRoutine()
    {
        var (crowd, settlement, site) = Crowd();
        crowd.SelectTier(100f);
        crowd.Refresh(settlement, SettlementEconomy.AllocateLabour(IslandSettlements.Rules, settlement), site);

        crowd.Advance(100.0, IslandSettlements.Island.Terrain);

        Assert.All(crowd.Poses, pose => Assert.False(pose.Indoors));
    }

    private static (SettlementCrowd Crowd, SettlementState Settlement, SettlementSite Site) Crowd()
    {
        var settlement = IslandSettlements.Create(population: 120, housing: 120, constructionProgress: 0f);
        return (new SettlementCrowd(new WorldSeed(1234), settlement.Id, TestContent.SettlementView, Day), settlement, IslandSettlements.Site(settlement));
    }

    private static void Show(SettlementCrowd crowd, SettlementState settlement, SettlementSite site, double seconds, float hour)
    {
        crowd.SelectTier(100f);
        crowd.Refresh(settlement, SettlementEconomy.AllocateLabour(IslandSettlements.Rules, settlement), site);
        crowd.Advance(seconds, IslandSettlements.Island.Terrain, null, hour);
    }

    private static Vector2 Flat(Vector3 point) => new(point.X, point.Z);
}
