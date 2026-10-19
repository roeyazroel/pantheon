using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Conflict;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;
using static Pantheon.Tests.Support.PoliticsWorlds;

namespace Pantheon.Tests.Conflict;

/// <summary>Mortal holy wars (spec §46, ADR-026).</summary>
public sealed class HolyWarTests
{
    private static readonly ConflictRules Eager = DiplomacyWorlds.ConflictRules with { HolyWarPerSecond = 1f };

    [Fact]
    public void AMilitaristVillage_GoesToHolyWar_OnANeighbourOfAnotherFaith_KillingAndConverting()
    {
        var (world, attackers, defenders) = Frontier();
        var events = new List<WorldEvent>();

        Step(world, Eager, 30, events);

        var declared = events.OfType<HolyWarDeclared>().Single();
        Assert.Equal((attackers.Id, defenders.Id, Me, Rival), (declared.SettlementId, declared.DefenderSettlementId, declared.GodId, declared.EnemyGodId));
        var war = attackers.Society.HolyWar!;
        Assert.True(war.Killed > 0 && war.Converted > 0, $"killed {war.Killed}, converted {war.Converted}");
        Assert.True(defenders.Beliefs.Single(b => b.GodId == Me).Adherence > 0f);
        Assert.Contains(DiplomacyModel.FindRelation(world, Rival, Me)!.Memories, m => m.Act == DiplomaticAct.HolyWar);
    }

    [Fact]
    public void Encouragement_MakesRaidsFiercer_AndDiscouragementCostsLove()
    {
        int Killed(HolyWarStance stance)
        {
            var (world, attackers, defenders) = Frontier();
            Step(world, Eager, 1, []);
            var authority = new ConflictAuthority(DivineWorlds.Rules, Eager, null);
            Assert.Equal(ConflictRejection.None, authority.Execute(world, new HolyWarCommand(Me, attackers.Id, stance), Me));
            Step(world, Eager, 30, []);
            return 100 - defenders.Population;
        }

        var (world, attackers, _) = Frontier();
        Step(world, Eager, 1, []);
        var love = attackers.Beliefs.Single().Love;
        new ConflictAuthority(DivineWorlds.Rules, Eager, null).Execute(world, new HolyWarCommand(Me, attackers.Id, HolyWarStance.Discourage), Me);

        Assert.True(Killed(HolyWarStance.Encourage) > Killed(HolyWarStance.Discourage));
        Assert.Equal(love - Eager.DiscourageLoveLoss, attackers.Beliefs.Single().Love, 3);
        Assert.Equal(ConflictRejection.NotItsWar, new ConflictAuthority(DivineWorlds.Rules, Eager, null).Check(world, new HolyWarCommand(Rival, attackers.Id, HolyWarStance.Encourage), Rival));
    }

    [Fact]
    public void AHolyWar_Ends_WhenItsTimeRunsOut_AndAlliesAreNotAttacked()
    {
        var (world, attackers, _) = Frontier();
        var events = new List<WorldEvent>();
        Step(world, Eager, 1, events);
        Step(world, Eager, (int)Eager.HolyWarSeconds + 2, events);

        var (allied, peaceful, _) = Frontier();
        allied.Diplomacy.Stances.Clear();
        DiplomacyModel.EnsureStance(allied, Me, Rival).Status = DiplomaticStatus.Alliance;
        Step(allied, Eager, 10, []);

        Assert.Single(events.OfType<HolyWarEnded>());
        Assert.Null(attackers.Society.HolyWar);
        Assert.Null(peaceful.Society.HolyWar);
    }

    [Fact]
    public void AnAiGod_UrgesOnItsPeoplesHolyWars_AtWar_AndHoldsThemBackFromItsAllies()
    {
        var world = Create(300f);
        var guardian = TestContent.Shipped.GodAi.Profiles!.First(p => p.Personality == GodPersonality.Guardian);
        SetStatus(world, Me, Rival, DiplomaticStatus.War);
        SetStatus(world, Me, Third, DiplomaticStatus.Alliance);

        Assert.Equal(HolyWarStance.Encourage, CovertAdvisor.Stance(world, Me, Rival, guardian));
        Assert.Equal(HolyWarStance.Discourage, CovertAdvisor.Stance(world, Me, Third, guardian));
    }

    /// <summary>A militarist village of the player's god beside a village of the rival's.</summary>
    private static (WorldState World, Domain.Settlements.SettlementState Attackers, Domain.Settlements.SettlementState Defenders) Frontier()
    {
        var world = Create(300f, 500f);
        var (attackers, defenders) = (world.Settlements[0], world.Settlements[1]);
        DivineWorlds.Believe(attackers, adherence: 0.8f, faith: 0.8f, love: 0.6f);
        DivineWorlds.Believe(defenders, adherence: 0.8f, faith: 0.6f, godId: Rival);
        attackers.Culture.Militarism = 0.9f;
        attackers.Population = 200;
        return (world, attackers, defenders);
    }

    private static void Step(WorldState world, ConflictRules rules, int steps, List<WorldEvent> events)
    {
        var system = new HolyWarSystem(rules, 6, new DiplomacyLedger(DiplomacyWorlds.Rules), 10);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events));
            world.Tick += 10;
        }
    }
}
