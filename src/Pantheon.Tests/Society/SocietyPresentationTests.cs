using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Game.Society;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Society;

/// <summary>How society is shown and driven (ADR-023): the Settlements panel, commands, alerts, history, the temple and walls, and launch options.</summary>
public sealed class SocietyPresentationTests
{
    [Fact]
    public void ThePanel_ShowsASettlementsSociety_AndLetsItsGodSetItsCourse()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var home = session.World.Settlements.First(s => s.Beliefs.Any(b => b.GodId == session.LocalGodId && b.Adherence >= 0.5f));

        var model = SettlementPanelReadout.Build(session, home.Id)!;
        var status = SocietyCommands.SetPriority(session, home.Id, SettlementPriority.Temple);
        var after = SettlementPanelReadout.Build(session, home.Id)!;

        Assert.StartsWith(home.Name + ":", model.Title, StringComparison.Ordinal);
        Assert.Contains(model.Lines, line => line.StartsWith("Follows: ", StringComparison.Ordinal));
        Assert.Contains(model.Lines, line => line.StartsWith("Knows: agriculture 0.0", StringComparison.Ordinal));
        Assert.All(model.Options.Where(o => o.Priority != SettlementPriority.None), o => Assert.True(o.Enabled));
        Assert.Equal(home.Name + " will build a temple", status);
        Assert.Equal("Puts first: Build a temple (asked by Ilyra).", after.Priority);
        Assert.Contains(after.Lines, line => line.StartsWith("Saving for temple", StringComparison.Ordinal));
        Assert.All(after.Options, o => Assert.False(o.Enabled));
        Assert.Contains("lately", after.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Alerts_TellOfTemplesFoundingsAndARivalSettingTheCourseOfYourVillage()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", Domain.Gods.GodController.Ai, Domain.Gods.GodPersonality.Tyrant, 0f));
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        WorldEvent[] events =
        [
            new PriorityChanged { Tick = 1, SettlementId = 1, GodId = 2, Priority = SettlementPriority.Defence },
            new BuildingCompleted { Tick = 1, SettlementId = 1, Kind = BuildingKind.Temple, GodId = DivineWorlds.GodId },
            new BuildingCompleted { Tick = 1, SettlementId = 1, Kind = BuildingKind.Levee, GodId = null },
            new SettlementFounded { Tick = 1, SettlementId = 2, FromSettlementId = 1, Settlers = 20 },
            new TechnologyAdvanced { Tick = 1, SettlementId = 1, Field = TechnologyField.Medicine, Level = 2 },
            new TechnologyAdvanced { Tick = 1, SettlementId = 1, Field = TechnologyField.Science, Level = 1 },
        ];

        var raised = new AlertFeed().Push(events, world, DivineWorlds.GodId, 10.0);

        Assert.Equal(
            ["Rivalis asks Town1 to prepare for war.", "Town1 raises a temple to you.", "Town1 has built levees.", "Settlers from Town1 found Town2.", "Town1 masters medicine (level 2)."],
            raised.Select(a => a.Text));
    }

    [Fact]
    public void Foundings_AndTemples_EnterTheHistory()
    {
        var world = DivineWorlds.Create(0f, 300f, 900f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.7f, faith: 0.5f);

        new HistoryRecorder(TestContent.Shipped.History).Record(world, [new SettlementFounded { Tick = 0, SettlementId = 2, FromSettlementId = 1, Settlers = 20 }, new BuildingCompleted { Tick = 0, SettlementId = 1, Kind = BuildingKind.Temple, GodId = 1 }], 0);

        Assert.Equal([HistoryKind.Founding, HistoryKind.Temple], world.History.Select(r => r.Kind));
        Assert.Equal("The Founding of Town2", HistoryText.Title(world.History[0], world));
        Assert.Equal("20 settlers", HistoryText.Detail(world.History[0], world));
        Assert.Equal("Town1 raises a temple to Testa", HistoryText.Title(world.History[1], world));
    }

    [Fact]
    public void TheTemple_StandsInsideTheHousesRing_AndTheWallsBeyondItWithGates()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var village = session.World.Settlements[0];
        var site = SettlementLayout.ComputeSite(village, session.Content.SettlementRules, session.Terrain, TestContent.SettlementView.Layout);

        var (temple, _) = CivicLayout.Temple(site, session.Terrain, village.Id);
        var walls = CivicLayout.Walls(site, session.Terrain);

        var fromShrine = new System.Numerics.Vector2(temple.X - site.Center.X, temple.Z - site.Center.Z).Length();
        Assert.Equal(CivicLayout.TempleDistance, fromShrine, 2);
        Assert.NotEmpty(walls);
        Assert.All(walls, post => Assert.Equal(site.HouseDiscRadius + CivicLayout.WallMargin, new System.Numerics.Vector2(post.X - site.Center.X, post.Z - site.Center.Z).Length(), 2));
        var ring = (int)MathF.Round(MathF.Tau * (site.HouseDiscRadius + CivicLayout.WallMargin) / CivicLayout.PostSpacing);
        Assert.True(walls.Count < ring);
    }

    [Fact]
    public void LaunchOptions_SetAPriority_AndOpenThePanel()
    {
        var options = LaunchOptions.Parse(["--priority=preparation", "--settlement-panel=on"]);

        Assert.Equal(SettlementPriority.Preparation, options.Priority);
        Assert.True(options.SettlementPanel);
        Assert.Single(LaunchOptions.Parse(["--priority=7"]).Warnings);
    }
}
