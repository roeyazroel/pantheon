using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Migration;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Migration;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Migration;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Migration as the player sees and drives it (ADR-016): alerts, labels, the overlay, the F1 lines, commands and options.</summary>
public sealed class MigrationPresentationTests
{
    [Fact]
    public void Alerts_TellThePlayer_WhoLeaves_WhoArrives_AndWhoIsTurnedAway()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        world.Settlements[0].Beliefs.Add(new() { GodId = 1, Adherence = 0.5f, Faith = 0.5f, Love = 0f, Fear = 0f, Doubt = 0f });
        WorldEvent[] events =
        [
            new MigrationStarted { Tick = 1, GroupId = 1, OriginSettlementId = 1, DestinationSettlementId = 2, People = 12, Cause = MigrationCause.Fear, GodId = 1, AbandonedGodId = 1 },
            new MigrationStarted { Tick = 1, GroupId = 2, OriginSettlementId = 2, DestinationSettlementId = 1, People = 9, Cause = MigrationCause.War, GodId = 3, AbandonedGodId = null },
            new MigrationEnded { Tick = 2, GroupId = 2, OriginSettlementId = 2, SettlementId = 1, People = 9, Outcome = MigrationOutcome.Converted, GodId = 3, HostGodId = 1 },
            new MigrationEnded { Tick = 2, GroupId = 3, OriginSettlementId = 1, SettlementId = 2, People = 7, Outcome = MigrationOutcome.Converted, GodId = 1, HostGodId = 2 },
            new MigrationEnded { Tick = 2, GroupId = 4, OriginSettlementId = 1, SettlementId = 2, People = 5, Outcome = MigrationOutcome.Perished, GodId = 1, HostGodId = null },
            new MigrantsTurnedAway { Tick = 3, GroupId = 5, SettlementId = 2, ByGodId = 4, People = 6, GodId = 1, NextSettlementId = 1 },
            new MigrantsTurnedAway { Tick = 3, GroupId = 6, SettlementId = 1, ByGodId = 1, People = 4, GodId = 3, NextSettlementId = 2 },
            new RefugeePolicyChanged { Tick = 3, GodId = 2, Policy = RefugeePolicy.DemandConversion },
            new RefugeePolicyChanged { Tick = 3, GodId = 1, Policy = RefugeePolicy.TurnAway },
        ];

        var alerts = new AlertFeed().Push(events, world, 1, 1.0);

        Assert.Equal(
            [
                ("12 people leave Town1 for Town2 (fear of their god), and many of them give you up.", AlertSeverity.Warning),
                ("9 migrants from Town2 settled in Town1, and many turned to you.", AlertSeverity.Good),
                ("Rivalis made 7 of your followers from Town1 turn to it in Town2.", AlertSeverity.Warning),
                ("5 of your followers from Town1 died on the road.", AlertSeverity.Danger),
                ("Quarta turned away 6 of your followers at Town2.", AlertSeverity.Warning),
                ("Your people turned away 4 migrants at Town1.", AlertSeverity.Info),
                ("Rivalis now demands that migrants convert.", AlertSeverity.Info),
            ],
            alerts.Select(a => (a.Text, a.Severity)));
    }

    [Fact]
    public void Labels_MarkPeopleLeaving_AndMigrantsComing()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        world.Settlements[0].Emigration = 3f;
        world.Migration.Groups.Add(new MigrationGroup
        {
            Id = 1, OriginSettlementId = 1, DestinationSettlementId = 2, Cause = MigrationCause.Famine, Population = 10, Food = 0f, Health = 1f,
            Position = world.Settlements[0].Position, Path = [world.Settlements[1].Position], Beliefs = [], TurnedAwayFrom = [], DepartedTick = 0,
        });

        Assert.Equal(["people leaving"], MigrationText.Marks(world.Settlements[0], world));
        Assert.Equal(["migrants coming"], MigrationText.Marks(world.Settlements[1], world));
        Assert.EndsWith("\npeople leaving", SettlementLabel.Format(world.Settlements[0], world, SettlementWorlds.Rules), StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_ShowsThePressureToLeave_AndTheWayOfPeopleOnTheRoad()
    {
        var world = MigrationWorlds.Create(200f, 700f);
        world.Migration.Groups.Add(new MigrationGroup
        {
            Id = 1, OriginSettlementId = 1, DestinationSettlementId = 2, Cause = MigrationCause.War, Population = 10, Food = 0f, Health = 1f,
            Position = new GroundPosition(300f, 512f), Path = [new GroundPosition(700f, 512f)],
            Beliefs = [new() { GodId = 3, Adherence = 1f, Faith = 1f, Love = 0f, Fear = 0f, Doubt = 0f }], TurnedAwayFrom = [], DepartedTick = 0,
        });
        var view = TestContent.DivineView;

        var image = OverlayMap.Build(world, OverlayKind.Migration, 1, DivineWorlds.Rules, SettlementWorlds.Rules, view, s => s.Id == 1 ? 1f : 0f);

        Assert.StartsWith("Migration overlay", image.Legend, StringComparison.Ordinal);
        Assert.Equal(OverlayKind.Migration, OverlayMap.Next(OverlayKind.Danger));
        Assert.Null(OverlayMap.Next(OverlayKind.Migration));
        world.Regions.TryGetRegionIdAt(500f, 512f, out var road);
        Assert.Equal(RgbColor.ToByte(view.GodColor(3).R), image.Rgba[road * 4]);
        var home = world.Settlements[0].RegionId * 4;
        Assert.True(image.Rgba[home] > image.Rgba[home + 1], "the lands of a village people want to leave are red");
    }

    [Fact]
    public void DebugPanel_ListsGroupsPoliciesAndASettlementsPressure()
    {
        var world = MigrationWorlds.Create(200f, 450f);
        world.Settlements[0].Emigration = 4.5f;
        world.Gods[1].RefugeePolicy = RefugeePolicy.DemandConversion;
        world.Migration.Groups.Add(new MigrationGroup
        {
            Id = 3, OriginSettlementId = 2, DestinationSettlementId = 1, Cause = MigrationCause.Danger, Population = 17, Food = 40f, Health = 0.9f,
            Position = new GroundPosition(400f, 512f), Path = [new GroundPosition(200f, 512f)],
            Beliefs = [new() { GodId = 2, Adherence = 0.7f, Faith = 1f, Love = 0f, Fear = 0f, Doubt = 0f }], TurnedAwayFrom = [], DepartedTick = 0,
        });
        var snapshot = new DebugSnapshot
        {
            FramesPerSecond = 60,
            World = world,
            SettlementRules = SettlementWorlds.Rules,
            DivineRules = DivineWorlds.Rules,
            MigrationRules = MigrationWorlds.Rules,
            MigrationPressure = new MigrationPressure(0.42f, MigrationCause.War),
            RefugeeChoices = [(2, RefugeePolicy.DemandConversion, "its nature")],
            IsPaused = false,
            TimeScale = 1,
            TicksPerSecond = 10,
            TerrainBackend = "test",
            TerrainIsFallback = false,
            ZoomBand = CameraZoomBand.Regional,
            CameraDistance = 380f,
            RegionBoundariesVisible = false,
            SelectedSettlementId = 1,
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains("Migration: 1 groups, 17 people on the road; refugees: Testa welcome, Rivalis demand conversion (its nature), Tertia welcome, Quarta welcome", lines);
        Assert.Contains("  #3 17 from Town2 to Town1 (danger): food 40, health 0.90, 1 waypoints left, mostly following Rivalis", lines);
        Assert.Contains("  migration: pressure 0.42 (war) (leaving above 0.30), 4.5 want to leave, 17 on their way here", lines);
    }

    [Fact]
    public void Commands_SetThePlayersPolicy_AndSendPeopleAway()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var home = session.World.Settlements[0];

        Assert.Equal("Your people now turn migrants away", MigrationCommands.SetPolicy(session, RefugeePolicy.TurnAway));
        Assert.Equal("That is already your refugee policy", MigrationCommands.SetPolicy(session, RefugeePolicy.TurnAway));
        Assert.Equal(RefugeePolicy.TurnAway, session.LocalGod!.RefugeePolicy);

        Assert.Equal("Point at a village to send people away", MigrationCommands.SendFrom(session, new GroundPosition(-500f, -500f)));
        var status = MigrationCommands.SendFrom(session, home.Position, 10);
        Assert.StartsWith($"10 people leave {home.Name} for ", status, StringComparison.Ordinal);
        Assert.Contains(session.DrainEvents(), e => e is MigrationStarted { People: 10 });
    }

    [Fact]
    public void LaunchOptions_SetTheRefugeePolicy_AndSendPeopleOut()
    {
        Assert.Equal(RefugeePolicy.DemandConversion, LaunchOptions.Parse(["--refugees=convert"]).Refugees);
        Assert.Equal(RefugeePolicy.TurnAway, LaunchOptions.Parse(["--refugees=TurnAway"]).Refugees);
        Assert.Equal(25, LaunchOptions.Parse(["--migrate=25"]).Migrate);
        Assert.Equal(OverlayKind.Migration, LaunchOptions.Parse(["--overlay=migration"]).Overlay);

        var bad = LaunchOptions.Parse(["--refugees=maybe", "--migrate=-3"]);
        Assert.Null(bad.Refugees);
        Assert.Null(bad.Migrate);
        Assert.Equal(2, bad.Warnings.Count);
    }

    [Fact]
    public void Panel_ShowsEachGodsRefugeePolicy()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        session.World.Gods[1].RefugeePolicy = RefugeePolicy.DemandConversion;

        var entry = Pantheon.Game.Diplomacy.DiplomacyReadout.Build(session).First();

        Assert.Equal(RefugeePolicy.DemandConversion, entry.RefugeePolicy);
        Assert.Equal("demands that migrants convert", MigrationText.Practice(entry.RefugeePolicy));
        Assert.Equal(["Welcome", "Demand conversion", "Turn away"], Enum.GetValues<RefugeePolicy>().Select(MigrationText.Policy));
    }

    [Fact]
    public void Content_GivesEveryPersonalityARefugeePolicy()
    {
        var profiles = TestContent.Shipped.GodAi.Profiles!;

        Assert.Equal(RefugeePolicy.Welcome, profiles.Single(p => p.Personality == Domain.Gods.GodPersonality.Guardian).RefugeePolicy);
        Assert.Equal(RefugeePolicy.DemandConversion, profiles.Single(p => p.Personality == Domain.Gods.GodPersonality.Tyrant).RefugeePolicy);
        Assert.Equal(RefugeePolicy.TurnAway, profiles.Single(p => p.Personality == Domain.Gods.GodPersonality.Isolationist).RefugeePolicy);
        Assert.Equal(MigrationWorlds.Rules, TestContent.Shipped.Migration);
    }
}
