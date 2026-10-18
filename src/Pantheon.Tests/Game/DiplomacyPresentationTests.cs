using System;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Diplomacy;
using Pantheon.Persistence;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Diplomacy as the player sees it: the panel, the explanations, the alerts and the commands.</summary>
public sealed class DiplomacyPresentationTests
{
    private readonly GameSession _session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));

    private int Rival => _session.World.Gods[1].Id;

    [Fact]
    public void Panel_ListsTheRival_WithTheMovesTheAuthorityWouldAccept()
    {
        var entry = Assert.Single(DiplomacyReadout.Build(_session));

        Assert.Equal(Rival, entry.GodId);
        Assert.Equal(DiplomaticStatus.Neutral, entry.Status);
        Assert.Equal([DiplomaticCommandKind.OfferAlliance, DiplomaticCommandKind.DeclareWar], entry.Available);
        Assert.Equal("has formed no view of you yet", entry.Attitude);
        Assert.Null(entry.Proposal);
    }

    [Fact]
    public void Commands_ReportWhatHappened_OrWhyNot()
    {
        var name = _session.World.Gods[1].Name;

        Assert.Equal($"Offer alliance: {name}", SessionCommands.CommandDiplomacy(_session, Rival, DiplomaticCommandKind.OfferAlliance));
        Assert.Equal($"A proposal between you and {name} is already waiting", SessionCommands.CommandDiplomacy(_session, Rival, DiplomaticCommandKind.OfferAlliance));
        Assert.Equal($"You cannot break alliance towards {name} now", SessionCommands.CommandDiplomacy(_session, Rival, DiplomaticCommandKind.BreakAlliance));
    }

    [Fact]
    public void Explanations_GiveTheReasons()
    {
        var world = _session.World;
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        var village = world.Settlements[0];
        ledger.Record(world, Rival, 1, DiplomaticAct.Converted, village.Id);
        ledger.Record(world, Rival, 1, DiplomaticAct.Betrayed, null);
        ledger.Record(world, 1, Rival, DiplomaticAct.Harmed, village.Id);

        var entry = Assert.Single(DiplomacyReadout.Build(_session));

        Assert.StartsWith("remembers that you betrayed it", entry.Explanation, StringComparison.Ordinal);
        Assert.Contains($"converted {village.Name}", entry.Explanation, StringComparison.Ordinal);
        Assert.Equal($"{world.Gods[1].Name} struck your followers in {village.Name}", entry.Grievances);
        Assert.StartsWith("distrusts you", DiplomacyText.Attitude(DiplomacyModel.FindRelation(world, Rival, 1)), StringComparison.Ordinal);
    }

    [Fact]
    public void Alerts_AnnounceProposalsWarAndBetrayal()
    {
        var world = _session.World;
        var name = world.Gods[1].Name;
        var feed = new AlertFeed();

        var alerts = feed.Push(
            [
                new DiplomacyChanged { Tick = 1, ActorGodId = Rival, OtherGodId = 1, Kind = DiplomaticCommandKind.OfferAlliance, Status = DiplomaticStatus.Neutral, Betrayal = false },
                new DiplomacyChanged { Tick = 2, ActorGodId = Rival, OtherGodId = 1, Kind = DiplomaticCommandKind.DeclareWar, Status = DiplomaticStatus.War, Betrayal = true },
                new DiplomacyChanged { Tick = 3, ActorGodId = 1, OtherGodId = Rival, Kind = DiplomaticCommandKind.RequestPeace, Status = DiplomaticStatus.War, Betrayal = false },
            ],
            world,
            1,
            1.0);

        Assert.Equal([$"{name} proposes an alliance (P to answer).", $"{name} betrayed your alliance and declared war!"], alerts.Select(a => a.Text));
        Assert.Equal(AlertSeverity.Danger, alerts[1].Severity);
    }

    [Fact]
    public void Hud_ShowsTheStatusWithTheRival()
    {
        SessionCommands.CommandDiplomacy(_session, Rival, DiplomaticCommandKind.DeclareWar);

        Assert.EndsWith(", at war", Assert.Single(Pantheon.Game.Hud.HudReadout.Build(_session, null, null).Rivals), StringComparison.Ordinal);
    }

    [Fact]
    public void ShippedDiplomacyRules_Parse()
    {
        Assert.Equal(DiplomacyWorlds.Rules, TestContent.Shipped.Diplomacy);
        var text = TestContent.ReadText(ContentJson.DiplomacyFileName).Replace("\"WarResistance\": 0.6", "\"WarResistance\": 1.6", StringComparison.Ordinal);
        var files = GameContentFiles.Read(n => n == ContentJson.DiplomacyFileName ? text : TestContent.ReadText(n));

        Assert.Contains("WarResistance", Assert.Throws<ContentFormatException>(() => GameContent.Parse(files)).Message, StringComparison.Ordinal);
    }
}
