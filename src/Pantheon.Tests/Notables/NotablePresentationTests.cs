using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.Notables;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Book;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Notables;
using Pantheon.Networking.Protocol;
using Pantheon.Persistence;
using Pantheon.Simulation.History;
using Pantheon.Simulation.Notables;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Notables;

/// <summary>How prophets and elders are told, shown, driven, saved and shared (ADR-032).</summary>
public sealed class NotablePresentationTests
{
    private static readonly NotableRules Rules = TestContent.Shipped.DivineRules.Notables;

    [Fact]
    public void Alerts_TellOfYourProphets_RivalsAmongYourPeople_Martyrs_AndElders()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var (mine, theirs) = (world.Settlements[0], world.Settlements[1]);
        var raised = new List<WorldEvent>();
        var prophet = NotableModel.Raise(Rules, world, NotableKind.Prophet, 1, mine, anointed: true, raised);
        var rival = NotableModel.Raise(Rules, world, NotableKind.Prophet, 2, mine, anointed: false, raised);
        var elder = NotableModel.Raise(Rules, world, NotableKind.Elder, 1, mine, anointed: false, raised);
        var far = NotableModel.Raise(Rules, world, NotableKind.Prophet, 2, theirs, anointed: false, raised);
        WorldEvent[] events =
        [
            raised[0],
            raised[1],
            raised[2],
            raised[3],
            new NotablePreached { Tick = 1, NotableId = rival.Id, GodId = 2, SettlementId = mine.Id, Converted = 5 },
            new NotablePreached { Tick = 1, NotableId = rival.Id, GodId = 2, SettlementId = mine.Id, Converted = 1 },
            new NotableDied { Tick = 1, NotableId = prophet.Id, Name = prophet.Name, Kind = NotableKind.Prophet, GodId = 1, SettlementId = mine.Id, Martyred = true },
            new ElderTurned { Tick = 1, NotableId = elder.Id, SettlementId = mine.Id, FromGodId = 1, ToGodId = 2, Resented = true },
            new ElderTurned { Tick = 1, NotableId = elder.Id, SettlementId = mine.Id, FromGodId = 1, ToGodId = 2 },
            new ElderTurned { Tick = 1, NotableId = elder.Id, SettlementId = theirs.Id, FromGodId = 3, ToGodId = 4 },
        ];

        var alerts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()).ToList();

        Assert.Equal($"{prophet.Name}, a prophet of yours, rises in {mine.Name} at your anointing: they will walk and preach your name.", alerts[0]!.Text);
        Assert.Equal(($"{rival.Name}, a prophet of Rivalis, rises in {mine.Name}.", AlertSeverity.Warning), (alerts[1]!.Text, alerts[1]!.Severity));
        Assert.Null(alerts[2]);
        Assert.Null(alerts[3]);
        Assert.Equal($"A prophet of Rivalis preaches in {mine.Name}: 5 turn to Rivalis.", alerts[4]!.Text);
        Assert.Null(alerts[5]);
        Assert.Equal($"Your prophet {prophet.Name} dies a martyr at {mine.Name}: the faithful hold to you the harder.", alerts[6]!.Text);
        Assert.Equal(($"{elder.Name}, elder of {mine.Name}, turns from you to Rivalis: the village resents you.", AlertSeverity.Danger), (alerts[7]!.Text, alerts[7]!.Severity));
        Assert.Null(alerts[8]);
        Assert.Null(alerts[9]);
        Assert.NotEqual(far.Id, prophet.Id);
    }

    [Fact]
    public void TheHistory_RemembersProphets_Martyrs_AndEldersWhoTurn()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var village = world.Settlements[0];
        var events = new List<WorldEvent>();
        var prophet = NotableModel.Raise(Rules, world, NotableKind.Prophet, 1, village, anointed: true, events);
        var elder = NotableModel.Raise(Rules, world, NotableKind.Elder, 1, village, anointed: false, events);
        events.Add(new NotableDied { Tick = 1, NotableId = prophet.Id, Name = prophet.Name, Kind = NotableKind.Prophet, GodId = 1, SettlementId = village.Id, Martyred = true });
        events.Add(new NotableDied { Tick = 1, NotableId = elder.Id, Name = elder.Name, Kind = NotableKind.Elder, GodId = 1, SettlementId = village.Id, Martyred = false });
        events.Add(new ElderTurned { Tick = 1, NotableId = elder.Id, SettlementId = village.Id, FromGodId = 1, ToGodId = 2, Resented = true });
        events.Add(new ElderTurned { Tick = 1, NotableId = elder.Id, SettlementId = village.Id, FromGodId = 2, ToGodId = 1 });

        new HistoryRecorder(TestContent.Shipped.History).Record(world, events, 0);

        Assert.Equal(
            [$"{prophet.Name}, prophet of Testa, arises in {village.Name}", $"The Martyrdom of {prophet.Name} at {village.Name}", $"{elder.Name}, elder of {village.Name}, turns from Testa to Rivalis"],
            world.History.Select(r => HistoryText.Title(r, world)));
        Assert.Equal("anointed by their god", HistoryText.Detail(world.History.Single(r => r.Kind == HistoryKind.Prophet), world));
    }

    [Fact]
    public void TheProphetOption_RaisesOne_AndTheBookAndTheDebugPanelShowThem()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var god = session.LocalGod!;
        var home = session.World.Settlements.First(s => s.Beliefs.Any(b => b.GodId == god.Id && b.Adherence > 0.5f));

        var status = NotableCommands.RaiseProphet(session, home.Position);

        var prophet = Assert.Single(session.World.Notables);
        var title = $"Prophet {prophet.Name} of {god.Name}";
        Assert.Equal($"{title} rises in {home.Name}", status);
        var world = WorldPage.Build(session).Lines;
        Assert.Contains(world, l => l.Text == "Prophets and elders" && l.Tone == BookTone.Heading);
        Assert.Contains(world, l => l.Text == $"{title}, preaching in {home.Name}" && l.Tone == BookTone.Good);
        var religion = ReligionPage.Build(session).Lines.Select(l => l.Text).ToList();
        Assert.Equal(["Your prophets and elders", $"{title}, preaching in {home.Name}", $"  rose in {home.Name}", "No elder keeps a village to you."], religion.SkipWhile(t => t != "Your prophets and elders").Take(4));

        var lines = DebugReadout.Build(Snapshot(session.World, home.Id)).Lines;
        Assert.Contains($"Notables: 1 prophet ({god.Name} 1), 0 elders", lines);
        Assert.Contains(lines, l => l.StartsWith($"  #{prophet.Id} {title}: preaching in {home.Name}, ", StringComparison.Ordinal) && l.EndsWith($" left; rose in {home.Name}", StringComparison.Ordinal));
        Assert.Contains($"  notables: preaching: {prophet.Name} of {god.Name}", lines);

        Assert.True(LaunchOptions.Parse(["--prophet=on"]).Prophet);
        Assert.Single(LaunchOptions.Parse(["--prophet=maybe"]).Warnings);
    }

    [Fact]
    public void NotableWords_SayWhatTheyAreDoing_AndRemember()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var (home, next) = (world.Settlements[0], world.Settlements[1]);
        var prophet = NotableModel.Raise(Rules, world, NotableKind.Prophet, 1, home, anointed: false, []);
        var elder = NotableModel.Raise(Rules, world, NotableKind.Elder, 2, next, anointed: false, []);
        NotableModel.SetOut(world, prophet, next);

        Assert.Equal($"walking to {next.Name}", NotableText.Doing(prophet, world));
        Assert.Equal($"Elder {elder.Name} of {next.Name}", NotableText.Title(elder, world));
        Assert.Equal($"keeping the faith of {next.Name} in Rivalis", NotableText.Doing(elder, world));
        Assert.Equal($"preached in {next.Name} and won 4", NotableText.Memory(new NotableMemory { Tick = 1, Deed = NotableDeed.Preached, SettlementId = next.Id, Count = 4 }, world));
        Assert.All(Enum.GetValues<NotableDeed>(), d => Assert.NotEmpty(NotableText.Memory(new NotableMemory { Tick = 1, Deed = d, SettlementId = null, Count = 0 }, world)));
    }

    [Fact]
    public void Notables_AreSaved_AndSharedWithJoinedPlayers()
    {
        var host = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        var (home, other, far) = (host.Settlements[0], host.Settlements[1], host.Settlements[^1]);
        var prophet = NotableModel.Raise(Rules, host, NotableKind.Prophet, 1, home, anointed: true, []);
        NotableModel.Raise(Rules, host, NotableKind.Elder, 2, other, anointed: false, []);
        NotableModel.SetOut(host, prophet, far);
        NotableModel.Remember(Rules, prophet, 5, NotableDeed.Preached, home.Id, 7);

        var json = WorldSaveSerializer.Serialize(host);
        var loaded = WorldSaveSerializer.Deserialize(json);
        Assert.Equal(json, WorldSaveSerializer.Serialize(loaded));
        Assert.Equal((2, 3), (loaded.Notables.Count, loaded.NextNotableId));
        Assert.NotEmpty(loaded.Notables[0].Path);
        Assert.Equal(prophet.Path.Count, loaded.Notables[0].Path.Count);

        var replica = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        WorldSections.Apply(replica, WorldSection.Notables, WorldSections.Write(host, WorldSection.Notables));
        Assert.Equal(json, WorldSaveSerializer.Serialize(replica));
    }

    [Fact]
    public void TheShippedNotables_Parse_AndBrokenOnesAreRejected()
    {
        Assert.Equal(40, Rules.Names.Count);
        Assert.Equal(0.4f, TestContent.Shipped.DivineRules.TempleInfluenceStrength);
        var anoint = TestContent.Shipped.Miracles.Miracles.Single(m => m.Id == "anoint");
        Assert.Equal(Pantheon.Domain.Miracles.MiracleEffectKind.Anoint, Assert.Single(anoint.Effects).Kind);

        AssertRejected("\"Devotion\": 0.6", "\"Devotion\": 1.6", "Notables.Devotion");
        AssertRejected("\"MemoryCapacity\": 12", "\"MemoryCapacity\": 0", "Notables.MemoryCapacity");
        AssertRejected("\"TempleInfluenceStrength\": 0.4", "\"TempleInfluenceStrength\": -0.4", "TempleInfluenceStrength");
        AssertRejected("\"TurnDoubt\": 0.15", "\"TurnDoubt\": 0.15, \"Hermits\": 3", "Hermits");
    }

    /// <summary>A debug snapshot of a world with one settlement selected.</summary>
    private static DebugSnapshot Snapshot(WorldState world, int selected) => new()
    {
        FramesPerSecond = 60,
        World = world,
        SettlementRules = SettlementWorlds.Rules,
        DivineRules = DivineWorlds.Rules,
        IsPaused = false,
        TimeScale = 1,
        TicksPerSecond = 10,
        TerrainBackend = "test",
        TerrainIsFallback = false,
        ZoomBand = CameraZoomBand.Regional,
        CameraDistance = 380f,
        RegionBoundariesVisible = false,
        SelectedSettlementId = selected,
    };

    /// <summary>Breaks the shipped divine rules one way and checks the error names the member.</summary>
    private static void AssertRejected(string shipped, string broken, string expected)
    {
        var file = ContentJson.DivineRulesFileName;
        var text = TestContent.ReadText(file);
        Assert.Contains(shipped, text, StringComparison.Ordinal);
        var files = GameContentFiles.Read(name => name == file ? text.Replace(shipped, broken, StringComparison.Ordinal) : TestContent.ReadText(name));

        var exception = Assert.Throws<ContentFormatException>(() => GameContent.Parse(files));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }
}
