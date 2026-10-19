using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Politics;
using Pantheon.Simulation.Conflict;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Miracles;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Conflict;

/// <summary>Intelligence, veiled miracles and proxy acts (spec §43, §45, ADR-026).</summary>
public sealed class CovertTests
{
    private static readonly ConflictRules Rules = DiplomacyWorlds.ConflictRules;

    [Fact]
    public void WhatAGodKnows_ComesFromAlliesTreatiesSpiesScoutsAndLearning()
    {
        var (world, creature) = CreatureWorlds.Create(300f, 700f);
        world.Gods.Add(DivineWorlds.God(Rival, "Rivalis", GodController.Ai, GodPersonality.Opportunist, 100f));
        world.Settlements[0].Beliefs.Clear();
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.6f, godId: Rival);
        var blind = IntelModel.Level(Rules, world, Me, Rival);

        DivineWorlds.Believe(world.Settlements[0], adherence: 0.1f, faith: 0.6f);
        var spies = IntelModel.Level(Rules, world, Me, Rival);
        creature.Memories.Add(new CreatureMemory { Tick = world.Tick, Action = CreatureAction.Wander, Situation = default, SettlementId = world.Settlements[0].Id, OtherCreatureId = null, Feedback = default });
        var scouted = IntelModel.Level(Rules, world, Me, Rival);
        DiplomacyModel.EnsureStance(world, Me, Rival).Status = DiplomaticStatus.Alliance;
        world.Gods[0].Domains.Add(GodDomain.Knowledge);

        Assert.Equal(0f, blind);
        Assert.Equal(Rules.PresenceIntel * 0.1f, spies, 3);
        Assert.Equal(spies + Rules.ScoutIntel, scouted, 3);
        Assert.Equal(1f, IntelModel.Level(Rules, world, Me, Rival));
        Assert.Equal("following unknown", Pantheon.Game.Conflict.ConflictText.Following(blind, 340, 0.3f));
        Assert.Equal("about 350 followers (~30%)", Pantheon.Game.Conflict.ConflictText.Following(0.4f, 340, 0.31f));
        Assert.Equal("340 followers (31%)", Pantheon.Game.Conflict.ConflictText.Following(0.9f, 340, 0.31f));
    }

    [Fact]
    public void AVeiledStrike_IsDearer_AndUnattributed_ItsVictimsDoubtingTheirOwnGod()
    {
        var world = Create(300f);
        var village = world.Settlements[0];
        var theirs = DivineWorlds.Believe(village, adherence: 0.7f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f);
        var neverSeen = DiplomacyWorlds.Rules with { Conflict = Rules with { ExposureChance = 0f } };
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(neverSeen), diplomacy: neverSeen);
        var request = new CastMiracleRequest(Me, "lightning", village.Position);
        var open = caster.Check(world, request, Me).Cost;
        var events = new List<WorldEvent>();

        var check = caster.Cast(world, request with { Veiled = true }, Me, events);

        Assert.Equal(open * Rules.VeilCostFactor, check.Cost, 2);
        Assert.True(events.OfType<MiracleCast>().Single().Veiled);
        Assert.Equal(Rules.VeiledDoubt, theirs.Doubt, 3);
        Assert.Equal(0f, theirs.Fear);
        Assert.Null(DiplomacyModel.FindRelation(world, Rival, Me));
        Assert.Equal(0f, world.Gods[0].Conduct.Harm);
    }

    [Fact]
    public void AVeiledStrike_TracedToItsCaster_IsRememberedAndExposed()
    {
        var world = Create(300f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 0.7f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f);
        var alwaysSeen = DiplomacyWorlds.Rules with { Conflict = Rules with { ExposureChance = 1f } };
        var caster = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, DivineWorlds.Miracles, new DiplomacyLedger(alwaysSeen), diplomacy: alwaysSeen);
        var events = new List<WorldEvent>();

        caster.Cast(world, new CastMiracleRequest(Me, "lightning", village.Position, Veiled: true), Me, events);

        var exposed = events.OfType<CovertActExposed>().Single();
        Assert.Equal((Me, Rival, "lightning"), (exposed.GodId, exposed.VictimGodId, exposed.Act));
        Assert.False(events.OfType<MiracleCast>().Single().Veiled);
        var memories = DiplomacyModel.FindRelation(world, Rival, Me)!.Memories.Select(m => m.Act).ToList();
        Assert.Contains(DiplomaticAct.Harmed, memories);
        Assert.Contains(DiplomaticAct.CovertAct, memories);
        Assert.True(world.Gods[0].Conduct.Deceit > 0f);
    }

    [Fact]
    public void ProxyActs_StirResentmentOrDoubt_AtACost_AndRest()
    {
        var world = Create(300f, 700f);
        var village = world.Settlements[0];
        var theirs = DivineWorlds.Believe(village, adherence: 0.7f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(village, adherence: 0.2f, faith: 0.6f);
        var authority = new ConflictAuthority(DivineWorlds.Rules, Rules with { ExposureChance = 0f }, new DiplomacyLedger(DiplomacyWorlds.Rules));
        var stability = village.Society.Stability;
        var events = new List<WorldEvent>();

        Assert.Equal(ConflictRejection.None, authority.Execute(world, new ProxyCommand(Me, village.Id, Rival, ProxyKind.Incite), Me, events));
        var tooSoon = authority.Check(world, new ProxyCommand(Me, village.Id, Rival, ProxyKind.Propaganda), Me);
        world.Tick += (long)(Rules.ProxyCooldownSeconds * 10f);
        Assert.Equal(ConflictRejection.None, authority.Execute(world, new ProxyCommand(Me, village.Id, Rival, ProxyKind.Propaganda), Me, events));

        Assert.Equal(ConflictRejection.TooSoon, tooSoon);
        Assert.Equal(Rules.InciteResentment, theirs.Resentment, 3);
        Assert.True(theirs.Fear > 0f && village.Society.Stability < stability);
        Assert.Equal(Rules.PropagandaDoubt, theirs.Doubt, 3);
        Assert.Equal(500f - Rules.IncitePower - Rules.PropagandaPower, world.Gods[0].DivinePower, 2);
        Assert.Equal(2, events.OfType<ProxyActed>().Count());
        Assert.Empty(events.OfType<CovertActExposed>());
    }

    [Fact]
    public void ProxyActs_NeedFollowersInfluencePowerAndTheRightGod()
    {
        var world = Create(300f, 900f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.7f, faith: 0.6f, godId: Rival);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.2f, faith: 0.6f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.7f, faith: 0.6f, godId: Rival);
        var authority = new ConflictAuthority(DivineWorlds.Rules, Rules, null);

        Assert.Equal(ConflictRejection.NoFollowers, authority.Check(world, new ProxyCommand(Me, world.Settlements[0].Id, Third, ProxyKind.Incite), Me));
        Assert.Equal(ConflictRejection.OutsideInfluence, authority.Check(world, new ProxyCommand(Me, world.Settlements[1].Id, Rival, ProxyKind.Incite), Me));
        Assert.Equal(ConflictRejection.NotAuthorized, authority.Check(world, new ProxyCommand(Me, world.Settlements[0].Id, Rival, ProxyKind.Incite), Third));
        world.Gods[0].DivinePower = 10f;
        Assert.Equal(ConflictRejection.NotEnoughPower, authority.Check(world, new ProxyCommand(Me, world.Settlements[0].Id, Rival, ProxyKind.Incite), Me));
    }
}
