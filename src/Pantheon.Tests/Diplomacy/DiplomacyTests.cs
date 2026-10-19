using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Diplomacy;

/// <summary>Relations, status and betrayal memory between gods (spec §37–41, §113).</summary>
public sealed class DiplomacyTests
{
    private const int Me = 1;
    private const int Rival = RivalWorlds.RivalId;

    [Fact]
    public void Acts_ChangeHowAGodSeesTheActor_AndAreRemembered()
    {
        var world = DiplomacyWorlds.Create(200f);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);

        ledger.Record(world, Rival, Me, DiplomaticAct.Harmed, 1);
        ledger.Record(world, Rival, Me, DiplomaticAct.Helped, 1);

        var view = DiplomacyModel.FindRelation(world, Rival, Me)!;
        Assert.Equal(0.5f - DiplomacyWorlds.Rules.HarmTrustLoss, view.Trust, 4);
        Assert.Equal(0.2f + DiplomacyWorlds.Rules.HarmRivalry, view.Rivalry, 4);
        Assert.Equal(DiplomacyWorlds.Rules.HelpGratitude, view.Gratitude, 4);
        Assert.Equal([DiplomaticAct.Harmed, DiplomaticAct.Helped], view.Memories.Select(m => m.Act));
        Assert.Null(DiplomacyModel.FindRelation(world, Me, Rival));
    }

    [Fact]
    public void Memories_BeyondCapacity_LoseTheWeakest()
    {
        var world = DiplomacyWorlds.Create(200f);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);

        ledger.Record(world, Rival, Me, DiplomaticAct.Betrayed, null);
        for (var i = 0; i < DiplomacyWorlds.Rules.MemoryCapacity + 3; i++)
        {
            ledger.Record(world, Rival, Me, DiplomaticAct.Courted, 1);
        }

        var memories = DiplomacyModel.FindRelation(world, Rival, Me)!.Memories;
        Assert.Equal(DiplomacyWorlds.Rules.MemoryCapacity, memories.Count);
        Assert.Contains(memories, m => m.Act == DiplomaticAct.Betrayed);
    }

    [Fact]
    public void Feelings_DriftBack_AndBetrayalIsRememberedLongest()
    {
        var world = DiplomacyWorlds.Create(200f);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        ledger.Record(world, Rival, Me, DiplomaticAct.Betrayed, null);
        ledger.Record(world, Rival, Me, DiplomaticAct.Harmed, null);
        var system = new DiplomacySystem(DiplomacyWorlds.Rules);

        for (var i = 0; i < 1000; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), []));
        }

        var view = DiplomacyModel.FindRelation(world, Rival, Me)!;
        Assert.True(view.Trust > 0.1f);
        var betrayal = Assert.Single(view.Memories, m => m.Act == DiplomaticAct.Betrayed);
        Assert.True(betrayal.Weight > 0.5f);
        Assert.DoesNotContain(view.Memories, m => m.Act == DiplomaticAct.Harmed);
    }

    [Fact]
    public void ColdFeelings_SourThePairToHostile_WarmOnes_MakeItFriendly()
    {
        var world = DiplomacyWorlds.Create(200f);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        for (var i = 0; i < 4; i++)
        {
            ledger.Record(world, Rival, Me, DiplomaticAct.Harmed, null);
        }

        new DiplomacySystem(DiplomacyWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), []));
        Assert.Equal(DiplomaticStatus.Hostile, DiplomacyModel.StatusOf(world, Me, Rival));

        var warm = DiplomacyWorlds.Create(200f);
        foreach (var (observer, subject) in new[] { (Me, Rival), (Rival, Me) })
        {
            var relation = DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, warm, observer, subject);
            relation.Trust = 0.9f;
            relation.Rivalry = 0f;
            relation.Suspicion = 0f;
        }

        new DiplomacySystem(DiplomacyWorlds.Rules).Update(new TickContext(warm, new SimulationRandom(1), []));
        Assert.Equal(DiplomaticStatus.Friendly, DiplomacyModel.StatusOf(warm, Me, Rival));
    }

    [Fact]
    public void Alliance_IsProposed_Accepted_AndRemembered()
    {
        var world = DiplomacyWorlds.Create(200f);
        var authority = DiplomacyWorlds.Authority();
        var events = new List<WorldEvent>();

        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.OfferAlliance), Me, events));
        Assert.Equal(DiplomacyRejection.AlreadyProposed, authority.Check(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.OfferAlliance), Rival));
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.Accept), Rival, events));

        Assert.True(DiplomacyModel.AreAllied(world, Me, Rival));
        Assert.Empty(world.Diplomacy.Proposals);
        Assert.Contains(DiplomacyModel.FindRelation(world, Rival, Me)!.Memories, m => m.Act == DiplomaticAct.Allied);
        Assert.Equal([DiplomaticCommandKind.OfferAlliance, DiplomaticCommandKind.Accept], events.OfType<DiplomacyChanged>().Select(e => e.Kind));
    }

    [Fact]
    public void BreakingAnAlliance_IsABetrayal_EveryGodRemembers()
    {
        var world = DiplomacyWorlds.Create(200f);
        world.Gods.Add(DivineWorlds.God(3, "Witness", GodController.Ai, GodPersonality.Guardian, 0f));
        DiplomacyWorlds.SetStatus(world, DiplomaticStatus.Alliance);
        var events = new List<WorldEvent>();

        var rejection = DiplomacyWorlds.Authority().Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.BreakAlliance), Me, events);

        Assert.Equal(DiplomacyRejection.None, rejection);
        Assert.Equal(DiplomaticStatus.Neutral, DiplomacyModel.StatusOf(world, Me, Rival));
        Assert.True(Assert.Single(events.OfType<DiplomacyChanged>()).Betrayal);
        var betrayed = DiplomacyModel.FindRelation(world, Rival, Me)!;
        Assert.Contains(betrayed.Memories, m => m.Act == DiplomaticAct.Betrayed);
        Assert.Equal(0f, betrayed.Trust);
        Assert.Equal(0.5f - DiplomacyWorlds.Rules.BetrayalReputationLoss, DiplomacyModel.FindRelation(world, 3, Me)!.Trust, 4);
    }

    [Fact]
    public void War_AndPeace_FollowTheirOwnRules()
    {
        var world = DiplomacyWorlds.Create(200f);
        var authority = DiplomacyWorlds.Authority();
        var events = new List<WorldEvent>();

        Assert.Equal(DiplomacyRejection.WrongStatus, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.RequestPeace), Me));
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.DeclareWar), Rival, events));
        Assert.True(DiplomacyModel.AreAtWar(world, Me, Rival));
        Assert.Equal(DiplomacyRejection.TooSoon, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.RequestPeace), Me));

        world.Tick += (long)(DiplomacyWorlds.Rules.StatusCooldownSeconds * SimulationTime.TicksPerSecond);
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.RequestPeace), Me, events));
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.Accept), Rival, events));

        Assert.Equal(DiplomaticStatus.Neutral, DiplomacyModel.StatusOf(world, Me, Rival));
        Assert.Contains(DiplomacyModel.FindRelation(world, Me, Rival)!.Memories, m => m.Act == DiplomaticAct.DeclaredWar);
    }

    [Fact]
    public void Commands_AreCheckedBeforeAnythingChanges()
    {
        var world = DiplomacyWorlds.Create(200f);
        var authority = DiplomacyWorlds.Authority();

        Assert.Equal(DiplomacyRejection.NotAuthorized, authority.Check(world, new DiplomaticCommand(Rival, Me, DiplomaticCommandKind.DeclareWar), Me));
        Assert.Equal(DiplomacyRejection.UnknownGod, authority.Check(world, new DiplomaticCommand(Me, Me, DiplomaticCommandKind.DeclareWar), Me));
        Assert.Equal(DiplomacyRejection.WrongStatus, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.BreakAlliance), Me));
        Assert.Equal(DiplomacyRejection.NoProposal, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.Accept), Me));
    }

    [Fact]
    public void Proposals_Lapse_AndCannotBeRepeatedAtOnce()
    {
        var world = DiplomacyWorlds.Create(200f);
        var authority = DiplomacyWorlds.Authority();
        authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.OfferAlliance), Me, new List<WorldEvent>());

        world.Tick += (long)(DiplomacyWorlds.Rules.ProposalSeconds * SimulationTime.TicksPerSecond);
        new DiplomacySystem(DiplomacyWorlds.Rules).Update(new TickContext(world, new SimulationRandom(1), []));

        Assert.Empty(world.Diplomacy.Proposals);
        Assert.Equal(DiplomacyRejection.AlreadyProposed, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.OfferAlliance), Me));
        world.Tick += (long)(DiplomacyWorlds.Rules.ProposalCooldownSeconds * SimulationTime.TicksPerSecond);
        Assert.Equal(DiplomacyRejection.None, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.OfferAlliance), Me));
    }

    [Fact]
    public void Allies_DoNotPlantDoubt_OrRaiseEachOthersCosts()
    {
        var world = DiplomacyWorlds.Create(200f, 450f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.4f, faith: 0.8f, doubt: 0.1f);
        var theirs = DivineWorlds.Believe(world.Settlements[0], adherence: 0.4f, faith: 0.9f, doubt: 0.1f, godId: Rival);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.9f, faith: 1f, godId: Rival);
        DiplomacyWorlds.SetStatus(world, DiplomaticStatus.Alliance);
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);

        Assert.Equal(0f, caster.RivalInfluence(world, Me, world.Settlements[1].Position));
        caster.Cast(world, new CastMiracleRequest(Me, "food", world.Settlements[0].Position), Me, new List<WorldEvent>());

        Assert.Equal(0.1f, theirs.Doubt, 4);
    }

    [Fact]
    public void APeopleAtWar_ResistsTheEnemy()
    {
        var peace = DiplomacyWorlds.Create(200f);
        var war = DiplomacyWorlds.Create(200f);
        foreach (var world in new[] { peace, war })
        {
            DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.6f, doubt: 0.6f);
            DivineWorlds.Believe(world.Settlements[0], adherence: 0.2f, faith: 0.9f, love: 0.8f, godId: Rival);
        }

        DiplomacyWorlds.SetStatus(war, DiplomaticStatus.War);
        var system = new BeliefSystem(DivineWorlds.Rules, SettlementWorlds.Rules, DiplomacyWorlds.Rules);

        system.Update(new TickContext(peace, new SimulationRandom(1), []));
        system.Update(new TickContext(war, new SimulationRandom(1), []));

        Assert.True(war.Settlements[0].Beliefs[1].Adherence < peace.Settlements[0].Beliefs[1].Adherence);
    }

    [Fact]
    public void Miracles_OnARivalsFollowers_AreNotedAsHelpOrCourting_ByNeed()
    {
        var world = DiplomacyWorlds.Create(200f, 450f);
        foreach (var settlement in world.Settlements)
        {
            DivineWorlds.Believe(settlement, adherence: 0.5f, faith: 0.8f, godId: Rival);
            DivineWorlds.Believe(settlement, adherence: 0.3f, faith: 0.8f);
        }

        world.Settlements[0].Food = 0f;
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(DiplomacyWorlds.Rules));
        world.Gods[0].DivinePower = 1000f;

        caster.Cast(world, new CastMiracleRequest(Me, "food", world.Settlements[0].Position), Me, new List<WorldEvent>());
        world.Tick += 1000;
        caster.Cast(world, new CastMiracleRequest(Me, "food", world.Settlements[1].Position), Me, new List<WorldEvent>());

        // Starving followers fed are saved in great need: a rescue, and the rival owes a favour (ADR-025).
        Assert.Equal([DiplomaticAct.Rescued, DiplomaticAct.Courted], DiplomacyModel.FindRelation(world, Rival, Me)!.Memories.Select(m => m.Act));
        Assert.Equal(1f, Pantheon.Simulation.Politics.FavourBook.Find(world, Me, Rival)!.Weight);
    }

    [Fact]
    public void AiDiplomat_AnswersProposalsAndExplainsItself()
    {
        var world = DiplomacyWorlds.Create(200f, 600f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.2f, faith: 0.5f, godId: Rival);
        var authority = DiplomacyWorlds.Authority();
        authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.OfferAlliance), Me, new List<WorldEvent>());
        var diplomat = new Pantheon.Simulation.Gods.GodDiplomat(DiplomacyWorlds.Rules, DivineWorlds.Rules, authority);
        var profile = RivalWorlds.AiRules.Profiles.Single(p => p.Personality == GodPersonality.Guardian);

        var moves = diplomat.Evaluate(world, world.Gods[1], profile);
        var events = new List<WorldEvent>();
        diplomat.Act(world, world.Gods[1], profile, events);

        var answer = Assert.Single(moves, m => m.Kind is DiplomaticCommandKind.Accept or DiplomaticCommandKind.Decline);
        Assert.Contains("trust", answer.Reason, System.StringComparison.Ordinal);
        Assert.Contains(events.OfType<DiplomacyChanged>(), e => e.Kind == answer.Kind);
        Assert.Empty(world.Diplomacy.Proposals);
    }

    [Fact]
    public void AiGod_DoesNotStrikeItsAllysFollowers()
    {
        var world = RivalWorlds.Create(GodPersonality.Tyrant, 500f, 200f, 450f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.8f, godId: Rival);
        DiplomacyWorlds.SetStatus(world, DiplomaticStatus.Alliance);

        var candidates = RivalWorlds.CreateAi().Evaluate(world, world.Gods[1]);

        Assert.DoesNotContain(candidates, c => c.MiracleId == "lightning" && c.SettlementId == world.Settlements[0].Id);
    }
}
