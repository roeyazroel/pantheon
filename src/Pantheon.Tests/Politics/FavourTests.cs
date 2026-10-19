using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Politics;

/// <summary>Favours owed for major help, and what a god may ask of another (spec §41, ADR-025).</summary>
public sealed class FavourTests
{
    [Fact]
    public void SavingAnotherGodsFollowersInGreatNeed_MakesThatGodOweAFavour()
    {
        var world = Create(300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(village, adherence: 0.3f, faith: 0.8f);
        village.Food = 0f;
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(DiplomacyWorlds.Rules));
        var events = new List<WorldEvent>();

        Assert.True(caster.Cast(world, new CastMiracleRequest(Me, "food", village.Position), Me, events).IsAccepted);

        Assert.Equal((Me, Rival, (int?)village.Id), events.OfType<FavourOwed>().Select(f => (f.CreditorGodId, f.DebtorGodId, f.SettlementId)).Single());
        Assert.Equal(1f, FavourBook.Find(world, Me, Rival)!.Weight);
    }

    [Fact]
    public void GrantingARequest_RepaysAFavour_AndRefusingOne_IsRemembered()
    {
        var world = Create(300f);
        var authority = Authority();
        FavourBook.Owe(DiplomacyWorlds.PoliticsRules, world, Me, Rival, null);
        FavourBook.Owe(DiplomacyWorlds.PoliticsRules, world, Me, Rival, null);
        var events = new List<WorldEvent>();
        var gods = world.Gods.ToDictionary(g => g.Id);

        Ask(world, authority, Me, Rival, new RequestAsk(AskKind.DivinePower, Amount: 100f), events);
        Answer(world, authority, Rival, Me, accept: true, events);
        world.Tick += 2000;
        Ask(world, authority, Me, Rival, new RequestAsk(AskKind.DivinePower, Amount: 100f), events);
        var trust = DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, world, Me, Rival).Trust;
        Answer(world, authority, Rival, Me, accept: false, events);

        Assert.Equal((600f, 400f), (gods[Me].DivinePower, gods[Rival].DivinePower));
        Assert.Null(FavourBook.Find(world, Me, Rival));
        Assert.Equal([(true, true), (false, true)], events.OfType<RequestAnswered>().Select(a => (a.Granted, a.Owed)));
        Assert.True(DiplomacyModel.FindRelation(world, Me, Rival)!.Trust < trust);
        Assert.Contains(DiplomacyModel.FindRelation(world, Me, Rival)!.Memories, m => m.Act == DiplomaticAct.BrokePromise);
    }

    [Fact]
    public void Requests_ForWar_AndForTerritory_AreCarriedOutWhenGranted()
    {
        var world = Create(300f);
        var authority = Authority();
        var events = new List<WorldEvent>();

        Ask(world, authority, Me, Rival, new RequestAsk(AskKind.WarSupport, Fourth), events);
        Answer(world, authority, Rival, Me, accept: true, events);
        world.Tick += 2000;
        Ask(world, authority, Me, Third, new RequestAsk(AskKind.Territory), events);
        Answer(world, authority, Third, Me, accept: true, events);

        Assert.True(DiplomacyModel.AreAtWar(world, Rival, Fourth));
        Assert.True(TreatyModel.Has(world, Me, Third, TreatyKind.TerritorialAgreement));
    }

    [Fact]
    public void APowerRequest_CannotBeGranted_WithoutThePower()
    {
        var world = Create(300f);
        var authority = Authority();
        Ask(world, authority, Me, Rival, new RequestAsk(AskKind.DivinePower, Amount: 400f), []);
        world.Gods.Single(g => g.Id == Rival).DivinePower = 10f;

        Assert.Equal(DiplomacyRejection.CannotGrant, authority.Check(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.Accept), Rival));
        Assert.Equal(DiplomacyRejection.InvalidTerms, authority.Check(world, new DiplomaticCommand(Me, Third, DiplomaticCommandKind.Request, Ask: new RequestAsk(AskKind.WarSupport, Third)), Me));
    }

    [Fact]
    public void APromiseOfAssistance_IsKeptByHelpingInTime_AndBrokenOtherwise()
    {
        var world = Create(300f, 700f);
        var authority = Authority();
        var events = new List<WorldEvent>();
        Ask(world, authority, Me, Rival, new RequestAsk(AskKind.Assistance), events);
        Answer(world, authority, Rival, Me, accept: true, events);
        world.Tick += 2000;
        Ask(world, authority, Me, Third, new RequestAsk(AskKind.Assistance), events);
        Answer(world, authority, Third, Me, accept: true, events);
        new DiplomacyLedger(DiplomacyWorlds.Rules).Record(world, Me, Rival, DiplomaticAct.Helped, null);

        Step(world, 1, events);
        world.Tick += (long)(DiplomacyWorlds.PoliticsRules.AssistSeconds * 10f);
        Step(world, 1, events);

        Assert.Equal([(Rival, true), (Third, false)], events.OfType<ObligationEnded>().Select(o => (o.DebtorGodId, o.Kept)));
        Assert.Empty(world.Diplomacy.Obligations);
        Assert.Contains(DiplomacyModel.FindRelation(world, Me, Third)!.Memories, m => m.Act == DiplomaticAct.BrokePromise);
    }

    private static void Ask(Pantheon.Domain.World.WorldState world, DiplomacyAuthority authority, int asker, int asked, RequestAsk ask, List<WorldEvent> events) =>
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(asker, asked, DiplomaticCommandKind.Request, Ask: ask), asker, events));

    private static void Answer(Pantheon.Domain.World.WorldState world, DiplomacyAuthority authority, int asked, int asker, bool accept, List<WorldEvent> events) =>
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(asked, asker, accept ? DiplomaticCommandKind.Accept : DiplomaticCommandKind.Decline), asked, events));
}
