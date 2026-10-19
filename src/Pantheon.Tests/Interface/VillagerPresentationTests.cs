using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Book;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Villagers;
using Pantheon.Networking.Protocol;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>Following a villager in the game (spec §80, ADR-043): /, the Book's Villager page, F1, alerts, the launch option and shared sessions.</summary>
public sealed class VillagerPresentationTests
{
    [Fact]
    public void Slash_FollowsTheFigureAtTheCursor_TheSamePersonEachTime_AndShiftSlashLetsThemGo()
    {
        var (session, village, figure) = Start();
        var view = TestContent.SettlementView;

        var picked = VillagerCommands.Pick(session, view, figure.Position);
        var status = VillagerCommands.FollowAt(session, view, figure.Position);

        var villager = VillagerCommands.Of(session.World, session.LocalGodId)!;
        Assert.Equal((figure.Slot, figure.Home), (picked!.Slot, picked.Home));
        Assert.StartsWith($"You follow {villager.Name}, a {VillagerText.Worker(villager.Work)} of {village.Name}, aged ", status, StringComparison.Ordinal);
        Assert.Equal((village.Id, figure.Slot), (villager.SettlementId, villager.Slot!.Value));
        Assert.Equal("Nobody there: point at someone in a village and press / to follow them", VillagerCommands.FollowAt(session, view, new GroundPosition(5f, 5f)));
        Assert.Equal($"You let {villager.Name} go back to the crowd of {village.Name}", VillagerCommands.Release(session));
        Assert.Equal("You follow no villager", VillagerCommands.Release(session));
        VillagerCommands.Follow(session, figure);
        Assert.Equal(villager.Name, VillagerCommands.Of(session.World, session.LocalGodId)!.Name);
    }

    [Fact]
    public void TheBooksVillagerPage_TellsTheirLife_WithTheirAgeAtEachMemory_AndF1TheirNeedsOptionsAndDanger()
    {
        var (session, village, figure) = Start();
        Assert.Equal("You follow no villager.", BookReadout.Build(session, BookTab.Villager).Lines[0].Text);
        VillagerCommands.Follow(session, figure);
        session.RunTicks(1200);
        var villager = VillagerCommands.Of(session.World, session.LocalGodId)!;
        var rules = session.Content.SettlementRules.Villagers;

        var page = BookReadout.Build(session, BookTab.Villager);
        var lines = DebugReadout.Build(Snapshot(session)).Lines;

        Assert.Equal(("Villager", VillagerText.Title(rules, villager, session.World)), (BookReadout.Name(BookTab.Villager), page.Lines[0].Text));
        Assert.Contains(page.Lines, l => l.Text == "What they want" && l.Tone == BookTone.Heading);
        Assert.Contains(page.Lines, l => l.Text.StartsWith("To eat: ", StringComparison.Ordinal));
        Assert.Equal($"At {Math.Floor(villager.AgeYears):0}, first noticed by {session.LocalGod!.Name} in {village.Name}.", page.Lines[^1].Text);
        Assert.Contains($"Villager of {session.LocalGod!.Name}: {VillagerText.Title(rules, villager, session.World)}: {VillagerText.Doing(villager, session.World)}; {VillagerText.Faith(villager, session.World)}", lines);
        Assert.Contains(lines, l => l.StartsWith("  options: ", StringComparison.Ordinal) && l.Contains("eat ", StringComparison.Ordinal) && l.Contains("pray ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  danger: 0.00 (the fire); flees at 0.25", StringComparison.Ordinal));
    }

    [Fact]
    public void Words_NameEveryMemoryActivityAndFate_WithoutAssumingAVillagersPronouns()
    {
        var (session, village, _) = Start();
        var world = session.World;
        var villager = new VillagerState
        {
            Id = 1, Name = "Mara", FollowerGodId = 1, SettlementId = village.Id, Work = VillagerWork.Building, GodId = 1, Faith = 0.6f,
            Home = village.Position, WorkPlace = village.Position, Shrine = village.Position, Position = village.Position, AgeYears = 30f, LifeYears = 70f, FollowedTick = 0,
        };
        var texts = new List<string>();
        foreach (var deed in Enum.GetValues<VillagerDeed>())
        {
            texts.Add(VillagerText.Memory(new VillagerMemory { Tick = 0, Deed = deed, SettlementId = village.Id, GodId = 1, Detail = nameof(DisasterKind.Fire), Count = 2 }, villager, world));
        }

        foreach (var activity in Enum.GetValues<VillagerActivity>())
        {
            villager.Activity = activity;
            texts.Add(VillagerText.Doing(villager, world));
            texts.Add(VillagerText.Verb(activity));
        }

        texts.AddRange(Enum.GetValues<VillagerFate>().Select(VillagerText.Fate));
        texts.AddRange(Enum.GetValues<VillagerRejection>().Skip(1).Select(VillagerText.Describe));

        Assert.All(texts, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        Assert.All(texts, t => Assert.DoesNotMatch(@"\b(he|she|him|his|her|hers)\b", t));
        Assert.Equal($"lived through the fire in {village.Name} (2 died)", texts[(int)VillagerDeed.Survived]);
        Assert.Equal($"fled the fire in {village.Name}", texts[(int)VillagerDeed.Fled]);
        Assert.Equal(
            $"helped raise the temple of {village.Name}",
            VillagerText.Memory(new VillagerMemory { Tick = 0, Deed = VillagerDeed.Built, SettlementId = village.Id, Detail = nameof(BuildingKind.Temple) }, villager, world));
    }

    [Fact]
    public void Alerts_TellThePlayerOfTheirVillagersFlight_TurningAndDeath_AndNotOfAnotherGodsVillager()
    {
        var (session, village, _) = Start();
        var world = session.World;
        var me = session.LocalGodId!.Value;
        var feed = new AlertFeed();
        List<WorldEvent> events =
        [
            new VillagerFled { Tick = 0, VillagerId = 1, Name = "Mara", GodId = me, SettlementId = village.Id, RefugeId = null, Cause = VillagerFate.Flood },
            new VillagerTurned { Tick = 0, VillagerId = 1, Name = "Mara", GodId = me, FromGodId = 2, ToGodId = me },
            new VillagerDied { Tick = 0, VillagerId = 1, Name = "Mara", GodId = me, SettlementId = village.Id, Fate = VillagerFate.OldAge, Age = 71 },
            new VillagerDied { Tick = 0, VillagerId = 2, Name = "Tobin", GodId = me + 1, SettlementId = village.Id, Fate = VillagerFate.Fire, Age = 40 },
        ];

        var alerts = feed.Push(events, world, me, 1.0);

        Assert.Equal(
            [
                $"Mara flees the flood in {village.Name}.",
                $"Mara turns from {world.Gods.Single(g => g.Id == 2).Name} to {session.LocalGod!.Name}.",
                $"Mara of {village.Name} dies of old age, aged 71. Press / on someone else to follow them.",
            ],
            alerts.Select(a => a.Text));
        Assert.Equal(AlertSeverity.Good, alerts[1].Severity);
    }

    [Fact]
    public void LaunchOptions_FollowAVillagerAtTheStart_AndOpenTheBookAtTheirPage()
    {
        var options = LaunchOptions.Parse(["--villager=on", "--book=villager"]);
        var bad = LaunchOptions.Parse(["--villager=maybe"]);

        Assert.Empty(options.Warnings);
        Assert.Equal((true, BookTab.Villager), (options.Villager, options.Book));
        Assert.False(bad.Villager);
        Assert.Equal("'--villager' needs on or off, got 'maybe'.", Assert.Single(bad.Warnings));
    }

    [Fact]
    public void AJoinedPlayer_FollowsAVillagerOnTheHost_AndTheVillagersReachEveryPlayer()
    {
        var shared = SharedWorlds.Start();
        var ana = shared.Join("Ana");
        var village = shared.Host.World.Settlements.First(s => s.Population > 0);
        var figure = VillagerCommands.First(ana.Session!, TestContent.SettlementView, ana.Session!.World.Settlements.Single(s => s.Id == village.Id))!;

        var asking = VillagerCommands.Follow(ana.Session!, figure);
        var statuses = shared.Exchange(ana);

        Assert.Equal("Asking the host...", asking);
        Assert.Contains(statuses, s => s.StartsWith("You follow ", StringComparison.Ordinal));
        var followed = Assert.Single(shared.Host.World.Villagers);
        Assert.Equal((2, village.Id), (followed.FollowerGodId, followed.SettlementId));
        var replica = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(shared.Host.World));
        replica.Villagers.Clear();
        shared.Host.RunTicks(300);
        WorldSections.Apply(replica, WorldSection.Notables, WorldSections.Write(shared.Host.World, WorldSection.Notables));
        Assert.Equal(shared.Host.World.Villagers[0].Position, replica.Villagers.Single().Position);
        Assert.Equal(shared.Host.World.NextVillagerId, replica.NextVillagerId);
    }

    [Fact]
    public void TheSameWorld_LivesTheSameVillagersLife()
    {
        var lives = new List<string>();
        for (var run = 0; run < 2; run++)
        {
            var (session, _, figure) = Start();
            VillagerCommands.Follow(session, figure);
            session.RunTicks(3000);
            lives.Add(WorldSaveSerializer.Serialize(session.World));
        }

        Assert.Equal(lives[0], lives[1]);
    }

    /// <summary>A small world, its first village, and the figure of it nearest its shrine.</summary>
    private static (GameSession Session, Domain.Settlements.SettlementState Village, FollowVillager Figure) Start()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var village = session.World.Settlements.First(s => s.Population > 20);
        return (session, village, VillagerCommands.First(session, TestContent.SettlementView, village)!);
    }

    /// <summary>Frame data for F1.</summary>
    private static DebugSnapshot Snapshot(GameSession session) => new()
    {
        FramesPerSecond = 60,
        World = session.World,
        SettlementRules = session.Content.SettlementRules,
        DivineRules = session.Content.DivineRules,
        LocalGodId = session.LocalGodId,
        IsPaused = false,
        TimeScale = 1,
        TicksPerSecond = 10,
        TerrainBackend = "Terrain3D",
        TerrainIsFallback = false,
        ZoomBand = CameraZoomBand.Regional,
        CameraDistance = 300f,
        RegionBoundariesVisible = false,
    };
}
