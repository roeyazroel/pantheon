using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Politics;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Politics;

/// <summary>Treaties: their terms, what they forbid and allow, breaking them, and their end (spec §38–40, ADR-025).</summary>
public sealed class TreatyTests
{
    [Fact]
    public void ANonAggressionPact_ForbidsWar_UntilBroken_AndBreakingItIsABetrayal()
    {
        var world = Create(300f);
        var authority = Authority();
        var events = new List<WorldEvent>();
        var pact = Sign(world, authority, Me, Rival, Terms(TreatyKind.NonAggression), events);
        var thirdTrust = DiplomacyModel.EnsureRelation(DiplomacyWorlds.Rules, world, Third, Me).Trust;

        Assert.Equal(DiplomacyRejection.TreatyForbids, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar), Me));
        world.Tick += 1000;
        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.BreakTreaty, TreatyId: pact.Id), Me, events));
        Assert.Equal(DiplomacyRejection.None, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.DeclareWar), Me));

        Assert.Empty(world.Diplomacy.Treaties);
        Assert.Contains(DiplomacyModel.FindRelation(world, Rival, Me)!.Memories, m => m.Act == DiplomaticAct.BrokeTreaty);
        Assert.True(DiplomacyModel.FindRelation(world, Third, Me)!.Trust < thirdTrust);
        Assert.True(world.Gods[0].Conduct.Deceit > 0f);
        Assert.Contains(events.OfType<TreatyEnded>(), e => e.BrokenByGodId == Me && e.Kind == TreatyKind.NonAggression);
        Assert.Contains(events.OfType<TreatySigned>(), e => e.TreatyId == pact.Id);
    }

    [Theory]
    [InlineData(TreatyKind.JointWar, null, null, 0f, 300f)]
    [InlineData(TreatyKind.Tribute, null, Rival, 1000f, 300f)]
    [InlineData(TreatyKind.Tribute, null, Third, 20f, 300f)]
    [InlineData(TreatyKind.NonAggression, Third, null, 0f, 300f)]
    [InlineData(TreatyKind.DefensivePact, Me, null, 0f, 300f)]
    [InlineData(TreatyKind.MutualDefense, null, null, 0f, 5000f)]
    public void InvalidTerms_AreRefused(TreatyKind kind, int? target, int? payer, float amount, float seconds)
    {
        var world = Create(300f);
        var terms = new TreatyTerms { Kind = kind, TargetGodId = target, PayerGodId = payer, Amount = amount, DurationSeconds = seconds, Secret = false };

        Assert.Equal(DiplomacyRejection.InvalidTerms, Authority().Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.ProposeTreaty, Terms: terms), Me));
    }

    [Fact]
    public void AtWar_OnlyPeacePactsMayBeProposed_AndSigningOneMakesPeace()
    {
        var world = Create(300f);
        SetStatus(world, Me, Rival, DiplomaticStatus.War);
        var authority = Authority();

        Assert.Equal(DiplomacyRejection.WrongStatus, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.ProposeTreaty, Terms: Terms(TreatyKind.SharedInfluence)), Me));
        Sign(world, authority, Me, Rival, Terms(TreatyKind.NonAggression));

        Assert.Equal(DiplomaticStatus.Neutral, DiplomacyModel.StatusOf(world, Me, Rival));
        Assert.Equal(DiplomacyRejection.WrongStatus, authority.Check(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.ProposeTreaty, Terms: Terms(TreatyKind.NonAggression)), Me));
    }

    [Fact]
    public void AJointWar_SendsBothToWar_AndForbidsASeparatePeace()
    {
        var world = Create(300f);
        var authority = Authority();

        Sign(world, authority, Me, Rival, Terms(TreatyKind.JointWar) with { TargetGodId = Third });

        Assert.True(DiplomacyModel.AreAtWar(world, Me, Third));
        Assert.True(DiplomacyModel.AreAtWar(world, Rival, Third));
        world.Tick += 100_000;
        Assert.Equal(DiplomacyRejection.TreatyForbids, authority.Check(world, new DiplomaticCommand(Me, Third, DiplomaticCommandKind.RequestPeace), Me));
    }

    [Fact]
    public void ASecretDefensivePact_CallsTheDefender_EvenAgainstItsAlly_AndComesToLight()
    {
        var world = Create(300f);
        var authority = Authority();
        var events = new List<WorldEvent>();
        SetStatus(world, Rival, Third, DiplomaticStatus.Alliance);
        var pact = Sign(world, authority, Rival, Me, Terms(TreatyKind.DefensivePact) with { TargetGodId = Third, Secret = true }, events);

        Assert.Equal(DiplomacyRejection.None, authority.Execute(world, new DiplomaticCommand(Third, Me, DiplomaticCommandKind.DeclareWar), Third, events));

        var invoked = events.OfType<TreatyInvoked>().Single();
        Assert.Equal((Rival, Me, Third, true), (invoked.DefenderGodId, invoked.ProtectedGodId, invoked.AggressorGodId, invoked.Betrayal));
        Assert.True(DiplomacyModel.AreAtWar(world, Rival, Third));
        Assert.False(pact.Secret);
        Assert.Single(events.OfType<TreatyRevealed>());
        Assert.Contains(DiplomacyModel.FindRelation(world, Third, Rival)!.Memories, m => m.Act == DiplomaticAct.Betrayed);
    }

    [Fact]
    public void MutualDefence_CallsEitherPartner()
    {
        var world = Create(300f);
        var authority = Authority();
        Sign(world, authority, Me, Rival, Terms(TreatyKind.MutualDefense));

        authority.Execute(world, new DiplomaticCommand(Fourth, Rival, DiplomaticCommandKind.DeclareWar), Fourth, new List<WorldEvent>());

        Assert.True(DiplomacyModel.AreAtWar(world, Me, Fourth));
    }

    [Fact]
    public void Treaties_RunTheirCourse_AndTributesArePaid_UntilThePayerCannotPay()
    {
        var world = Create(300f);
        var authority = Authority();
        var events = new List<WorldEvent>();
        Sign(world, authority, Me, Rival, Terms(TreatyKind.SharedInfluence) with { DurationSeconds = 2f });
        Sign(world, authority, Me, Third, Terms(TreatyKind.Tribute) with { PayerGodId = Third, Amount = 60f });
        var gods = world.Gods.ToDictionary(g => g.Id);
        (gods[Me].DivinePower, gods[Third].DivinePower) = (0f, 5f);

        Step(world, 3, events);
        var paid = gods[Me].DivinePower;
        Step(world, 10, events);

        Assert.Equal(3f, paid, 3);
        Assert.Contains(events.OfType<TreatyEnded>(), e => e.Kind == TreatyKind.SharedInfluence && e.BrokenByGodId is null);
        Assert.Contains(events.OfType<TreatyEnded>(), e => e.Kind == TreatyKind.Tribute && e.BrokenByGodId == Third);
        Assert.Empty(world.Diplomacy.Treaties);
    }

    [Fact]
    public void SecretTreaties_ComeToLight_AndTheirTargetRemembersThePlot()
    {
        var world = Create(300f);
        var authority = Authority();
        var events = new List<WorldEvent>();
        Sign(world, authority, Me, Rival, Terms(TreatyKind.JointWar) with { TargetGodId = Third, Secret = true });
        var leaky = DiplomacyWorlds.Rules with { Politics = DiplomacyWorlds.PoliticsRules with { SecretLeakPerSecond = 1f } };

        Step(world, 1, events, leaky);

        Assert.Single(events.OfType<TreatyRevealed>());
        Assert.False(world.Diplomacy.Treaties.Single().Secret);
        Assert.Contains(DiplomacyModel.FindRelation(world, Third, Me)!.Memories, m => m.Act == DiplomaticAct.PlottedAgainst);
    }

    [Fact]
    public void Treaties_ChangeWhatTheGodsMayDo()
    {
        var world = Create(300f, 700f);
        var authority = Authority();
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.7f, faith: 0.6f, godId: Rival);
        foreach (var kind in new[] { TreatyKind.SharedInfluence, TreatyKind.ReligiousAccess, TreatyKind.CreatureNonAggression, TreatyKind.DisasterAssistance, TreatyKind.TerritorialAgreement, TreatyKind.MigrationAgreement })
        {
            Sign(world, authority, Me, Rival, Terms(kind));
            world.Tick += 2000;
        }

        Assert.True(TreatyModel.SharesInfluence(world, Me, Rival));
        Assert.True(TreatyModel.HasAccess(world, Me, world.Settlements[0]));
        Assert.True(TreatyModel.SparesCreatures(world, Rival, Me));
        Assert.True(TreatyModel.Assists(world, Me, Rival));
        Assert.True(TreatyModel.WelcomesMigrants(world, Rival, Me));
        Assert.Equal(Rival, TreatyModel.ForbiddenLand(world, Me, world.Settlements[0].Position, 30f));
        Assert.Null(TreatyModel.ForbiddenLand(world, Me, world.Settlements[1].Position, 30f));
        Assert.False(TreatyModel.SharesInfluence(world, Me, Third));
    }

    [Fact]
    public void ATerritorialAgreement_KeepsTheGodsMiraclesOutOfItsPartnersVillages()
    {
        var world = Create(300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.7f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.2f, faith: 0.6f);
        Sign(world, Authority(), Me, Rival, Terms(TreatyKind.TerritorialAgreement));
        var caster = new Pantheon.Simulation.Miracles.MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles);

        var check = caster.Check(world, new Pantheon.Domain.Gods.CastMiracleRequest(Me, "food", world.Settlements[0].Position), Me);

        Assert.Equal(Pantheon.Simulation.Miracles.CastRejection.TreatyForbids, check.Rejection);
    }

    [Fact]
    public void Absorbing_AnAlly_TakesAShareOfItsFollowersWhereTheGodLeads_AndIsABetrayal()
    {
        var world = Create(300f, 700f);
        SetStatus(world, Me, Rival, DiplomaticStatus.Alliance);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.6f);
        var led = DivineWorlds.Believe(world.Settlements[0], adherence: 0.3f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.2f, faith: 0.6f);
        var theirs = DivineWorlds.Believe(world.Settlements[1], adherence: 0.6f, faith: 0.6f, godId: Rival);
        var events = new List<WorldEvent>();

        Assert.Equal(DiplomacyRejection.None, Authority().Execute(world, new DiplomaticCommand(Me, Rival, DiplomaticCommandKind.Absorb), Me, events));

        Assert.Equal(0.3f * (1f - DiplomacyWorlds.PoliticsRules.AbsorbShare), led.Adherence, 4);
        Assert.Equal(0.6f, theirs.Adherence);
        Assert.Equal(12, events.OfType<AllyAbsorbed>().Single().People);
        Assert.Equal(DiplomaticStatus.Neutral, DiplomacyModel.StatusOf(world, Me, Rival));
        Assert.Contains(DiplomacyModel.FindRelation(world, Rival, Me)!.Memories, m => m.Act == DiplomaticAct.Betrayed);
    }
}
