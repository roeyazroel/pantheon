using System;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.Gods;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>How the gods' meetings and plans are shown and driven (ADR-036).</summary>
public sealed class PlanPresentationTests
{
    [Fact]
    public void MeetingAGod_IsAnnounced_WithHowYouMet()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var rival = world.Gods[1];
        var place = world.Settlements[0];

        var alerts = new AlertFeed().Push(
            [
                new GodsMet { Tick = 1, GodId = 1, OtherGodId = rival.Id, Way = MeetingWay.Neighbours, SettlementId = place.Id },
                new GodsMet { Tick = 1, GodId = 2, OtherGodId = 3, Way = MeetingWay.Council },
            ],
            world,
            1,
            1.0);

        var alert = Assert.Single(alerts);
        Assert.Equal($"You have met {rival.Name} the {Pantheon.Game.Divine.DivineText.Personality(rival.Personality)}, as neighbours at {place.Name}. You may now deal with it (P).", alert.Text);
        Assert.Equal(AlertSeverity.Info, alert.Severity);
    }

    [Fact]
    public void TheDiplomacyPanel_ShowsGodsNotYetMet_AndAWellInformedPlayerSeesTheirGoals()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var me = session.LocalGodId!.Value;
        var rival = world.Gods.First(g => g.Id != me);
        var unmet = DiplomacyReadout.Build(session).Single(e => e.GodId == rival.Id);

        Meetings.All(world);
        var theirs = world.Settlements.First(s => TreatyModel.Leader(s) == rival.Id);
        var mine = DivineWorlds.Believe(theirs, adherence: 0.45f, faith: 0.5f, godId: me);
        rival.Goals.Add(new GodGoal { Kind = GodGoalKind.Weaken, GodId = me, SinceTick = 0, Weight = 0.8f });
        rival.Goals.Add(new GodGoal { Kind = GodGoalKind.Convert, SettlementId = theirs.Id, SinceTick = 0, Weight = 0.5f });
        var met = DiplomacyReadout.Build(session).Single(e => e.GodId == rival.Id);

        Assert.False(unmet.Met);
        Assert.Empty(unmet.Available);
        Assert.True(met.Met);
        Assert.True(TreatyModel.Leader(theirs) == rival.Id && mine.Adherence > 0f);
        Assert.EndsWith($"; it means to weaken you and convert {theirs.Name}.", met.Intelligence, StringComparison.Ordinal);
    }

    [Fact]
    public void F1_ShowsEachAiGodsPlan_ItsOptions_AndTheGodsItKnows()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var rival = world.Gods[1];
        Pantheon.Simulation.Diplomacy.Acquaintance.Introduce(world, rival.Id, 1, MeetingWay.Neighbours, null, []);
        rival.Goals.Add(new GodGoal { Kind = GodGoalKind.Befriend, GodId = 1, SinceTick = 0, Weight = 0.6f });
        rival.NextPlanTick = 120;
        world.Tick = 20;
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = session.Content.SettlementRules,
            DivineRules = session.Content.DivineRules,
            GodAiCandidates = [(rival.Id, session.PreviewGodAi(rival))],
            GodGoalOptions = [(rival.Id, [new Pantheon.Simulation.Gods.GoalOption(GodGoalKind.Convert, world.Settlements[0].Id, null, 0.7f, "convert it (0% ours)")])],
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "Terrain3D",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 300f,
            RegionBoundariesVisible = false,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        var plans = $"  plans: befriend {world.Gods[0].Name} 0.60 for 2s; next plan in 10s; knows {world.Gods[0].Name}; {world.Gods.Count - 2} not yet met";
        Assert.Contains(plans, lines);
        Assert.Contains("  could: 0.70 convert it (0% ours)", lines);
        Assert.Empty(session.PreviewGodGoals(session.LocalGod!));
        Assert.NotEmpty(session.PreviewGodGoals(rival));
    }

    [Fact]
    public void TheWords_ForEveryGoalAndMeeting_AndTheChronicleOfAMeeting()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var place = world.Settlements[1];
        string Goal(GodGoalKind kind) => GoalText.Goal(new GodGoal { Kind = kind, SettlementId = place.Id, GodId = 2, SinceTick = 0, Weight = 0.5f }, world, 2);

        Assert.Equal(
            ["protect Town2", "convert Town2", "befriend you", "weaken you", "prepare its people for hard times", "slay the beast", "win back its doubters"],
            Enum.GetValues<GodGoalKind>().Select(Goal));
        Assert.All(Enum.GetValues<MeetingWay>(), way => Assert.NotEmpty(GoalText.Meeting(way, null)));
        Assert.Equal("along the road to Town2", GoalText.Meeting(MeetingWay.Road, place.Name));

        var record = new HistoryRecord { Tick = 0, Kind = HistoryKind.GodsMet, GodId = 1, OtherGodId = 2, SettlementId = place.Id, CreatureId = null, OtherCreatureId = null, Count = (int)MeetingWay.SharedVillage };
        Assert.Equal("The Meeting of Testa and Rivalis", HistoryText.Title(record, world));
        Assert.Equal("both followed in Town2", HistoryText.Detail(record, world));
    }

    [Fact]
    public void TheGodsMetOption_IntroducesEveryGod_AtTheStart()
    {
        var options = LaunchOptions.Parse(["--gods-met=on", "--gods-met=maybe"]);
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));

        var said = PoliticsCommands.MeetAll(session);

        Assert.True(options.GodsMet);
        Assert.Single(options.Warnings);
        Assert.Equal("Every god has met every other", said);
        Assert.All(session.World.Gods, god => Assert.Equal(session.World.Gods.Count - 1, god.KnownGods.Count));
        Assert.Contains(session.DrainEvents(), e => e is GodsMet { Way: MeetingWay.Council });
    }
}
