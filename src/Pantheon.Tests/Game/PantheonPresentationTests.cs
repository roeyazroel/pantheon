using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Divine;
using Pantheon.Game.Hud;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Four gods as the player sees them (ADR-015): names, alliances, joint dominance and joint victory.</summary>
public sealed class PantheonPresentationTests
{
    [Fact]
    public void Names_PutThePlayerFirst_AndJoinTheRest()
    {
        var world = PantheonWorlds.Create();

        Assert.Equal("You", DivineText.Names(world, [1], 1));
        Assert.Equal("You and Tertia", DivineText.Names(world, [3, 1], 1));
        Assert.Equal("Rivalis, Tertia and Quarta", DivineText.Names(world, [2, 3, 4], 1));
        Assert.Equal("Rivalis", DivineText.Names(world, [2], null));
    }

    [Fact]
    public void Alerts_ReportAJointDominanceOnce_AndAJointVictory()
    {
        var world = PantheonWorlds.Create();

        var theirs = new AlertFeed().Push(
            [
                new DominanceChanged { Tick = 1, GodId = 2, Started = true, Allies = [3] },
                new DominanceChanged { Tick = 1, GodId = 3, Started = true, Allies = [2] },
                new VictoryAchieved { Tick = 2, GodIds = [2, 3] },
            ],
            world,
            1,
            1.0);
        var ours = new AlertFeed().Push(
            [
                new DominanceChanged { Tick = 1, GodId = 1, Started = true, Allies = [3] },
                new DominanceChanged { Tick = 1, GodId = 3, Started = true, Allies = [1] },
                new VictoryAchieved { Tick = 2, GodIds = [1, 3] },
            ],
            world,
            1,
            1.0);

        Assert.Equal(
            [
                ("Rivalis and Tertia hold most of the world's belief together: they win together in ten minutes unless you stop them.", AlertSeverity.Danger),
                ("Rivalis and Tertia have won the world's belief.", AlertSeverity.Danger),
            ],
            theirs.Select(a => (a.Text, a.Severity)));
        Assert.Equal(
            [
                ("You and Tertia hold most of the world's belief together: keep it for ten minutes to win together.", AlertSeverity.Good),
                ("You and Tertia have won together: your followers held the world's belief for ten minutes.", AlertSeverity.Good),
            ],
            ours.Select(a => (a.Text, a.Severity)));
    }

    [Fact]
    public void Alerts_ReportAlliancesBetweenOtherGods_ButNotTheirPeace()
    {
        var world = PantheonWorlds.Create();
        static DiplomacyChanged Accepted(DiplomaticStatus status) => new() { Tick = 1, ActorGodId = 3, OtherGodId = 2, Kind = DiplomaticCommandKind.Accept, Status = status, Betrayal = false };

        var alerts = new AlertFeed().Push([Accepted(DiplomaticStatus.Alliance), Accepted(DiplomaticStatus.Neutral)], world, 1, 1.0);

        var alert = Assert.Single(alerts);
        Assert.Equal(("Tertia and Rivalis are now allies.", AlertSeverity.Info), (alert.Text, alert.Severity));
    }

    [Fact]
    public void Hud_ShowsTheBlocsShare_ItsDominance_AndItsVictory()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var names = world.Gods.Select(g => g.Name).ToList();
        PantheonWorlds.SetStatus(world, 1, 3, DiplomaticStatus.Alliance);

        Assert.Matches($"^You and {names[2]} hold \\d+% of all belief together; 60% for 10:00 wins together$", HudReadout.Build(session, null, null).Contest);

        PantheonWorlds.SetStatus(world, 2, 4, DiplomaticStatus.Alliance);
        world.Gods[1].DominantSinceTick = world.Tick;
        var dominated = HudReadout.Build(session, null, null);
        Assert.Matches($"^{names[1]} and {names[3]} hold \\d+% of all belief together: they win together in 10:00$", dominated.Contest);
        Assert.False(dominated.ContestFavoursPlayer);

        world.WinnerGodIds.AddRange([1, 3]);
        var won = HudReadout.Build(session, null, null);
        Assert.Equal($"You and {names[2]} have won together. The world goes on.", won.Contest);
        Assert.True(won.ContestFavoursPlayer);
    }

    [Fact]
    public void Panel_ShowsEachGodsAlliancesAndWars_WithOtherGods()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var names = world.Gods.Select(g => g.Name).ToList();
        PantheonWorlds.SetStatus(world, 2, 3, DiplomaticStatus.Alliance);
        PantheonWorlds.SetStatus(world, 2, 4, DiplomaticStatus.War);
        PantheonWorlds.SetStatus(world, 2, 1, DiplomaticStatus.War);

        var entries = Pantheon.Game.Diplomacy.DiplomacyReadout.Build(session).ToDictionary(e => e.GodId);

        Assert.Equal($"Allied with {names[2]}; at war with {names[3]}", entries[2].Ties);
        Assert.Equal($"Allied with {names[1]}", entries[3].Ties);
        Assert.Equal($"At war with {names[1]}", entries[4].Ties);
        Assert.Equal(string.Empty, Pantheon.Game.Diplomacy.DiplomacyText.Ties(PantheonWorlds.Create(), 2, 1));
    }

    [Fact]
    public void Panel_ExplainsAWrongedAlly()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var village = world.Settlements[0];
        PantheonWorlds.SetStatus(world, 2, 3, DiplomaticStatus.Alliance);

        new Pantheon.Simulation.Diplomacy.DiplomacyLedger(DiplomacyWorlds.Rules).Record(world, 2, 1, DiplomaticAct.Harmed, village.Id);

        new Pantheon.Simulation.Diplomacy.DiplomacyLedger(DiplomacyWorlds.Rules).Record(world, 3, 2, DiplomaticAct.Harmed, village.Id);
        PantheonWorlds.SetStatus(world, 1, 3, DiplomaticStatus.Alliance);
        new Pantheon.Simulation.Diplomacy.DiplomacyLedger(DiplomacyWorlds.Rules).Record(world, 3, 4, DiplomaticAct.Harmed, village.Id);

        var entries = Pantheon.Game.Diplomacy.DiplomacyReadout.Build(session);
        Assert.Contains($"wronged its ally in {village.Name}", entries.Single(e => e.GodId == 3).Explanation, System.StringComparison.Ordinal);
        Assert.Contains($"wronged your ally in {village.Name}", entries.Single(e => e.GodId == 4).Grievances, System.StringComparison.Ordinal);
    }
}
