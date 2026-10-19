using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Politics;

/// <summary>The Divine Council: what summons it, its votes, its decrees and their defiance (spec §42, ADR-025).</summary>
public sealed class CouncilTests
{
    private static readonly PoliticsRules Rules = DiplomacyWorlds.PoliticsRules;

    private static readonly CouncilAuthority Council = new();

    [Fact]
    public void ABetrayal_SummonsTheCouncil_WhichCondemnsTheBetrayer_InAPublicBallot()
    {
        var world = Create(300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.6f);
        SetStatus(world, Me, Rival, DiplomaticStatus.Alliance);
        Authority().Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.BreakAlliance), Me, []);
        var events = new List<WorldEvent>();
        var trust = DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, world, Third, Me).Trust;

        Step(world, 1, events);
        var summoned = events.OfType<CouncilSummoned>().Single();
        Vote(world, Third, VoteChoice.Yes);
        Vote(world, Fourth, VoteChoice.Yes);
        Vote(world, Me, VoteChoice.No);
        Step(world, 61, events);

        Assert.Equal((CouncilMotionKind.Condemn, (int?)Me, (int?)Rival, CouncilTrigger.Betrayal), (summoned.Motion.Kind, summoned.Motion.TargetGodId, summoned.Motion.ProposerGodId, summoned.Trigger));
        var closed = events.OfType<CouncilClosed>().Single();
        Assert.Equal((true, 3, 1), (closed.Passed, closed.Yes, closed.No));
        Assert.True(CouncilModel.IsCondemned(world, Me));
        Assert.Equal(trust - Rules.CondemnTrustLoss, DiplomacyModel.FindRelation(world, Third, Me)!.Trust, 3);
        Assert.Equal(Rules.CondemnDoubt, world.Settlements[0].Beliefs.Single().Doubt, 3);
        Assert.Equal(3, DiplomacyModel.FindRelation(world, Me, Third)!.Memories.Count(m => m.Act == DiplomaticAct.VotedAgainst)
            + DiplomacyModel.FindRelation(world, Me, Fourth)!.Memories.Count(m => m.Act == DiplomaticAct.VotedAgainst)
            + DiplomacyModel.FindRelation(world, Me, Rival)!.Memories.Count(m => m.Act == DiplomaticAct.VotedAgainst));
        Assert.Null(world.Diplomacy.Council.Session);
    }

    [Fact]
    public void ASecretBallot_IsCounted_ButNotRemembered_AndTooFewVotesFail()
    {
        var world = Create(300f);
        var events = new List<WorldEvent>();
        Open(world, new CouncilMotion { Kind = CouncilMotionKind.Condemn, ProposerGodId = Rival, TargetGodId = Me, Secret = true }, events);
        Vote(world, Me, VoteChoice.No);
        Vote(world, Third, VoteChoice.No);

        Step(world, 61, events);

        Assert.False(events.OfType<CouncilClosed>().Single().Passed);
        Assert.Null(DiplomacyModel.FindRelation(world, Me, Rival));
        Assert.Empty(world.Diplomacy.Council.Decrees);
    }

    [Fact]
    public void ATabledMotion_WaitsForTheRegularSession()
    {
        var world = Create(300f);
        var events = new List<WorldEvent>();
        Assert.Equal(CouncilRejection.None, Council.Execute(world, new CouncilCommand(Third, new CouncilMotion { Kind = CouncilMotionKind.ProhibitPractice, Practice = Practice.Cataclysms, Secret = false }, null), Third));

        Step(world, 1, events);
        var waiting = world.Diplomacy.Council.Session;
        world.Tick = world.Diplomacy.Council.NextSessionTick;
        Step(world, 1, events);

        Assert.Null(waiting);
        Assert.Equal((CouncilTrigger.Tabled, (int?)Third), events.OfType<CouncilSummoned>().Select(s => (s.Trigger, s.Motion.ProposerGodId)).Single());
        Assert.Empty(world.Diplomacy.Council.Tabled);
    }

    [Fact]
    public void ADominantGod_FacesAPantheonWar_OfEveryGodThatVotesForIt()
    {
        var world = Create(300f, 600f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f, godId: Fourth);
        world.Gods.Single(g => g.Id == Fourth).DominantSinceTick = world.Tick;
        world.Gods.Single(g => g.Id == Rival).DominantSinceTick = world.Tick;
        var events = new List<WorldEvent>();

        Step(world, 1, events);
        Vote(world, Rival, VoteChoice.Yes);
        Vote(world, Third, VoteChoice.Yes);
        Step(world, 61, events);

        Assert.Equal((CouncilMotionKind.PantheonWar, (int?)Fourth), events.OfType<CouncilSummoned>().Select(s => (s.Motion.Kind, s.Motion.TargetGodId)).Single());
        Assert.True(DiplomacyModel.AreAtWar(world, Rival, Fourth));
        Assert.True(DiplomacyModel.AreAtWar(world, Third, Fourth));
        Assert.False(DiplomacyModel.AreAtWar(world, Me, Fourth));
    }

    [Fact]
    public void ADisasterInTheHistory_SummonsACoordinatedResponse_ThatHalvesTheCostOfHelpWhereItStruck()
    {
        var world = Create(300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.6f);
        village.Ruins = 3;
        world.History.Add(new HistoryRecord { Tick = world.Tick, Kind = HistoryKind.Earthquake, SettlementId = village.Id, GodId = null, OtherGodId = null, CreatureId = null, OtherCreatureId = null, Count = 12 });
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, politics: Rules);
        var before = caster.Check(world, new CastMiracleRequest(Me, "food", village.Position), Me).Cost;
        var events = new List<WorldEvent>();

        Step(world, 1, events);
        Vote(world, Third, VoteChoice.Yes);
        Vote(world, Rival, VoteChoice.Yes);
        Step(world, 61, events);

        Assert.Equal(CouncilTrigger.Disaster, events.OfType<CouncilSummoned>().Single().Trigger);
        Assert.Equal(before * Rules.CoordinatedCostFactor, caster.Check(world, new CastMiracleRequest(Me, "food", village.Position), Me).Cost, 2);
        Assert.Equal(before, caster.Check(world, new CastMiracleRequest(Me, "lightning", village.Position), Me).Cost * DivineWorlds.Miracles.Find("food")!.PowerCost / DivineWorlds.Miracles.Find("lightning")!.PowerCost, 2);
    }

    [Fact]
    public void DefyingADecree_CostsTheTrustOfEveryGod()
    {
        var world = Create(300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.6f);
        world.Diplomacy.Council.Decrees.Add(new CouncilDecree { Motion = new CouncilMotion { Kind = CouncilMotionKind.ProhibitPractice, Practice = Practice.Smiting, Secret = false }, PassedTick = world.Tick, EndsTick = world.Tick + 5000 });
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(DiplomacyWorlds.Rules), politics: Rules);
        var events = new List<WorldEvent>();

        caster.Cast(world, new CastMiracleRequest(Me, "food", village.Position), Me, events);
        caster.Cast(world, new CastMiracleRequest(Me, "lightning", village.Position), Me, events);

        Assert.Equal(Me, events.OfType<CouncilDefied>().Single().GodId);
        Assert.All(new[] { Rival, Third, Fourth }, god => Assert.Contains(DiplomacyModel.FindRelation(world, god, Me)!.Memories, m => m.Act == DiplomaticAct.DefiedCouncil));
    }

    [Fact]
    public void NeutralTerritory_StopsEveryGodsPull_AndHarmThereDefiesIt()
    {
        var world = Create(300f);
        var village = world.Settlements[0];
        var motion = new CouncilMotion { Kind = CouncilMotionKind.NeutralTerritory, SettlementId = village.Id, Secret = false };
        world.Diplomacy.Council.Decrees.Add(new CouncilDecree { Motion = motion, PassedTick = world.Tick, EndsTick = world.Tick + 5000 });

        Assert.True(CouncilModel.IsNeutral(world, village.Id));
        Assert.Same(motion, CouncilModel.Defied(world, DivineWorlds.Miracles.Find("lightning")!, village.Position, 25f, [village.Id]));
        Assert.Null(CouncilModel.Defied(world, DivineWorlds.Miracles.Find("food")!, village.Position, 25f, [village.Id]));
    }

    [Fact]
    public void TheAuthority_ChecksMotionsAndVotes()
    {
        var world = Create(300f);
        var condemnSelf = new CouncilMotion { Kind = CouncilMotionKind.Condemn, TargetGodId = Me, Secret = false };
        var protectNothing = new CouncilMotion { Kind = CouncilMotionKind.ProtectSacredLand, SiteId = 99, Secret = false };
        var condemnRival = new CouncilMotion { Kind = CouncilMotionKind.Condemn, TargetGodId = Rival, Secret = false };

        Assert.Equal(CouncilRejection.InvalidMotion, Council.Check(world, new CouncilCommand(Me, condemnSelf, null), Me));
        Assert.Equal(CouncilRejection.InvalidMotion, Council.Check(world, new CouncilCommand(Me, protectNothing, null), Me));
        Assert.Equal(CouncilRejection.NotAuthorized, Council.Check(world, new CouncilCommand(Rival, condemnRival with { TargetGodId = Third }, null), Me));
        Assert.Equal(CouncilRejection.NoSession, Council.Check(world, new CouncilCommand(Me, null, VoteChoice.Yes), Me));
        Assert.Equal(CouncilRejection.None, Council.Execute(world, new CouncilCommand(Me, condemnRival, null), Me));
        Assert.Equal(CouncilRejection.AlreadyTabled, Council.Check(world, new CouncilCommand(Me, condemnRival with { TargetGodId = Third }, null), Me));
        world.Diplomacy.Council.Decrees.Add(new CouncilDecree { Motion = condemnRival with { ProposerGodId = Third }, PassedTick = 0, EndsTick = world.Tick + 100 });
        Assert.Equal(CouncilRejection.InvalidMotion, Council.Check(world, new CouncilCommand(Third, condemnRival, null), Third));
    }

    [Fact]
    public void AnAiGod_VotesForItsOwnMotion_AgainstOneAimedAtIt_AndAsItPromised()
    {
        var world = Create(300f);
        var profile = TestContent.Shipped.GodAi.Profiles!.First(p => p.Personality == GodPersonality.Guardian);
        var session = new CouncilSession
        {
            Motion = new CouncilMotion { Kind = CouncilMotionKind.Condemn, ProposerGodId = Rival, TargetGodId = Fourth, Secret = false },
            Trigger = CouncilTrigger.Tabled, OpenedTick = world.Tick, ClosesTick = world.Tick + 600, Votes = [new CouncilVote { GodId = Me, Choice = VoteChoice.No }],
        };
        world.Diplomacy.Obligations.Add(new Obligation { DebtorGodId = Third, CreditorGodId = Me, Kind = ObligationKind.Back, SinceTick = world.Tick, DueTick = world.Tick + 5000 });
        var gods = world.Gods.ToDictionary(g => g.Id);

        Assert.Equal(VoteChoice.Yes, CouncilAdvisor.Vote(world, gods[Rival], session, profile));
        Assert.Equal(VoteChoice.No, CouncilAdvisor.Vote(world, gods[Fourth], session, profile));
        Assert.Equal(VoteChoice.No, CouncilAdvisor.Vote(world, gods[Third], session, profile));
    }

    private static void Vote(WorldState world, int godId, VoteChoice choice) =>
        Assert.Equal(CouncilRejection.None, Council.Execute(world, new CouncilCommand(godId, null, choice), godId));

    private static void Open(WorldState world, CouncilMotion motion, List<WorldEvent> events)
    {
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        new CouncilChamber(DiplomacyWorlds.Rules, ledger, new DiplomacyAuthority(DiplomacyWorlds.Rules, ledger)).Open(world, motion, CouncilTrigger.Tabled, events);
    }
}
