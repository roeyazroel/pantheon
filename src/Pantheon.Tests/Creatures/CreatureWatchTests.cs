using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Alerts;
using Pantheon.Game.Creatures;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Creatures;

/// <summary>Gods answering other gods' Creatures in their lands (ADR-070), and fights that leave a winner its breath.</summary>
public sealed class CreatureWatchTests
{
    private static readonly WatchRules Rules = TestContent.Shipped.GodAi.Watch;

    [Theory]
    [InlineData(DiplomaticStatus.Alliance, WatchAnswer.Welcome)]
    [InlineData(DiplomaticStatus.Friendly, WatchAnswer.Welcome)]
    [InlineData(DiplomaticStatus.Neutral, WatchAnswer.Watch)]
    [InlineData(DiplomaticStatus.War, WatchAnswer.Repel)]
    public void AGod_AnswersAVisitorByHowItStandsWithItsGod(DiplomaticStatus status, WatchAnswer expected)
    {
        var (session, rival, _, _) = Visit(status);

        Assert.Equal(expected, CreatureWatch.AnswerFor(Rules, CreatureWatch.Hostility(session.World, rival, session.LocalGodId!.Value, null)));
    }

    [Fact]
    public void AnAggressiveGod_DrivesOutWhatACalmOneOnlyWatches()
    {
        var (session, rival, _, _) = Visit(DiplomaticStatus.Hostile);
        var me = session.LocalGodId!.Value;
        var calm = TestContent.Shipped.GodAi.Profiles.First(p => p.Aggression <= 0.3f);
        var fierce = TestContent.Shipped.GodAi.Profiles.First(p => p.Aggression >= 0.8f);

        Assert.Equal(WatchAnswer.Watch, CreatureWatch.AnswerFor(Rules, CreatureWatch.Hostility(session.World, rival, me, calm)));
        Assert.Equal(WatchAnswer.Repel, CreatureWatch.AnswerFor(Rules, CreatureWatch.Hostility(session.World, rival, me, fierce)));
        Assert.True(CreatureWatch.Hostility(session.World, rival, me, null) < CreatureWatch.Hostility(session.World, rival, me, fierce));
    }

    [Fact]
    public void AnEnemy_SetsItsCreatureOnYours_RemembersTheTrespass_AndYouAreTold()
    {
        var (session, rival, village, guard) = Visit(DiplomaticStatus.War);
        var mine = session.LocalCreature!;

        var events = Run(session, 2);

        var watched = Assert.Single(events.OfType<CreatureWatched>(), w => w.CreatureId == mine.Id && w.GodId == rival.Id);
        Assert.Equal(WatchAnswer.Repel, watched.Answer);
        Assert.Equal(village.Id, watched.SettlementId);
        Assert.Equal(guard.Id, watched.GuardId);
        Assert.True(CreatureBrain.IsHunting(guard, mine.Id), "its Creature is set on the trespasser");
        Assert.Contains(DiplomacyModel.FindRelation(session.World, rival.Id, session.LocalGodId!.Value)!.Memories, m => m.Act == DiplomaticAct.Trespassed);
        var alert = new AlertFeed().Push([watched], session.World, session.LocalGodId, 1).Single();
        Assert.Equal($"{rival.Name} drives {mine.Name} out of {village.Name}: it sets {guard.Name} on it!", alert.Text);
        Assert.DoesNotContain(Run(session, 3).OfType<CreatureWatched>(), w => w.CreatureId == mine.Id && w.GodId == rival.Id);
    }

    [Fact]
    public void AnAlly_SendsItsCreatureToGreetYours_AndRemembersAFriendsVisit()
    {
        var (session, rival, village, guard) = Visit(DiplomaticStatus.Alliance);
        var mine = session.LocalCreature!;

        var watched = Assert.Single(Run(session, 2).OfType<CreatureWatched>(), w => w.CreatureId == mine.Id && w.GodId == rival.Id);

        Assert.Equal(WatchAnswer.Welcome, watched.Answer);
        Assert.Equal(guard.Id, watched.GuardId);
        Assert.Equal(CreatureAction.GoTo, guard.Activity.Action);
        Assert.False(CreatureBrain.IsHunting(guard, mine.Id));
        Assert.Contains(DiplomacyModel.FindRelation(session.World, rival.Id, session.LocalGodId!.Value)!.Memories, m => m.Act == DiplomaticAct.Visited);
        Assert.Equal($"{rival.Name} welcomes {mine.Name} to {village.Name}: {guard.Name} comes to greet it.", new AlertFeed().Push([watched], session.World, session.LocalGodId, 1).Single().Text);
    }

    [Fact]
    public void YouAreTold_WhenAnotherGodsCreatureComesIntoYourVillage()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234), new NewWorldOptions(GodsMet: true));
        var me = session.LocalGodId!.Value;
        var home = session.World.Settlements.First(s => TreatyModel.Leader(s) == me);
        var visitor = session.World.Creatures.First(c => c.GodId != me);
        visitor.Position = home.Position;

        var watched = Run(session, 2).OfType<CreatureWatched>().First(w => w.GodId == me && w.CreatureId == visitor.Id);

        Assert.Equal(WatchAnswer.Unanswered, watched.Answer);
        Assert.Null(watched.GuardId);
        Assert.StartsWith($"{visitor.Name}, ", new AlertFeed().Push([watched], session.World, me, 1).Single().Text);
    }

    [Fact]
    public void AWinner_GetsItsBreathBack_BeforeItCanBeDrawnIntoAnotherFight()
    {
        var rules = TestContent.Shipped.Creatures;
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalCreature!;
        var tick = session.World.Tick;
        mine.Memories.Add(new CreatureMemory { Tick = tick, Action = CreatureAction.Fight, Situation = CreatureSituation.MeetsEnemyCreature, SettlementId = null, OtherCreatureId = null, Feedback = CreatureFeedback.None });

        Assert.True(CreatureCombat.Recovering(rules, mine, tick + 10));
        Assert.False(CreatureCombat.Recovering(rules, mine, tick + (long)(rules.Combat.RespiteSeconds * 10f) + 1));
        Assert.False(CreatureCombat.Recovering(rules with { Combat = rules.Combat with { RespiteSeconds = 0f } }, mine, tick + 10));
        Assert.True(rules.Combat.DamagePerSecond <= 0.03f, "a fight between equals lasts about half a minute");
    }

    [Fact]
    public void YourCreaturesFight_SlowsTheWorldToX1()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var mine = session.LocalCreature!;
        var other = CreatureCommands.NearestOther(session)!;
        WorldEvent[] fight = [new CreaturesMet { Tick = 1, CreatureId = other.Id, OtherCreatureId = mine.Id, Outcome = MeetingOutcome.Fought, Betrayal = false, GodsAtWar = false, Friends = false }];

        Assert.Null(CreatureCommands.SlowForFight(session, fight));
        SessionCommands.SetSpeedPreset(session, 2);
        Assert.Null(CreatureCommands.SlowForFight(session, []));
        Assert.Equal($"{mine.Name} is fighting: time slows to ×1 so you can watch", CreatureCommands.SlowForFight(session, fight));
        Assert.Equal(1.0, session.Scheduler.TimeScale);
    }

    [Fact]
    public void Visit_SetsYourCreatureDownBesideAVillage()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var village = session.World.Settlements[2];
        var mine = session.LocalCreature!;

        Assert.Equal($"{mine.Name} visits {village.Name}", CreatureCommands.Visit(session, 3));
        Assert.True(MathF.Abs(mine.Position.X - village.Position.X) + MathF.Abs(mine.Position.Z - village.Position.Z) < 200f);
        Assert.Equal("Your Creature cannot visit there", CreatureCommands.Visit(session, 99));
        Assert.Equal(3, Pantheon.Game.Launch.LaunchOptions.Parse(["--visit=3"]).Visit);
        Assert.Single(Pantheon.Game.Launch.LaunchOptions.Parse(["--visit=0"]).Warnings);
    }

    /// <summary>A world where the player's Creature stands in a village another god leads, the two gods at a standing.</summary>
    private static (GameSession Session, GodState Rival, Pantheon.Domain.Settlements.SettlementState Village, CreatureState Guard) Visit(DiplomaticStatus status)
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234), new NewWorldOptions(GodsMet: true));
        var world = session.World;
        var me = session.LocalGodId!.Value;
        var village = world.Settlements.First(s => TreatyModel.Leader(s) is { } leader && leader != me && world.Gods.Find(g => g.Id == leader)!.Controller == GodController.Ai);
        var rival = world.Gods.Find(g => g.Id == TreatyModel.Leader(village))!;
        var (a, b) = (Math.Min(me, rival.Id), Math.Max(me, rival.Id));
        world.Diplomacy.Stances.RemoveAll(s => s.GodA == a && s.GodB == b);
        world.Diplomacy.Stances.Add(new GodStance { GodA = a, GodB = b, Status = status, SinceTick = 0, LastProposalTick = null });
        var guard = world.Creatures.Find(c => c.Id == rival.CreatureId)!;
        (guard.Health, guard.Needs.Hunger, guard.Needs.Fatigue) = (1f, 0f, 0f);
        guard.Position = new GroundPosition(village.Position.X + 120f, village.Position.Z);
        session.LocalCreature!.Health = 0.4f;
        session.LocalCreature.Position = village.Position;
        foreach (var other in world.Creatures.Where(c => c.Id != guard.Id && c.Id != session.LocalCreature.Id))
        {
            other.Position = new GroundPosition(5f, 5f);
        }

        return (session, rival, village, guard);
    }

    /// <summary>Runs whole seconds, gathering the events.</summary>
    private static List<WorldEvent> Run(GameSession session, int seconds)
    {
        session.RunTicks(seconds * 10);
        return [.. session.DrainEvents()];
    }
}
