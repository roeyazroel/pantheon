using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.HolySites;
using Pantheon.Domain.Politics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Conflict;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Conflict;

/// <summary>Wars' goals, scores and dictated peace (spec §44, ADR-026).</summary>
public sealed class WarTests
{
    private static readonly ConflictRules Rules = DiplomacyWorlds.ConflictRules;

    [Fact]
    public void DeclaringWar_RecordsItsAim_AndAHolySiteWarWithoutASiteIsAConquest()
    {
        var world = Create(300f);
        world.HolySites.Add(Site(7, Rival));
        var authority = Authority();

        authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar, Aim: new WarAim(WarGoal.HolySite)), Me, []);
        authority.Execute(world, new DiplomaticCommand(Me, Third, DiplomaticCommandKind.DeclareWar, Aim: new WarAim(WarGoal.HolySite)), Me, []);

        Assert.Equal((WarGoal.HolySite, (int?)7, Me), Describe(WarBook.Find(world, Me, Rival)!));
        Assert.Equal((WarGoal.Conquest, (int?)null, Me), Describe(WarBook.Find(world, Third, Me)!));
    }

    [Fact]
    public void ActsInWar_Score_ConversionsMostAndStrikesLeast()
    {
        var world = Create(300f);
        var authority = Authority();
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar), Me, []);

        ledger.Record(world, Rival, Me, DiplomaticAct.Converted, null);
        ledger.Record(world, Rival, Me, DiplomaticAct.Harmed, null);
        ledger.Record(world, Me, Rival, DiplomaticAct.CreatureDefeated, null);
        ledger.Record(world, Third, Me, DiplomaticAct.Converted, null);

        var war = WarBook.Find(world, Me, Rival)!;
        Assert.Equal(Rules.ConversionScore + Rules.HarmScore, war.AggressorScore, 3);
        Assert.Equal(Rules.CreatureScore, war.DefenderScore, 3);
        Assert.Single(world.Diplomacy.Wars);
    }

    [Fact]
    public void AGoalMet_LetsTheVictorDemandSurrender_AndAConquestCedesBelievers()
    {
        var world = Create(300f, 700f);
        var village = world.Settlements[1];
        var theirs = DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.6f, godId: Rival);
        var mine = DivineWorlds.Believe(village, adherence: 0.1f, faith: 0.6f);
        var authority = Authority();
        var events = new List<WorldEvent>();
        authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar), Me, events);
        var demand = new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DemandSurrender);
        var early = authority.Check(world, demand, Me);

        WarBook.Find(world, Me, Rival)!.AggressorScore = Rules.GoalScore;
        Step(world, 1, events);
        world.Tick += 2000;
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, demand, Me, events));
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.Accept), Rival, events));

        Assert.Equal(DiplomacyRejection.WrongStatus, early);
        Assert.Equal(WarGoal.Conquest, events.OfType<WarGoalAchieved>().Single().Goal);
        var peace = events.OfType<PeaceDictated>().Single();
        Assert.Equal((Me, Rival, (int?)village.Id, 40), (peace.WinnerGodId, peace.LoserGodId, peace.SettlementId, peace.People));
        Assert.Equal((0.4f, 0.5f), (theirs.Adherence, mine.Adherence));
        Assert.Equal(DiplomaticStatus.Neutral, DiplomacyModel.StatusOf(world, Me, Rival));
        Assert.Empty(world.Diplomacy.Wars);
        Assert.Contains(DiplomacyModel.FindRelation(world, Rival, Me)!.Memories, m => m.Act == DiplomaticAct.Dictated);
    }

    [Theory]
    [InlineData(WarGoal.HolySite)]
    [InlineData(WarGoal.Tribute)]
    [InlineData(WarGoal.Humiliation)]
    public void EachGoal_HasItsTerms(WarGoal goal)
    {
        var world = Create(300f);
        world.HolySites.Add(Site(3, Rival));
        var authority = Authority();
        authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar, Aim: new WarAim(goal)), Me, []);
        var war = WarBook.Find(world, Me, Rival)!;
        war.AggressorScore = Rules.GoalScore;
        world.HolySites[0].KeeperGodId = goal == WarGoal.HolySite ? Me : Rival;
        if (goal == WarGoal.Humiliation)
        {
            new DiplomacyLedger(DiplomacyWorlds.Rules).Record(world, Rival, Me, DiplomaticAct.CreatureDefeated, null);
        }

        world.Tick += 2000;
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DemandSurrender), Me, []));
        authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.Accept), Rival, []);

        switch (goal)
        {
            case WarGoal.HolySite:
                Assert.Equal(Me, world.HolySites[0].KeeperGodId);
                break;
            case WarGoal.Tribute:
                Assert.Equal((Rival, Rules.DictatedTribute), world.Diplomacy.Treaties.Select(t => (t.PayerGodId!.Value, t.Amount)).Single());
                break;
            case WarGoal.Humiliation:
                Assert.Equal(TreatyKind.TerritorialAgreement, world.Diplomacy.Treaties.Single().Kind);
                break;
        }
    }

    [Fact]
    public void ADefenderThatRepelsTheAttack_MayExactTribute()
    {
        var world = Create(300f);
        var authority = Authority();
        authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.DeclareWar), Rival, []);
        WarBook.Find(world, Me, Rival)!.DefenderScore = Rules.GoalScore;
        world.Tick += 2000;

        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DemandSurrender), Me, []));
        authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.Accept), Rival, []);

        Assert.Equal(Rival, world.Diplomacy.Treaties.Single().PayerGodId);
    }

    [Fact]
    public void AnAiGod_ChoosesItsAim_AndYieldsWhenBeaten()
    {
        var world = Create(300f);
        world.HolySites.Add(Site(4, Rival));
        var profile = TestContent.Shipped.GodAi.Profiles!.First(p => p.Personality == GodPersonality.Tyrant);
        var me = world.Gods.Single(g => g.Id == Me);

        Assert.Equal(new WarAim(WarGoal.HolySite, 4), WarAims.For(world, me, Rival, profile with { HolySites = 0.9f }));
        Assert.Equal(WarGoal.Conquest, WarAims.For(world, me, Third, profile with { HolySites = 0f }).Goal);
        Authority().Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar), Me, []);
        var view = DiplomacyModel.NewRelation(DiplomacyWorlds.Rules, Rival, Me);
        var even = WarAims.SurrenderUtility(Rules, world, Rival, Me, view, profile);
        WarBook.Find(world, Me, Rival)!.AggressorScore = 2f * Rules.GoalScore;
        Assert.True(WarAims.SurrenderUtility(Rules, world, Rival, Me, view, profile) > even + 1.5f);
    }

    private static (WarGoal, int?, int) Describe(WarRecord war) => (war.Goal, war.SiteId, war.AggressorGodId);

    private static HolySite Site(int id, int? keeper) =>
        new() { Id = id, Kind = HolySiteKind.SacredPeak, Name = "The Peak", RegionId = 0, Position = new GroundPosition(300f, 512f), KeeperGodId = keeper, ClaimantGodId = null, ClaimTicks = 0 };
}
