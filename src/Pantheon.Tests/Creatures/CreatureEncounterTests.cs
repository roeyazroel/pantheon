using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>Creatures meeting, playing, fleeing and fighting (spec §27–28, §111, §126, ADR-017).</summary>
public sealed class CreatureEncounterTests
{
    private static readonly CreatureRules Rules = CreatureBondWorlds.Rules;

    [Fact]
    public void CreaturesThatComeClose_Meet_ThenNotAgainUntilTheCooldown()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsCreature, CreatureAction.Ignore);
        }

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 20);

        var met = Assert.Single(events.OfType<CreaturesMet>());
        Assert.Equal(MeetingOutcome.PassedBy, met.Outcome);
        Assert.False(met.GodsAtWar);
        Assert.Equal(EncounterKind.Met, CreatureBonds.Find(mine, theirs.Id)!.Encounters.Single().Kind);
        Assert.Equal(met.Tick, CreatureBonds.Find(theirs, mine.Id)!.LastMetTick);
    }

    [Fact]
    public void AtWar_TheStrongerCreatureWins_AndTheLoserIsBanishedScarredLessLoyalAndSetBack()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        }

        mine.Experience = Rules.LevelExperience[3];
        mine.Level = 5;
        theirs.Experience = Rules.LevelExperience[0] + 30f;
        theirs.Level = 2;
        var (loyalty, progress) = (theirs.Loyalty, 30f);
        var ledger = new DiplomacyLedger(DiplomacyWorlds.Rules);
        var proud = BeliefModel.Find(world.Settlements[0], 1)!;
        var shaken = BeliefModel.Find(world.Settlements[1], 2)!;
        var (faith, doubt) = (proud.Faith, shaken.Doubt);

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(ledger: ledger), 400);

        var met = Assert.Single(events.OfType<CreaturesMet>());
        Assert.Equal(MeetingOutcome.Fought, met.Outcome);
        Assert.True(met.GodsAtWar);
        var end = Assert.Single(events.OfType<CreatureFightEnded>());
        Assert.Equal((mine.Id, theirs.Id, FightOutcome.Banished), (end.CreatureId, end.OtherCreatureId, end.Outcome));
        Assert.Equal(CreaturePhase.Banished, theirs.Activity.Phase);
        Assert.Equal(1, theirs.Scars);
        Assert.Equal(loyalty - Rules.Combat.LoyaltyPerDefeat, theirs.Loyalty, 4);
        Assert.Equal(Rules.LevelExperience[0] + (progress * (1f - Rules.Combat.DefeatExperienceLoss)), theirs.Experience, 3);
        Assert.Equal(2, theirs.Level);
        Assert.True(mine.Experience > Rules.LevelExperience[3]);
        Assert.Equal(Rules.Combat.SkillPerFight, mine.CombatSkill, 4);
        Assert.Equal(Rules.Combat.SkillPerFight, theirs.CombatSkill, 4);
        Assert.True(CreatureBonds.Find(theirs, mine.Id)!.Fear > Rules.Bonds.FearPerDefeat - 0.05f, "the loser fears the winner (less what has faded since)");
        Assert.Contains(DiplomacyModel.FindRelation(world, 2, 1)!.Memories, m => m.Act == DiplomaticAct.CreatureDefeated);
        Assert.True(proud.Faith > faith, "the winner's followers who saw it think more of their god");
        Assert.True(shaken.Doubt > doubt, "the loser's followers who saw it doubt theirs");
        Assert.Contains(mine.Memories, m => m.Action == CreatureAction.Fight && m.OtherCreatureId == theirs.Id);
    }

    [Fact]
    public void AMercifulWinner_LetsTheLoserGo()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.Hostile);
        CreatureBondWorlds.Prefer(mine, CreatureSituation.MeetsCreature, CreatureAction.Ignore);
        CreatureBondWorlds.Prefer(theirs, CreatureSituation.MeetsCreature, CreatureAction.Fight);
        theirs.Personality.Aggression = 1f;
        mine.Level = 6;
        (mine.Personality.Kindness, mine.Personality.Aggression) = (1f, 0f);

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 400);

        Assert.True(CreatureCombat.Mercy(world, mine, theirs) >= Rules.Combat.SpareThreshold);
        var end = Assert.Single(events.OfType<CreatureFightEnded>());
        Assert.Equal((mine.Id, FightOutcome.Spared), (end.CreatureId, end.Outcome));
        Assert.NotEqual(CreaturePhase.Banished, theirs.Activity.Phase);
        Assert.Equal(0, theirs.Scars);
        Assert.Equal(EncounterKind.WasSpared, CreatureBonds.Find(theirs, mine.Id)!.Encounters[^1].Kind);
    }

    [Fact]
    public void EvenFighters_WhoCannotHurtEachOtherEnough_Draw()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        }

        var gentle = Rules with { Combat = Rules.Combat with { DamagePerSecond = 0.001f } };

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(gentle), 500);

        Assert.Equal(FightOutcome.Drew, Assert.Single(events.OfType<CreatureFightEnded>()).Outcome);
        Assert.Equal(EncounterKind.Drew, CreatureBonds.Find(mine, theirs.Id)!.Encounters[^1].Kind);
        Assert.All(new[] { mine, theirs }, c => Assert.NotEqual(CreaturePhase.Banished, c.Activity.Phase));
    }

    [Fact]
    public void APunishment_CallsTheCreatureOffAFight_AndTheOtherHasChasedItOff()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        }

        var system = CreatureBondWorlds.System();
        CreatureBondWorlds.Run(world, system, 10);
        Assert.True(CreatureCombat.IsFighting(mine));
        var authority = new CreatureAuthority(Rules, system.Brain);

        var judged = authority.Execute(world, new CreatureCommand(1, mine.Id, CreatureCommandKind.Punish), 1, new List<WorldEvent>());
        var events = CreatureBondWorlds.Run(world, system, 2);

        Assert.Equal(CreatureAction.Fight, judged.JudgedAction);
        var end = Assert.Single(events.OfType<CreatureFightEnded>());
        Assert.Equal((theirs.Id, mine.Id, FightOutcome.BrokeOff), (end.CreatureId, end.OtherCreatureId, end.Outcome));
        Assert.Equal(EncounterKind.Fled, CreatureBonds.Find(mine, theirs.Id)!.Encounters[^1].Kind);
        Assert.False(CreatureCombat.IsFighting(theirs));
    }

    [Fact]
    public void AFrightenedCreature_RunsFromAnAttacker()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        CreatureBondWorlds.Prefer(mine, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        CreatureBondWorlds.Prefer(theirs, CreatureSituation.MeetsEnemyCreature, CreatureAction.Flee);
        CreatureBonds.Ensure(Rules.Bonds, theirs, mine.Id).Fear = 0.8f;

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 10);

        var met = Assert.Single(events.OfType<CreaturesMet>());
        Assert.Equal((MeetingOutcome.Fled, theirs.Id), (met.Outcome, met.CreatureId));
        Assert.Empty(events.OfType<CreatureFightEnded>());
        Assert.Equal((CreatureAction.Flee, mine.Id), (theirs.Activity.Action, theirs.Activity.TargetCreatureId));
        Assert.Equal(EncounterKind.ChasedOff, CreatureBonds.Find(mine, theirs.Id)!.Encounters[^1].Kind);
    }

    [Fact]
    public void CreaturesThatBothWantToPlay_PlayTogether_AndBecomeFriends()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsCreature, CreatureAction.Befriend);
        }

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 120);

        Assert.Equal(MeetingOutcome.Played, Assert.Single(events.OfType<CreaturesMet>()).Outcome);
        Assert.All(new[] { (mine, theirs), (theirs, mine) }, pair =>
        {
            var view = CreatureBonds.Find(pair.Item1, pair.Item2.Id)!;
            Assert.Equal(Rules.Bonds.FriendshipPerPlay, view.Friendship, 2);
            Assert.Equal(EncounterKind.Played, view.Encounters[^1].Kind);
        });
        Assert.Contains(mine.Memories, m => m.Action == CreatureAction.Befriend && m.OtherCreatureId == theirs.Id);
    }

    [Fact]
    public void FriendsWhoseGodsGoToWar_DoNotFight()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        foreach (var (self, other) in new[] { (mine, theirs), (theirs, mine) })
        {
            CreatureBonds.Ensure(Rules.Bonds, self, other.Id).Friendship = 0.9f;
            self.Personality.Aggression = 1f;
        }

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 10);

        var met = Assert.Single(events.OfType<CreaturesMet>());
        Assert.NotEqual(MeetingOutcome.Fought, met.Outcome);
        Assert.True(met.GodsAtWar && met.Friends);
    }

    [Fact]
    public void AnAttackedFriend_FeelsBetrayed()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        CreatureBondWorlds.Prefer(mine, CreatureSituation.MeetsCreature, CreatureAction.Befriend);
        CreatureBondWorlds.Prefer(theirs, CreatureSituation.MeetsCreature, CreatureAction.Fight);
        theirs.Personality.Aggression = 1f;
        CreatureBonds.Ensure(Rules.Bonds, mine, theirs.Id).Friendship = 0.6f;
        CreatureBonds.Ensure(Rules.Bonds, mine, theirs.Id).Trust = 0.8f;

        var events = CreatureBondWorlds.Run(world, CreatureBondWorlds.System(), 10);

        var met = Assert.Single(events.OfType<CreaturesMet>());
        Assert.Equal((MeetingOutcome.Fought, theirs.Id, true), (met.Outcome, met.CreatureId, met.Betrayal));
        Assert.Contains(CreatureBonds.Find(mine, theirs.Id)!.Encounters, e => e.Kind == EncounterKind.Betrayed);
    }

    [Fact]
    public void ACreatureOnItsGodsErrand_DoesNotStopToPlay()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create();
        foreach (var creature in new[] { mine, theirs })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsCreature, CreatureAction.Befriend);
        }

        var system = CreatureBondWorlds.System();
        Assert.True(system.Brain.Command(world, mine, new GroundPosition(600f, 512f)));

        var events = CreatureBondWorlds.Run(world, system, 10);

        Assert.Equal(MeetingOutcome.Greeted, Assert.Single(events.OfType<CreaturesMet>()).Outcome);
        Assert.Equal(CreatureAction.GoTo, mine.Activity.Action);
    }

    [Fact]
    public void ACreatureAttackingAVillage_BecomesTheRivalOfThatGodsCreature()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 400f);
        var deeds = new CreatureDeeds(Rules, DivineWorlds.Rules, SettlementWorlds.NoBirthsOrDeaths);
        theirs.Activity.Action = CreatureAction.Attack;
        theirs.Activity.Situation = CreatureSituation.VillageThriving;
        theirs.Activity.TargetSettlementId = world.Settlements[0].Id;

        deeds.Complete(world, theirs, new List<WorldEvent>());

        var view = CreatureBonds.Find(mine, theirs.Id)!;
        Assert.Equal(Rules.Bonds.RivalryPerAttack, view.Rivalry, 4);
        Assert.Equal(EncounterKind.AttackedOurPeople, view.Encounters.Single().Kind);
    }

    [Fact]
    public void RivalsAtWar_GoLookingForEachOther()
    {
        var (world, mine, theirs) = CreatureBondWorlds.Create(gap: 250f);
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        CreatureBondWorlds.Prefer(mine, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        CreatureBonds.Ensure(Rules.Bonds, mine, theirs.Id).Rivalry = 1f;
        mine.Personality.Aggression = 1f;
        var brain = CreatureBondWorlds.System().Brain;

        var best = brain.ListOptions(world, mine)[0];
        brain.Decide(world, mine, new SimulationRandom(1));

        Assert.Equal((CreatureAction.Fight, (int?)theirs.Id), (best.Action, best.CreatureId));
        Assert.Equal((CreatureAction.Fight, CreaturePhase.Travelling, (int?)theirs.Id), (mine.Activity.Action, mine.Activity.Phase, mine.Activity.TargetCreatureId));
    }

    [Fact]
    public void AFight_PlaysOutTheSame_AcrossASaveInTheMiddle()
    {
        var straight = Pantheon.Game.GameSession.CreateNew(TestContent.Small, new WorldSeed(77)).World;
        var (a, b) = (straight.Creatures[0], straight.Creatures[1]);
        b.Position = CreatureFounder.Beside(straight.Regions, a.Position)!.Value;
        PantheonWorlds.SetStatus(straight, a.GodId, b.GodId, DiplomaticStatus.War);
        foreach (var creature in new[] { a, b })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        }

        var resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(straight));
        var system = CreatureBondWorlds.System();

        CreatureBondWorlds.Run(straight, system, 300);
        CreatureBondWorlds.Run(resumed, system, 60);
        Assert.True(CreatureCombat.IsFighting(resumed.Creatures[0]));
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        CreatureBondWorlds.Run(resumed, system, 240);

        WorldAssert.Equal(straight, resumed);
        Assert.NotEmpty(straight.Creatures[0].Relations);
    }

    [Fact]
    public void Blows_UseTheirOwnStream_SoAFightLeavesTheSharedStreamToTheBrains()
    {
        var (world, a, b) = CreatureBondWorlds.Create();
        PantheonWorlds.SetStatus(world, 1, 2, DiplomaticStatus.War);
        foreach (var creature in new[] { a, b })
        {
            CreatureBondWorlds.Prefer(creature, CreatureSituation.MeetsEnemyCreature, CreatureAction.Fight);
        }

        var system = CreatureBondWorlds.System();
        CreatureBondWorlds.Run(world, system, 10);
        Assert.True(CreatureCombat.IsFighting(a));
        var before = world.Random;

        // An odd tick: no decisions, only blows.
        new SimulationEngine([system]).Step(world);

        Assert.Equal(before, world.Random);
    }
}
