using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Divine;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Miracles;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Alerts, overlays, labels and readouts of gods and belief.</summary>
public sealed class DivinePresentationTests
{
    [Fact]
    public void Alerts_ConcernThePlayersGodAndFollowers()
    {
        var world = DivineWorlds.Create(0f, 200f, 600f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        var feed = new AlertFeed();

        var alerts = feed.Push(
            [
                new FamineStarted { Tick = 1, SettlementId = 1 },
                new FamineStarted { Tick = 1, SettlementId = 2 },
                new FollowingChanged { Tick = 1, SettlementId = 2, GodId = 1, Gained = true },
                new RankAchieved { Tick = 1, GodId = 1, Rank = DivineRank.LesserGod },
            ],
            world,
            localGodId: 1,
            simulationSeconds: 5.0);

        Assert.Equal(["Town1 is starving.", "Town2 now follows you.", "You are now a Lesser God."], alerts.Select(a => a.Text));
        Assert.Equal(world.Settlements[0].Position, alerts[0].Location);
        Assert.Equal(AlertSeverity.Warning, alerts[0].Severity);
        Assert.Equal(AlertSeverity.Good, alerts[1].Severity);
        Assert.Equal("You are now a Lesser God.", feed.Recent[0].Text);
    }

    [Fact]
    public void Alerts_AreNotRepeatedWithinTheRepeatTime()
    {
        var world = DivineWorlds.Create(0f, 200f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        var feed = new AlertFeed(repeatSeconds: 30.0);
        WorldEvent famine = new FamineStarted { Tick = 1, SettlementId = 1 };

        Assert.Single(feed.Push([famine], world, 1, 0.0));
        Assert.Empty(feed.Push([famine], world, 1, 20.0));
        Assert.Single(feed.Push([famine], world, 1, 31.0));
    }

    [Fact]
    public void Alerts_WhenFull_DropTheLeastUrgentOldest()
    {
        var world = DivineWorlds.Create(0f, 100f, 300f, 500f);
        var feed = new AlertFeed(repeatSeconds: 0.0, capacity: 2);

        feed.Push([new FollowingChanged { Tick = 1, SettlementId = 1, GodId = 1, Gained = false }], world, 1, 0.0);
        feed.Push([new FollowingChanged { Tick = 2, SettlementId = 2, GodId = 1, Gained = true }], world, 1, 1.0);
        feed.Push([new FollowingChanged { Tick = 3, SettlementId = 3, GodId = 1, Gained = true }], world, 1, 2.0);

        Assert.Equal(["Town3 now follows you.", "Town1 has turned away from you."], feed.Recent.Select(a => a.Text));
        feed.Clear();
        Assert.Empty(feed.Recent);
    }

    [Fact]
    public void InfluenceOverlay_IsTheGodsColourWithInfluenceAsOpacity()
    {
        var world = DivineWorlds.Create(0f, 224f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.4f, faith: 1f);
        var view = TestContent.DivineView;

        var image = OverlayMap.Build(world, OverlayKind.Influence, 1, DivineWorlds.Rules, view);

        Assert.Equal(DivineWorlds.RegionsPerSide, image.Width);
        Assert.Equal(DivineWorlds.RegionsPerSide * DivineWorlds.RegionsPerSide * 4, image.Rgba.Length);
        var home = world.Settlements[0].RegionId * 4;
        Assert.Equal(RgbColor.ToByte(view.GodColor(1).R), image.Rgba[home]);
        var influence = Pantheon.Simulation.Belief.InfluenceField.Build(DivineWorlds.Rules, world, 1).At(world.Regions.GetRegionCenter(world.Settlements[0].RegionId));
        Assert.Equal(RgbColor.ToByte(influence * view.OverlayOpacity), image.Rgba[home + 3]);
        Assert.Equal(0, image.Rgba[(DivineWorlds.RegionsPerSide - 1) * 4 + 3]);
        Assert.StartsWith("Influence overlay", image.Legend, StringComparison.Ordinal);
    }

    [Fact]
    public void FaithOverlay_ColoursEachSettlementsLandsByItsFaith()
    {
        var world = DivineWorlds.Create(0f, 224f, 800f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.4f, faith: 1f);

        var image = OverlayMap.Build(world, OverlayKind.Faith, 1, DivineWorlds.Rules, TestContent.DivineView);

        var devout = world.Settlements[0].RegionId * 4;
        var heathen = world.Settlements[1].RegionId * 4;
        Assert.True(image.Rgba[devout + 1] > image.Rgba[devout]);
        Assert.True(image.Rgba[heathen] > image.Rgba[heathen + 1]);
        Assert.Equal(0, image.Rgba[3]);
    }

    [Fact]
    public void Overlays_CycleThenTurnOff()
    {
        Assert.Equal(OverlayKind.Influence, OverlayMap.Next(null));
        Assert.Equal(OverlayKind.Religion, OverlayMap.Next(OverlayKind.Influence));
        Assert.Equal(OverlayKind.Faith, OverlayMap.Next(OverlayKind.Religion));
        Assert.Equal(OverlayKind.Food, OverlayMap.Next(OverlayKind.Faith));
        Assert.Null(OverlayMap.Next(OverlayKind.Food));
    }

    [Fact]
    public void Label_ShowsTheMainGodsShare()
    {
        var world = DivineWorlds.Create(0f, 224f);
        var settlement = world.Settlements[0];

        Assert.Equal("Town1\n100 people", SettlementLabel.Format(settlement, world));

        DivineWorlds.Believe(settlement, adherence: 0.42f, faith: 0.5f);
        Assert.Equal("Town1\n100 people · Testa 42%", SettlementLabel.Format(settlement, world));
        Assert.Equal(1, SettlementLabel.MainBelief(settlement)!.GodId);
    }

    [Fact]
    public void Picker_FindsTheNearestSettlementInRange()
    {
        var world = DivineWorlds.Create(0f, 200f, 500f);

        Assert.Equal(2, SettlementPicker.Nearest(world, new GroundPosition(420f, 512f), 200f)!.Id);
        Assert.Null(SettlementPicker.Nearest(world, new GroundPosition(900f, 512f), 200f));
    }

    [Fact]
    public void DebugPanel_ShowsGodsInfluenceAndTheSelectedSettlementsBelief()
    {
        var world = DivineWorlds.Create(250f, 200f, 600f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.8f, love: 0.5f, fear: 0.25f, doubt: 0.1f);
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            LocalGodId = 1,
            SelectedSettlementId = 1,
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "Terrain3D",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 300f,
            CursorWorldPosition = new Vector3(200f, 10f, 512f),
            RegionBoundariesVisible = false,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains("God Testa [you] (Human): Minor Spirit   power 250 (+4.32/s)   followers 50, Lesser God at 100", lines);
        Assert.Contains(lines, line => line.StartsWith("  influence Testa ", StringComparison.Ordinal));
        Assert.Contains("    Testa: 50 followers (50%)  faith 0.80  love 0.50  fear 0.25  doubt 0.10  worship 3.6 -> +4.32/s", lines);
        Assert.Contains("  Town2: 100 people, health 100%, food 400, no god", lines);
    }

    [Fact]
    public void RegionDebugMap_ShowsBlessingAndInfluence()
    {
        var world = DivineWorlds.Create(0f, 200f);
        world.Regions.Cells[0].Blessing = 1f;
        var influence = new float[world.Regions.Cells.Count];
        influence[1] = 1f;

        var blessing = RegionDebugMap.Build(world, RegionDebugLayer.Blessing, TestContent.Palette, null);
        var map = RegionDebugMap.Build(world, RegionDebugLayer.Influence, TestContent.Palette, null, influence);

        Assert.Equal(0xfd, blessing.Rgba[0]);
        Assert.Equal(0x44, blessing.Rgba[4]);
        Assert.Equal(0xfd, map.Rgba[4]);
        Assert.Equal(RegionDebugLayer.Influence, RegionDebugMap.Next(RegionDebugLayer.Blessing));
        Assert.Null(RegionDebugMap.Next(RegionDebugLayer.Influence));
    }

    [Theory]
    [InlineData(CastRejection.OutsideInfluence, "Outside your influence: 5% here, Food needs 10%")]
    [InlineData(CastRejection.NotEnoughPower, "Not enough Divine Power: Food costs 78 here, you have 12")]
    [InlineData(CastRejection.OutsideWorld, "There is nothing to cast on there")]
    [InlineData(CastRejection.NotAuthorized, "You may not cast Food for another god")]
    public void CastWording_ExplainsTheVerdict(CastRejection rejection, string expected)
    {
        var food = TestContent.Shipped.Miracles.Find("food")!;
        var check = new CastCheck(rejection, food, 0.05f, 78f, 0);

        Assert.Equal(expected, DivineText.DescribeCheck(check, "food", 12f, 10.0));
    }

    [Fact]
    public void RankNames_ReadNaturally()
    {
        Assert.Equal(["Minor Spirit", "Lesser God", "God", "Greater God", "High God", "Ascendant"], Enum.GetValues<DivineRank>().Select(DivineText.Rank));
    }
}
