using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.History;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.History;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Gods;

/// <summary>How gods come to know each other (spec §11's KnownGods, ADR-036).</summary>
public sealed class AcquaintanceTests
{
    private static readonly AcquaintanceRules Meeting = DiplomacyWorlds.Acquaintance;
    private static readonly DiplomacyRules Rules = DiplomacyWorlds.Rules with { Acquaintance = Meeting };

    [Fact]
    public void NeighbouringVillages_BringTheirGodsToMeet_ButFarOnesDoNot()
    {
        // Villages 250 m apart in a row: each god meets the next, not the one beyond.
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var events = new List<WorldEvent>();

        Acquaintance.Step(Meeting with { NeighbourMetres = 300f }, world, events);

        Assert.Equal([2], world.Gods[0].KnownGods);
        Assert.Equal([1, 3], world.Gods[1].KnownGods);
        Assert.Equal([3], world.Gods[3].KnownGods);
        var met = events.OfType<GodsMet>().First();
        Assert.Equal((1, 2, MeetingWay.Neighbours, world.Settlements[1].Id), (met.GodId, met.OtherGodId, met.Way, met.SettlementId));
        Assert.Equal(3, events.Count);
        Acquaintance.Step(Meeting with { NeighbourMetres = 300f }, world, events);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void ASharedVillage_ARoad_AndTheirCreatures_BringGodsToMeet()
    {
        var alone = Meeting with { NeighbourMetres = 0f };
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        DivineWorlds.Believe(world.Settlements[0], adherence: Meeting.SharedAdherence, faith: 0.5f, godId: 3);
        world.Roads.Add(new RoadState { Id = 1, FromSettlementId = world.Settlements[1].Id, ToSettlementId = world.Settlements[3].Id, Length = 500f, Built = 500f });
        var events = new List<WorldEvent>();

        Acquaintance.Step(alone, world, events);

        Assert.Equal([MeetingWay.Road, MeetingWay.SharedVillage], events.OfType<GodsMet>().Select(m => m.Way));
        Assert.Equal([3], world.Gods[0].KnownGods);
        Assert.Equal([4], world.Gods[1].KnownGods);

        var (bonded, mine, theirs) = CreatureBondWorlds.Create();
        CreatureBonds.Ensure(CreatureBondWorlds.Bonds, mine, theirs.Id);
        events.Clear();
        Acquaintance.Step(alone, bonded, events);
        Assert.Equal(MeetingWay.Creatures, Assert.Single(events.OfType<GodsMet>()).Way);
    }

    [Fact]
    public void DeedsAndTheDivineCouncil_BringGodsToMeet()
    {
        var alone = Meeting with { NeighbourMetres = 0f };
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        new DiplomacyLedger(Rules).Record(world, 2, 4, DiplomaticAct.Harmed, world.Settlements[3].Id);
        var events = new List<WorldEvent>();

        Acquaintance.Step(alone, world, events);
        Assert.Equal(MeetingWay.Deeds, Assert.Single(events.OfType<GodsMet>()).Way);

        world.Diplomacy.Council.Session = new Pantheon.Domain.Politics.CouncilSession
        {
            Motion = new Pantheon.Domain.Politics.CouncilMotion { Kind = Pantheon.Domain.Politics.CouncilMotionKind.ProhibitPractice, Practice = Pantheon.Domain.Politics.Practice.Terror, ProposerGodId = 2, Secret = false },
            Trigger = Pantheon.Domain.Politics.CouncilTrigger.Tabled,
            OpenedTick = 0,
            ClosesTick = 600,
            Votes = [],
        };
        Acquaintance.Step(alone, world, events);

        Assert.All(world.Gods, god => Assert.Equal(3, god.KnownGods.Count));
        Assert.Equal(5, events.OfType<GodsMet>().Count(m => m.Way == MeetingWay.Council));
    }

    [Fact]
    public void UntilTheyMeet_GodsCannotAllyWarSignOrAsk_WhereTheRulesSaySo()
    {
        var world = DiplomacyWorlds.Create(200f, 450f);
        world.Tick = 10_000;
        var authority = new DiplomacyAuthority(Rules, new DiplomacyLedger(Rules));
        var terms = PoliticsWorlds.Terms(Pantheon.Domain.Politics.TreatyKind.NonAggression);

        Assert.Equal(DiplomacyRejection.NotMet, authority.Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.OfferAlliance), 1));
        Assert.Equal(DiplomacyRejection.NotMet, authority.Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.DeclareWar), 1));
        Assert.Equal(DiplomacyRejection.NotMet, authority.Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.ProposeTreaty, Terms: terms), 1));
        Assert.Equal(DiplomacyRejection.NoProposal, authority.Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.Accept), 1));
        Assert.Equal(DiplomacyRejection.None, DiplomacyWorlds.Authority().Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.OfferAlliance), 1));
        Assert.True(Acquaintance.Knows(AcquaintanceRules.None, world, 1, 2));

        Acquaintance.Introduce(world, 1, 2, MeetingWay.Neighbours, null, []);

        Assert.True(Acquaintance.Knows(Meeting, world, 2, 1));
        Assert.Equal(DiplomacyRejection.None, authority.Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.OfferAlliance), 1));
        Assert.Equal(DiplomacyRejection.None, authority.Check(world, new DiplomaticCommand(1, 2, DiplomaticCommandKind.DeclareWar), 1));
    }

    [Fact]
    public void AnAiGod_WeighsDealingsOnlyWithTheGodsItKnows()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var diplomat = new Pantheon.Simulation.Gods.GodDiplomat(Rules, DivineWorlds.Rules, new DiplomacyAuthority(Rules, new DiplomacyLedger(Rules)));
        var tyrant = world.Gods[1];

        Assert.Empty(diplomat.Evaluate(world, tyrant, PantheonWorlds.Profile(GodPersonality.Tyrant)));
        Acquaintance.Introduce(world, 2, 3, MeetingWay.Neighbours, null, []);

        Assert.All(diplomat.Evaluate(world, tyrant, PantheonWorlds.Profile(GodPersonality.Tyrant)), c => Assert.Equal(3, c.OtherGodId));
    }

    [Fact]
    public void TheDiplomacyStep_IntroducesNeighbours_AndTheHistoryRemembersAllButTheCouncilsMeetings()
    {
        var world = PantheonWorlds.Create(0.6f, 0.6f, 0.6f, 0.6f);
        var events = new List<WorldEvent>();
        new DiplomacySystem(Rules).Update(new TickContext(world, new SimulationRandom(1), events));
        var recorder = new HistoryRecorder(TestContent.Shipped.History);
        events.Add(new GodsMet { Tick = 0, GodId = 1, OtherGodId = 4, Way = MeetingWay.Council });

        recorder.Record(world, events, 0);

        // Within 500 m, each village's god meets the next two along the row; the ends, 750 m apart, do not.
        Assert.Equal(5, events.Count(e => e is GodsMet { Way: MeetingWay.Neighbours }));
        Assert.Equal(5, world.History.Count(r => r.Kind == HistoryKind.GodsMet));
        Assert.DoesNotContain(4, world.Gods[0].KnownGods);
        Assert.Equal((1, 2, (int)MeetingWay.Neighbours), (world.History[0].GodId!.Value, world.History[0].OtherGodId!.Value, world.History[0].Count));
    }
}
