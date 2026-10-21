using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Game;
using Pantheon.Game.Angel;
using Pantheon.Simulation.Gods;
using Pantheon.Tests.Minds.Agents;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Angel;

/// <summary>What moves the angel (ADR-074): its god's own deeds and what happens to its god, read from the world's events.</summary>
public sealed class AngelHooksTests
{
    private static (GameSession Session, GodState Rival, int Me, int Home) Setup()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        return (session, rival, me, GodPlanner.Home(session.World, me)!.Id);
    }

    private static List<AngelHook> Read(GameSession session, int me, params WorldEvent[] events) => AngelHooks.Read(events, session.World, session.Content, me);

    private static MiracleCast Cast(int god, string miracle, int village, int deaths = 0) => new()
    {
        Tick = 7, GodId = god, MiracleId = miracle, Target = default, Cost = 40f, AffectedSettlementIds = [village], Deaths = deaths,
    };

    [Fact]
    public void TheGodsOwnMiracles_AreKindOrHarshDeeds_ByWhetherTheyHarm()
    {
        var (session, _, me, home) = Setup();

        var kind = Assert.Single(Read(session, me, Cast(me, "food", home)));
        var harsh = Assert.Single(Read(session, me, Cast(me, "lightning", home, deaths: 2)));

        Assert.Equal(AngelMoment.DeedKind, kind.Moment);
        Assert.Contains("Food", kind.Text, System.StringComparison.Ordinal);
        Assert.Equal(home, kind.SettlementId);
        Assert.Equal(AngelMoment.DeedHarsh, harsh.Moment);
        Assert.Contains("2 dead", harsh.Text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ARivalsHarmfulMiracle_OnTheGodsVillage_StrikesIt_NamingTheCasterUnlessVeiled()
    {
        var (session, rival, me, home) = Setup();

        var struck = Assert.Single(Read(session, me, Cast(rival.Id, "lightning", home)));
        var veiled = Assert.Single(Read(session, me, Cast(rival.Id, "lightning", home) with { Veiled = true }));

        Assert.Equal(AngelMoment.VillageStruck, struck.Moment);
        Assert.Equal(rival.Id, struck.GodId);
        Assert.Contains(rival.Name, struck.Text, System.StringComparison.Ordinal);
        Assert.Null(veiled.GodId);
        Assert.Empty(Read(session, me, Cast(rival.Id, "food", home)));
    }

    [Fact]
    public void WarOffersAndMeetings_ComeFromTheOtherGods_AndTheGodsOwnWarIsAHarshDeed()
    {
        var (session, rival, me, _) = Setup();
        DiplomacyChanged Move(int actor, int other, DiplomaticCommandKind kind) => new() { Tick = 9, ActorGodId = actor, OtherGodId = other, Kind = kind, Status = DiplomaticStatus.Neutral, Betrayal = false };

        var hooks = Read(session, me, Move(rival.Id, me, DiplomaticCommandKind.DeclareWar), Move(rival.Id, me, DiplomaticCommandKind.OfferAlliance), Move(me, rival.Id, DiplomaticCommandKind.DeclareWar),
            Move(me, rival.Id, DiplomaticCommandKind.RequestPeace), new GodsMet { Tick = 9, GodId = rival.Id, OtherGodId = me, Way = MeetingWay.Creatures }, Move(rival.Id, rival.Id + 1, DiplomaticCommandKind.DeclareWar));

        Assert.Equal([AngelMoment.War, AngelMoment.Offer, AngelMoment.DeedHarsh, AngelMoment.DeedKind, AngelMoment.Met], hooks.Select(h => h.Moment));
        Assert.All(hooks, h => Assert.Equal(rival.Id, h.GodId));
    }

    [Fact]
    public void TheGodsVillages_HungryStruckWonAndLost_MoveIt_AndOtherGodsVillagesDoNot()
    {
        var (session, rival, me, home) = Setup();
        var theirs = GodPlanner.Home(session.World, rival.Id)!.Id;

        var hooks = Read(session, me,
            new FamineStarted { Tick = 3, SettlementId = home },
            new FamineStarted { Tick = 3, SettlementId = theirs },
            new SettlementDamaged { Tick = 3, SettlementId = home, Cause = Domain.Disasters.DisasterKind.Flood, HousesLost = 2, Deaths = 1 },
            new FollowingChanged { Tick = 3, SettlementId = home, GodId = me, Gained = false },
            new Rebellion { Tick = 3, SettlementId = home, GodId = me, People = 4 });

        Assert.Equal([AngelMoment.VillageHungry, AngelMoment.VillageStruck, AngelMoment.VillageLost, AngelMoment.VillageLost], hooks.Select(h => h.Moment));
        Assert.All(hooks, h => Assert.Equal(home, h.SettlementId));
    }

    [Fact]
    public void ARivalsCreatureInItsLands_ItsCreaturesFights_ItsTraitsAndItsRank_MoveIt()
    {
        var (session, rival, me, home) = Setup();
        var world = session.World;
        var mine = world.Creatures.Single(c => c.GodId == me);
        var theirs = world.Creatures.Single(c => c.GodId == rival.Id);

        var hooks = Read(session, me,
            new CreatureWatched { Tick = 4, GodId = me, VisitorGodId = rival.Id, CreatureId = theirs.Id, SettlementId = home, Answer = WatchAnswer.Unanswered },
            new CreatureFightEnded { Tick = 4, CreatureId = mine.Id, OtherCreatureId = theirs.Id, Outcome = FightOutcome.Spared },
            new CreatureFightEnded { Tick = 5, CreatureId = theirs.Id, OtherCreatureId = mine.Id, Outcome = FightOutcome.BrokeOff },
            new TraitEarned { Tick = 6, GodId = me, Trait = GodTrait.Merciful },
            new RankAchieved { Tick = 6, GodId = me, Rank = DivineRank.LesserGod });

        Assert.Equal([AngelMoment.Visitor, AngelMoment.CreatureWon, AngelMoment.CreatureLost, AngelMoment.Trait, AngelMoment.Rank], hooks.Select(h => h.Moment));
        Assert.Equal(theirs.Id, hooks[0].CreatureId);
        Assert.Equal("merciful", hooks[3].Detail);
        Assert.Equal("Lesser God", hooks[4].Detail);
    }
}
