using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.GreatEvents;
using Pantheon.Game.History;
using Pantheon.Game.Multiplayer;
using Pantheon.Networking.Protocol;
using Pantheon.Simulation.GreatEvents;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.GreatEvents;

/// <summary>How the ancient beast and the anomaly are told, driven and shared (ADR-035).</summary>
public sealed class BeastPresentationTests
{
    private static readonly GreatEventRules Rules = TestContent.Shipped.Disasters.GreatEvents;

    [Fact]
    public void Alerts_AndTheHistory_TellOfTheBeast_AndTheAnomaly()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f);
        var beast = BeastModel.Find(Rules.Beast, world)!;
        var events = new List<WorldEvent>();
        BeastModel.Wake(Rules, world, beast, events);
        AnomalyModel.Surge(Rules, world, world.Settlements[0].Position, events);
        beast.HarmByGod[1] = 30f;
        beast.HarmByGod[2] = 60f;
        BeastModel.Fall(Rules, world, beast, events);

        var alerts = events.Select(e => new AlertFeed().Push([e], world, 1, 1.0).SingleOrDefault()).ToList();

        var name = GreatEventText.Capitalised(beast.Name);
        Assert.Equal(($"{name} wakes and comes down from its lair! It will ravage the villages until it is slain or tires: send your Creature, and smite it.", AlertSeverity.Danger), (alerts[0]!.Text, alerts[0]!.Severity));
        var surge = world.GreatEvents.Events.Find(e => e.Kind == GreatEventKind.Anomaly)!;
        Assert.StartsWith(GreatEventText.Capitalised(surge.Name) + " shimmers near " + world.Settlements[0].Name, alerts[1]!.Text, StringComparison.Ordinal);
        Assert.Equal(($"{name} is slain, and the glory is yours, with every god who fought it.", AlertSeverity.Good), (alerts[2]!.Text, alerts[2]!.Severity));
        Assert.Null(alerts[3]);

        new HistoryRecorder(TestContent.Shipped.History).Record(world, events, 0);
        Assert.Equal([$"{name} wakes", GreatEventText.Capitalised(surge.Name) + " shimmers", "The Slaying of " + beast.Name], world.History.Select(r => HistoryText.Title(r, world)));
        Assert.Equal("by Rivalis and 1 other god", HistoryText.Detail(world.History[2], world));
    }

    [Fact]
    public void TheCommands_WakeTheBeast_AndSurgeTheAnomaly_AndTheBeastIsShared()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;

        var woke = GreatEventCommands.Start(session, GreatEventAtStart.Beast, world.Settlements[0].Position);
        var beast = world.GreatEvents.Beast!;
        Assert.Equal(GreatEventText.Capitalised(beast.Name) + " wakes", woke);
        Assert.StartsWith(GreatEventText.Capitalised(beast.Name) + " is looking for a village (100%)", GreatEventCommands.Start(session, GreatEventAtStart.Beast, world.Settlements[0].Position), StringComparison.Ordinal);
        Assert.EndsWith(" shimmers near " + world.Settlements[0].Name, GreatEventCommands.Start(session, GreatEventAtStart.Anomaly, world.Settlements[0].Position), StringComparison.Ordinal);
        Assert.Equal("Power already surges somewhere", GreatEventCommands.Start(session, GreatEventAtStart.Anomaly, world.Settlements[0].Position));
        Assert.Contains("miracles there cost less", GreatEventText.Bar(world), StringComparison.Ordinal);

        var replica = GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        WorldSections.Apply(replica, WorldSection.GreatEvents, WorldSections.Write(world, WorldSection.GreatEvents));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(world.GreatEvents), System.Text.Json.JsonSerializer.Serialize(replica.GreatEvents));
        Assert.Equal(beast.Name, replica.GreatEvents.Beast!.Name);
    }

    [Fact]
    public void AnAiGodsCreature_ComesToFightTheBeast_ThatRavagesItsVillage()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var ai = world.Gods.First(g => g.Controller == Pantheon.Domain.Gods.GodController.Ai && world.Creatures.Exists(c => c.GodId == g.Id));
        var creature = world.Creatures.First(c => c.GodId == ai.Id);
        var village = world.Settlements.OrderByDescending(s => s.Beliefs.Find(b => b.GodId == ai.Id)?.Adherence ?? 0f).First();
        var beast = BeastModel.Find(Rules.Beast, world)!;
        BeastModel.Wake(Rules, world, beast, []);
        beast.Position = new GroundPosition(village.Position.X + 10f, village.Position.Z);
        beast.TargetSettlementId = village.Id;
        creature.Position = new GroundPosition(village.Position.X + 200f, village.Position.Z);

        var engine = session.Content.CreateEngine();
        engine.Run(world, 20);

        Assert.Equal(Pantheon.Domain.Creatures.CreatureAction.GoTo, creature.Activity.Action);
    }

    [Fact]
    public void BrokenBeastAndAnomalyRules_AreRejected()
    {
        foreach (var (shipped, broken, expected) in new[]
        {
            ("\"Bite\": 0.025", "\"Bite\": 1.5", "GreatEvents.Beast.Bite"),
            ("\"CostFactor\": 0.5", "\"CostFactor\": 1.5", "GreatEvents.Anomaly.CostFactor"),
        })
        {
            var text = TestContent.ReadText(Pantheon.Persistence.ContentJson.DisastersFileName);
            Assert.Contains(shipped, text, StringComparison.Ordinal);
            var files = GameContentFiles.Read(name => name == Pantheon.Persistence.ContentJson.DisastersFileName ? text.Replace(shipped, broken, StringComparison.Ordinal) : TestContent.ReadText(name));
            Assert.Contains(expected, Assert.Throws<Pantheon.Persistence.ContentFormatException>(() => GameContent.Parse(files)).Message, StringComparison.Ordinal);
        }
    }
}
