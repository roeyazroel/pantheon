using System;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Conflict;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Multiplayer;
using Pantheon.Game.Society;
using Pantheon.Simulation.Conflict;
using Pantheon.Simulation.History;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Conflict;

/// <summary>How wars, covert acts and holy wars are shown and driven (ADR-026).</summary>
public sealed class ConflictPresentationTests
{
    [Fact]
    public void TheDiplomacyPanel_ShowsTheWarIntelligenceAndWarAims()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = Meetings.All(session.World);
        world.Tick = 20_000;
        var me = session.LocalGodId!.Value;
        var rival = world.Gods.First(g => g.Id != me);
        var peaceful = DiplomacyReadout.Build(session).Single(e => e.GodId == rival.Id);

        var declared = ConflictCommands.DeclareWar(session, WarGoal.Tribute);
        var target = world.Diplomacy.Wars.Single().DefenderGodId;
        var atWar = DiplomacyReadout.Build(session).Single(e => e.GodId == target);

        Assert.Equal(string.Empty, peaceful.War);
        Assert.StartsWith("You are ", peaceful.Intelligence, StringComparison.Ordinal);
        Assert.Contains(peaceful.Politics.Aims, a => a.Label == "War for tribute");
        Assert.StartsWith("You declare war on ", declared, StringComparison.Ordinal);
        Assert.StartsWith("Your war on ", atWar.War, StringComparison.Ordinal);
        Assert.Contains(" for tribute: 0 to 0 (10 to dictate peace)", atWar.War, StringComparison.Ordinal);
        Assert.Empty(atWar.Politics.Aims);
    }

    [Fact]
    public void TheSettlementsPanel_ShowsAHolyWar_AndOffersTheWordAndProxyActs()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var me = session.LocalGodId!.Value;
        var home = world.Settlements.First(s => Pantheon.Simulation.Politics.TreatyModel.Leader(s) == me);
        var foreign = world.Settlements.First(s => Pantheon.Simulation.Politics.TreatyModel.Leader(s) is { } leader && leader != me);
        Believe(foreign, me);

        var status = ConflictCommands.StartHolyWar(session, home.Id);
        var model = SettlementPanelReadout.Build(session, home.Id)!;
        var abroad = SettlementPanelReadout.Build(session, foreign.Id)!;

        Assert.EndsWith(" goes to holy war on " + world.Settlements.Single(s => s.Id == home.Society.HolyWar!.DefenderSettlementId).Name, status, StringComparison.Ordinal);
        Assert.Contains(model.Lines, l => l.StartsWith("Wages holy war on ", StringComparison.Ordinal));
        Assert.Equal(["Encourage the holy war", "Discourage the holy war"], model.Actions.Select(a => a.Label));
        Assert.Contains(abroad.Actions, a => a.Proxy?.Kind == ProxyKind.Propaganda);
        Assert.Equal("You urge your people on", Pantheon.Game.Politics.PoliticsCommands.Run(session, model.Actions[0]));
    }

    [Fact]
    public void Alerts_TellOfWarsWonExposuresAndHolyWars_AndHideAVeiledCaster()
    {
        var world = Create(300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f);
        WorldEvent[] events =
        [
            new WarGoalAchieved { Tick = 1, GodId = Me, EnemyGodId = Rival, Goal = WarGoal.Conquest },
            new PeaceDictated { Tick = 1, WinnerGodId = Third, LoserGodId = Me, Goal = WarGoal.Tribute, SettlementId = null, SiteId = null, People = 0 },
            new CovertActExposed { Tick = 1, GodId = Fourth, VictimGodId = Me, Act = "Propaganda", SettlementId = world.Settlements[0].Id },
            new HolyWarDeclared { Tick = 1, SettlementId = world.Settlements[1].Id, DefenderSettlementId = world.Settlements[0].Id, GodId = Rival, EnemyGodId = Me },
            new MiracleCast { Tick = 1, GodId = Fourth, MiracleId = "lightning", Target = world.Settlements[0].Position, Cost = 70f, AffectedSettlementIds = [world.Settlements[0].Id], Deaths = 2, Veiled = true },
        ];

        var raised = new AlertFeed().Push(events, world, Me, 10.0);

        Assert.Equal(
            [
                "You have won your war on Rivalis: demand its surrender (P).",
                "You accepted Tertia's terms: the war is over.",
                "Quartus was behind the propaganda in Town1!",
                "Town2, for Rivalis, wages holy war on your people in Town1!",
                "An unknown god worked a miracle (lightning) on Town1: 2 died.",
            ],
            raised.Select(a => a.Text));
    }

    [Fact]
    public void HolyWarsDictatedPeacesAndExposures_EnterTheHistory()
    {
        var world = Create(300f, 700f);

        new HistoryRecorder(TestContent.Shipped.History).Record(
            world,
            [
                new HolyWarEnded { Tick = 0, SettlementId = 1, DefenderSettlementId = 2, GodId = Rival, Killed = 30, Converted = 12, Won = true },
                new PeaceDictated { Tick = 0, WinnerGodId = Rival, LoserGodId = Third, Goal = WarGoal.Conquest, SettlementId = 2, SiteId = null, People = 40 },
                new CovertActExposed { Tick = 0, GodId = Fourth, VictimGodId = Me, Act = "Incite", SettlementId = 1 },
            ],
            0);

        Assert.Equal([HistoryKind.HolyWar, HistoryKind.DictatedPeace, HistoryKind.Exposure], world.History.Select(r => r.Kind));
        Assert.Equal(["The Holy War against Town2, for Rivalis", "Rivalis dictates peace to Tertia", "The Plot of Quartus against Testa is uncovered"], world.History.Select(r => HistoryText.Title(r, world)));
        Assert.Equal("30 killed", HistoryText.Detail(world.History[0], world));
    }

    [Fact]
    public void Secrets_ProxyActsAndVeiledCasters_AreKeptFromOtherPlayers()
    {
        var proxy = new ProxyActed { Tick = 1, GodId = 3, SettlementId = 1, TargetGodId = 2, Kind = ProxyKind.Incite };
        var veiled = new MiracleCast { Tick = 1, GodId = 3, MiracleId = "fire", Target = new GroundPosition(0f, 0f), Cost = 1f, AffectedSettlementIds = [], Deaths = 0, Veiled = true };

        Assert.True(SecretFilter.HasSecrets([proxy, veiled]));
        Assert.Equal(0, ((MiracleCast)SecretFilter.Events([proxy, veiled], 2).Single()).GodId);
        Assert.Equal(2, SecretFilter.Events([proxy, veiled], 3).Count);
    }

    /// <summary>Gives a god a fifth of a village's people, taken from the godless share or the leading god.</summary>
    internal static void Believe(Pantheon.Domain.Settlements.SettlementState settlement, int godId)
    {
        var leading = settlement.Beliefs.OrderByDescending(b => b.Adherence).First();
        leading.Adherence -= 0.2f;
        if (settlement.Beliefs.Find(b => b.GodId == godId) is { } mine)
        {
            mine.Adherence += 0.2f;
        }
        else
        {
            settlement.Beliefs.Add(new Pantheon.Domain.Belief.GodBelief { GodId = godId, Adherence = 0.2f, Faith = 0.6f, Love = 0.2f, Fear = 0f, Doubt = 0f });
        }
    }

    [Fact]
    public void LaunchOptions_DeclareWar_StartAHolyWar_AndVeil()
    {
        var options = LaunchOptions.Parse(["--war-goal=holysite", "--holy-war=on", "--veil=on", "--war-goal=glory"]);

        Assert.Equal(WarGoal.HolySite, options.WarGoal);
        Assert.True(options.HolyWar && options.Veil);
        Assert.Single(options.Warnings);
    }
}
