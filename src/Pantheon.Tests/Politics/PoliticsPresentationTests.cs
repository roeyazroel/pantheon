using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Diplomacy;
using Pantheon.Game.History;
using Pantheon.Game.Launch;
using Pantheon.Game.Politics;
using Pantheon.Simulation.History;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Politics;

/// <summary>How treaties, favours and the council are shown and driven (ADR-025): words, panels, alerts, history and launch options.</summary>
public sealed class PoliticsPresentationTests
{
    [Fact]
    public void Treaties_TermsRequestsAndMotions_ReadAsSentences()
    {
        var world = Create(300f);
        var pact = Sign(world, Authority(), Rival, Me, Terms(TreatyKind.DefensivePact) with { TargetGodId = Third, Secret = true });

        Assert.Equal("Secret defensive pact: Rivalis defends you against Tertia (5:00 left)", PoliticsText.Describe(pact, world, Me));
        Assert.Equal("a tribute: you pay 20 a minute for 10 minutes", PoliticsText.Terms(Terms(TreatyKind.Tribute) with { PayerGodId = Me, Amount = 20f, DurationSeconds = 600f }, world, Me));
        Assert.Equal("war on Quartus", PoliticsText.Ask(new RequestAsk(AskKind.WarSupport, Fourth), world));
        Assert.Equal("Make Town1 neutral territory", PoliticsText.Motion(new CouncilMotion { Kind = CouncilMotionKind.NeutralTerritory, SettlementId = world.Settlements[0].Id, Secret = false }, world));
        Assert.Equal("Cataclysms are prohibited", PoliticsText.Decree(new CouncilMotion { Kind = CouncilMotionKind.ProhibitPractice, Practice = Practice.Cataclysms, Secret = false }, world));
    }

    [Fact]
    public void TheDiplomacyPanel_ShowsTreatiesFavoursAndProposals_AndOffersWhatTheAuthorityAllows()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var world = session.World;
        var me = session.LocalGodId!.Value;
        var others = world.Gods.Where(g => g.Id != me).Select(g => g.Id).ToList();
        world.Tick = 20_000;
        session.Command((a, events) => a.Diplomacy.Execute(world, new DiplomaticCommand(me, others[0], DiplomaticCommandKind.ProposeTreaty, Terms: Terms(TreatyKind.NonAggression)), me, events));
        session.Command((a, events) => a.Diplomacy.Execute(world, new DiplomaticCommand(others[0], me, DiplomaticCommandKind.Accept), null, events));
        FavourBook.Owe(DiplomacyWorlds.PoliticsRules, world, me, others[1], null);
        session.Command((a, events) => a.Diplomacy.Execute(world, new DiplomaticCommand(others[2], me, DiplomaticCommandKind.ProposeTreaty, Terms: Terms(TreatyKind.MigrationAgreement)), others[2], events));

        var entries = DiplomacyReadout.Build(session).ToDictionary(e => e.GodId);

        var first = entries[others[0]].Politics;
        Assert.StartsWith("Non-aggression pact: you and ", first.Treaties.Single().Text, StringComparison.Ordinal);
        Assert.Equal(DiplomaticCommandKind.BreakTreaty, first.Treaties.Single().Break.Diplomacy!.Kind);
        Assert.DoesNotContain(first.Offers, o => o.Label == "Non-aggression pact");
        Assert.StartsWith(world.Gods.Single(g => g.Id == others[1]).Name + " owes you a favour", entries[others[1]].Politics.Favours, StringComparison.Ordinal);
        Assert.Contains(entries[others[1]].Politics.Requests, r => r.Label == "Ask 100 power");
        Assert.Equal("It proposes a migration agreement for 5 minutes.", entries[others[2]].Politics.Proposal);
        Assert.Contains(entries[others[1]].Politics.Offers, o => o.Label.StartsWith("Joint war on ", StringComparison.Ordinal));
        Assert.Empty(entries[others[2]].Politics.Offers);
    }

    [Fact]
    public void TheCouncilPanel_ShowsTheSession_TheBallot_AndTheMotionsThePlayerMayTable()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var before = CouncilReadout.Build(session, secretBallot: false, focusSettlementId: session.World.Settlements[0].Id);

        var summoned = PoliticsCommands.Summon(session);
        var during = CouncilReadout.Build(session, secretBallot: true, focusSettlementId: null);
        var voted = PoliticsCommands.Run(session, during.Ballot.First(b => b.Label == "Vote against"));
        var after = CouncilReadout.Build(session, secretBallot: true, focusSettlementId: null);

        Assert.Equal("The council is not sitting.", before.Note);
        Assert.Empty(before.Ballot);
        Assert.Contains(before.Motions, m => m.Label.StartsWith("Condemn ", StringComparison.Ordinal));
        Assert.Contains(before.Motions, m => m.Label.EndsWith(" neutral territory", StringComparison.Ordinal));
        Assert.StartsWith("The council meets: Condemn ", summoned, StringComparison.Ordinal);
        Assert.StartsWith("In session in regular session: Condemn ", during.Session, StringComparison.Ordinal);
        Assert.Equal("You vote against", voted);
        Assert.Equal("You vote against.", after.Note);
        Assert.DoesNotContain(after.Ballot, b => b.Label == "Vote against");
    }

    [Fact]
    public void Alerts_TellThePlayerOfTreatiesFavoursAndTheCouncil_AndKeepOthersSecrets()
    {
        var world = Create(300f);
        var motion = new CouncilMotion { Kind = CouncilMotionKind.Condemn, TargetGodId = Me, ProposerGodId = Rival, Secret = false };
        WorldEvent[] events =
        [
            new TreatySigned { Tick = 1, TreatyId = 1, Kind = TreatyKind.NonAggression, FirstGodId = Rival, SecondGodId = Me, TargetGodId = null, Secret = false },
            new TreatySigned { Tick = 1, TreatyId = 2, Kind = TreatyKind.JointWar, FirstGodId = Rival, SecondGodId = Third, TargetGodId = Me, Secret = true },
            new TreatyInvoked { Tick = 1, TreatyId = 3, DefenderGodId = Third, ProtectedGodId = Fourth, AggressorGodId = Me, Betrayal = false },
            new FavourOwed { Tick = 1, CreditorGodId = Me, DebtorGodId = Rival, SettlementId = world.Settlements[0].Id },
            new CouncilSummoned { Tick = 1, Motion = motion, Trigger = CouncilTrigger.Betrayal },
            new CouncilClosed { Tick = 1, Motion = motion, Passed = true, Yes = 3, No = 1 },
            new AllyAbsorbed { Tick = 1, GodId = Fourth, AllyGodId = Me, People = 40 },
        ];

        var raised = new AlertFeed().Push(events, world, Me, 10.0);

        Assert.Equal(
            [
                "You and Rivalis signed a non-aggression pact.",
                "Tertia honours its pact with Quartus and declares war on you!",
                "Rivalis owes you a favour: you saved its people in Town1.",
                "The Divine Council meets after a betrayal: Condemn Testa. Vote in the council (U).",
                "The council passes \"Condemn Testa\": 3 for, 1 against.",
                "Quartus broke your alliance and took 40 of your followers!",
            ],
            raised.Select(a => a.Text));
    }

    [Fact]
    public void TreatiesRevelationsDecreesAndAbsorptions_EnterTheHistory()
    {
        var world = Create(300f);
        var recorder = new HistoryRecorder(TestContent.Shipped.History);

        recorder.Record(
            world,
            [
                new TreatySigned { Tick = 0, TreatyId = 1, Kind = TreatyKind.MutualDefense, FirstGodId = Me, SecondGodId = Rival, TargetGodId = null, Secret = false },
                new TreatySigned { Tick = 0, TreatyId = 2, Kind = TreatyKind.SharedInfluence, FirstGodId = Me, SecondGodId = Rival, TargetGodId = null, Secret = false },
                new TreatyRevealed { Tick = 0, TreatyId = 3, Kind = TreatyKind.JointWar, FirstGodId = Third, SecondGodId = Fourth, TargetGodId = Me },
                new CouncilClosed { Tick = 0, Motion = new CouncilMotion { Kind = CouncilMotionKind.ProhibitPractice, Practice = Practice.Terror, Secret = false }, Passed = true, Yes = 3, No = 0 },
                new AllyAbsorbed { Tick = 0, GodId = Rival, AllyGodId = Third, People = 25 },
            ],
            0);

        Assert.Equal([HistoryKind.Treaty, HistoryKind.Revelation, HistoryKind.Decree, HistoryKind.Absorption], world.History.Select(r => r.Kind));
        Assert.Equal(
            ["Testa and Rivalis sign a mutual defence pact", "The secret joint war of Tertia and Quartus comes to light", "The Council prohibits terror", "Rivalis absorbs the followers of Tertia"],
            world.History.Select(r => HistoryText.Title(r, world)));
        Assert.Equal("25 turned", HistoryText.Detail(world.History[3], world));
    }

    [Fact]
    public void LaunchOptions_SignATreaty_AndOpenOrSummonTheCouncil()
    {
        var options = LaunchOptions.Parse(["--treaty=jointwar", "--council-panel=on", "--summon-council=on", "--treaty=peace"]);
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));

        var signed = PoliticsCommands.SignTreaty(session, TreatyKind.MutualDefense);

        Assert.Equal(TreatyKind.JointWar, options.Treaty);
        Assert.True(options.CouncilPanel && options.SummonCouncil);
        Assert.Single(options.Warnings);
        Assert.EndsWith(" signed a mutual defence pact", signed, StringComparison.Ordinal);
        Assert.Single(session.World.Diplomacy.Treaties);
    }
}
