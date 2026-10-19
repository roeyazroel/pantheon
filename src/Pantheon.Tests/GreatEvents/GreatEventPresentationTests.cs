using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Book;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.GreatEvents;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking.Protocol;
using Pantheon.Persistence;
using Pantheon.Simulation.GreatEvents;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.GreatEvents;

/// <summary>How the great events and the plague are told, shown, driven and shared (ADR-034).</summary>
public sealed class GreatEventPresentationTests
{
    private static readonly GreatEventRules Rules = TestContent.Shipped.Disasters.GreatEvents;

    [Fact]
    public void Alerts_TellEveryoneOfGreatEvents_AndYouOfThePlagueAmongYourPeople()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var (mine, theirs) = (world.Settlements[0], world.Settlements[1]);
        var begun = new List<WorldEvent>();
        var plague = GreatEventModel.Begin(Rules, world, GreatEventKind.Plague, null, null, 3, begun);
        var reformation = GreatEventModel.Begin(Rules, world, GreatEventKind.Reformation, 600, 1, 40, begun);
        var war = GreatEventModel.Begin(Rules, world, GreatEventKind.WorldWar, null, null, 4, begun);
        WorldEvent[] events =
        [
            begun[0], begun[1], begun[2],
            new GreatEventEnded { Tick = 2, EventId = plague.Id, Kind = GreatEventKind.Plague, Name = plague.Name, Peak = 5 },
            new GreatEventEnded { Tick = 2, EventId = 9, Kind = GreatEventKind.Eclipse, Name = "the Dark Noon", Peak = 4 },
            new PlagueStruck { Tick = 2, SettlementId = mine.Id, FromSettlementId = theirs.Id },
            new PlagueStruck { Tick = 2, SettlementId = theirs.Id },
        ];

        var alerts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()).ToList();

        Assert.Equal(($"{GreatEventText.Capitalised(plague.Name)} spreads: 3 villages are sick. Heal them, or it will travel the roads.", AlertSeverity.Danger), (alerts[0]!.Text, alerts[0]!.Severity));
        Assert.Equal(($"Your faith reforms itself in {reformation.Name}: doubt gives way to faith, but 40 leave you.", AlertSeverity.Good), (alerts[1]!.Text, alerts[1]!.Severity));
        Assert.Equal($"{GreatEventText.Capitalised(war.Name)}: 4 gods are at war at once, and fear grows everywhere.", alerts[2]!.Text);
        Assert.Equal($"{GreatEventText.Capitalised(plague.Name)} is over.", alerts[3]!.Text);
        Assert.Equal("The sun returns.", alerts[4]!.Text);
        Assert.Equal($"The plague reaches {mine.Name} from {theirs.Name}: heal its people before it spreads.", alerts[5]!.Text);
        Assert.Null(alerts[6]);
    }

    [Fact]
    public void TheHistory_RemembersGreatEventsByName()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var events = new List<WorldEvent>();
        GreatEventModel.Begin(Rules, world, GreatEventKind.Plague, null, null, 3, events);
        GreatEventModel.Begin(Rules, world, GreatEventKind.Reformation, 600, 1, 12, events);
        EclipseModel.Darken(Rules, world, events);

        new HistoryRecorder(TestContent.Shipped.History).Record(world, events, 0);

        Assert.Equal(
            ["The Grey Plague breaks out", "The Second Covenant of Testa", "The Black Sun: the sun goes dark"],
            world.History.Select(r => HistoryText.Title(r, world)));
        Assert.Equal(["3 villages sick at once", "12 left the faith rather than reform"], world.History.Take(2).Select(r => HistoryText.Detail(r, world)));
        Assert.Equal([HistoryKind.Plague, HistoryKind.Reformation, HistoryKind.Eclipse], world.History.Select(r => r.Kind));
    }

    [Fact]
    public void TheCommands_TheBar_TheBook_TheLabels_AndTheDebugPanel_ShowAndDriveThem()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var home = world.Settlements[0];

        Assert.Equal("The plague breaks out in " + home.Name, GreatEventCommands.Start(session, GreatEventAtStart.Plague, home.Position));
        Assert.Contains("plague", Pantheon.Game.Disasters.DisasterText.Marks(home, world, session.Content.SettlementRules));
        Assert.EndsWith(": the sun goes dark", GreatEventCommands.Start(session, GreatEventAtStart.Eclipse, home.Position), StringComparison.Ordinal);
        Assert.Equal("The sun is already dark", GreatEventCommands.Start(session, GreatEventAtStart.Eclipse, home.Position));
        Assert.EndsWith(": your faith reforms", GreatEventCommands.Start(session, GreatEventAtStart.Reformation, home.Position), StringComparison.Ordinal);
        Assert.Equal([GreatEventAtStart.Eclipse, GreatEventAtStart.Reformation, GreatEventAtStart.Plague], new GreatEventAtStart?[] { GreatEventAtStart.Plague, GreatEventAtStart.Eclipse, GreatEventAtStart.Reformation }.Select(GreatEventCommands.Next));

        var eclipse = GreatEventModel.Active(world, GreatEventKind.Eclipse)!;
        Assert.Contains("the sun is dark (0:45 left)", GreatEventText.Bar(world), StringComparison.Ordinal);
        Assert.Equal(0f, GreatEventText.Darkness(world));
        world.Tick += 50;
        Assert.Equal(1f, GreatEventText.Darkness(world));
        Assert.Contains(WorldPage.Build(session).Lines, l => l.Text.StartsWith(GreatEventText.Capitalised(eclipse.Name) + ": the sun is dark", StringComparison.Ordinal) && l.Tone == BookTone.Warning);

        var lines = DebugReadout.Build(Snapshot(session, home.Id)).Lines;
        Assert.Contains(lines, l => l.StartsWith("Great events: ", StringComparison.Ordinal) && l.Contains("sick: " + home.Name + " 30%", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  plague: 30% sick, 0% immune, shielded ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("  faiths: ", StringComparison.Ordinal));

        Assert.Equal(GreatEventAtStart.Eclipse, LaunchOptions.Parse(["--great-event=eclipse"]).GreatEvent);
        Assert.Single(LaunchOptions.Parse(["--great-event=comet"]).Warnings);
    }

    [Fact]
    public void TheGreatEvents_AreShared_AndTheirRulesChecked()
    {
        var host = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        var replica = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        EclipseModel.Darken(Rules, host, []);
        host.GreatEvents.LastReformations[1] = host.Tick;

        WorldSections.Apply(replica, WorldSection.GreatEvents, WorldSections.Write(host, WorldSection.GreatEvents));

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(host.GreatEvents), System.Text.Json.JsonSerializer.Serialize(replica.GreatEvents));
        Assert.Equal(6, Rules.Names.Count);
        Assert.Equal(1f, TestContent.WeatherView.EclipseDarkness + 0.15f, 4);
        var view = TestContent.ReadText(Pantheon.Game.Presentation.WeatherViewSettings.FileName).Replace("\"EclipseDarkness\": 0.85", "\"EclipseDarkness\": 1.85", StringComparison.Ordinal);
        Assert.Contains("EclipseDarkness", Assert.ThrowsAny<Exception>(() => Pantheon.Game.Presentation.WeatherViewSettings.Parse(view)).Message, StringComparison.Ordinal);
    }

    /// <summary>A debug snapshot of a session's world with one settlement selected.</summary>
    private static DebugSnapshot Snapshot(GameSession session, int selected) => new()
    {
        FramesPerSecond = 60,
        World = session.World,
        SettlementRules = session.Content.SettlementRules,
        DivineRules = DivineWorlds.Rules,
        DisasterRules = session.Content.Disasters,
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
}
