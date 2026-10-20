using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Game.Minds.Agents;
using Pantheon.Simulation.Gods;
using Pantheon.Simulation.Politics;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Minds.Agents;

/// <summary>What wakes the gods' and Creatures' minds (ADR-072): each event becomes hooks for those it concerns, in words from their side, with the scribes' moment.</summary>
public sealed class HookReaderTests
{
    [Fact]
    public void ACreatureInAGodsLands_WakesTheGod_TheVisitorsGod_TheVisitorAndTheGuard()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var world = session.World;
        var visitor = world.Creatures.Single(c => c.GodId == me);
        var guard = world.Creatures.Single(c => c.GodId == rival.Id);
        var village = GodPlanner.Home(world, rival.Id)!;

        var hooks = HookReader.Read([new CreatureWatched { Tick = 9, GodId = rival.Id, VisitorGodId = me, CreatureId = visitor.Id, SettlementId = village.Id, Answer = WatchAnswer.Repel, GuardId = guard.Id }], world, session.Content);

        var land = Assert.Single(hooks, h => h.Mind == Mind.God(rival.Id)).Hook;
        Assert.Equal((HookKind.Visitor, "Visitor.Repel", me), (land.Kind, land.Voice, land.OtherGodId));
        Assert.Contains(visitor.Name, land.Text);
        Assert.Contains(village.Name, land.Text);
        Assert.Equal(HookKind.Answered, Assert.Single(hooks, h => h.Mind == Mind.God(me)).Hook.Kind);
        Assert.Equal("Noticed.Watched", Assert.Single(hooks, h => h.Mind == Mind.Creature(visitor.Id)).Hook.Voice);
        Assert.Equal("Noticed.Guard", Assert.Single(hooks, h => h.Mind == Mind.Creature(guard.Id)).Hook.Voice);
    }

    [Theory]
    [InlineData(DiplomaticCommandKind.DeclareWar, DiplomaticStatus.War, "War.Declared", "War.Answered")]
    [InlineData(DiplomaticCommandKind.BreakAlliance, DiplomaticStatus.Neutral, "Betraying", "Betrayed")]
    [InlineData(DiplomaticCommandKind.Accept, DiplomaticStatus.Alliance, "Alliance.Formed", "Alliance.Formed")]
    [InlineData(DiplomaticCommandKind.OfferAlliance, DiplomaticStatus.Neutral, "Alliance.Offered", "")]
    public void ADiplomaticMove_WakesBothGods_EachWithWhatItWouldSayToTheOther(DiplomaticCommandKind kind, DiplomaticStatus status, string actorSays, string otherSays)
    {
        var (session, rival, me) = AgentWorlds.Setup();

        var hooks = HookReader.Read([new DiplomacyChanged { Tick = 1, ActorGodId = rival.Id, OtherGodId = me, Kind = kind, Status = status, Betrayal = kind == DiplomaticCommandKind.BreakAlliance, Answered = kind == DiplomaticCommandKind.Accept ? ProposalKind.Alliance : null }], session.World, session.Content);

        var actor = Assert.Single(hooks, h => h.Mind == Mind.God(rival.Id)).Hook;
        var other = Assert.Single(hooks, h => h.Mind == Mind.God(me)).Hook;
        Assert.Equal((HookKind.Diplomacy, actorSays, me), (actor.Kind, actor.Voice, actor.OtherGodId));
        Assert.Equal((otherSays, rival.Id), (other.Voice, other.OtherGodId));
        Assert.StartsWith("You ", actor.Text);
        Assert.StartsWith(rival.Name, other.Text);
    }

    [Theory]
    [InlineData(FightOutcome.Banished, "Fight.Won", "Fight.Lost")]
    [InlineData(FightOutcome.BrokeOff, "Fight.Won", "Fight.Fled")]
    [InlineData(FightOutcome.Drew, "Fight.Drew", "Fight.Drew")]
    public void AFightsEnd_WakesBothCreaturesAndBothGods(FightOutcome outcome, string winnerSays, string loserSays)
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var world = session.World;
        var winner = world.Creatures.Single(c => c.GodId == me);
        var loser = world.Creatures.Single(c => c.GodId == rival.Id);

        var hooks = HookReader.Read([new CreatureFightEnded { Tick = 1, CreatureId = winner.Id, OtherCreatureId = loser.Id, Outcome = outcome }], world, session.Content);

        Assert.Equal(winnerSays, Assert.Single(hooks, h => h.Mind == Mind.Creature(winner.Id)).Hook.Voice);
        Assert.Equal(loserSays, Assert.Single(hooks, h => h.Mind == Mind.Creature(loser.Id)).Hook.Voice);
        var losing = Assert.Single(hooks, h => h.Mind == Mind.God(rival.Id)).Hook;
        Assert.Equal((HookKind.CreatureFight, outcome == FightOutcome.Drew ? string.Empty : "Fight.Lost", me), (losing.Kind, losing.Voice, losing.OtherGodId));
    }

    [Fact]
    public void CreaturesThatOnlyPassBy_WakeNoOne_ButEnemiesThatFightAreFoes()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var world = session.World;
        var mine = world.Creatures.Single(c => c.GodId == me);
        var theirs = world.Creatures.Single(c => c.GodId == rival.Id);
        CreaturesMet Met(MeetingOutcome outcome) => new() { Tick = 1, CreatureId = mine.Id, OtherCreatureId = theirs.Id, Outcome = outcome, Betrayal = false, GodsAtWar = true, Friends = false };

        Assert.Empty(HookReader.Read([Met(MeetingOutcome.PassedBy)], world, session.Content));
        var hooks = HookReader.Read([Met(MeetingOutcome.Fought)], world, session.Content);
        Assert.Equal(2, hooks.Count);
        Assert.All(hooks, h => Assert.Equal("Meeting.Foe", h.Hook.Voice));
    }

    [Fact]
    public void AHarmfulMiracle_WakesTheGodWhoseVillageItStruck_NamingTheCasterUnlessVeiled()
    {
        var (session, rival, me) = AgentWorlds.Setup();
        var village = GodPlanner.Home(session.World, rival.Id)!;
        MiracleCast Cast(string id, bool veiled) => new() { Tick = 1, GodId = me, MiracleId = id, Target = village.Position, Cost = 70f, AffectedSettlementIds = [village.Id], Deaths = 0, Veiled = veiled };

        var open = Assert.Single(HookReader.Read([Cast("lightning", veiled: false)], session.World, session.Content)).Hook;
        var hidden = Assert.Single(HookReader.Read([Cast("lightning", veiled: true)], session.World, session.Content)).Hook;

        Assert.Equal((HookKind.Struck, "Struck", me), (open.Kind, open.Voice, open.OtherGodId));
        Assert.Contains(session.World.Gods.Single(g => g.Id == me).Name, open.Text);
        Assert.Equal((string.Empty, (int?)null), (hidden.Voice, hidden.OtherGodId));
        Assert.Empty(HookReader.Read([Cast("heal", veiled: false)], session.World, session.Content));
        Assert.Equal(rival.Id, TreatyModel.Leader(village));
    }

    [Fact]
    public void AGodsJudgementAndGrowth_WakeItsCreature()
    {
        var (session, _, me) = AgentWorlds.Setup();
        var creature = session.World.Creatures.Single(c => c.GodId == me);
        IEnumerable<string> Voices(CreatureChange change) => HookReader.Read([new CreatureChanged { Tick = 1, CreatureId = creature.Id, Change = change }], session.World, session.Content).Select(h => h.Hook.Voice);

        Assert.Equal(["Judged.Rewarded"], Voices(CreatureChange.Rewarded));
        Assert.Equal(["Judged.Punished"], Voices(CreatureChange.Punished));
        Assert.Equal(["Grew"], Voices(CreatureChange.LevelledUp));
        Assert.Empty(Voices(CreatureChange.Banished));
    }

    [Fact]
    public void AGodsWords_WakeTheGodSpokenTo()
    {
        var (session, rival, me) = AgentWorlds.Setup();

        var hook = Assert.Single(HookReader.Read([new GodSpoke { Tick = 1, GodId = me, ToGodId = rival.Id, Text = "Stand with me." }], session.World, session.Content));

        Assert.Equal((Mind.God(rival.Id), HookKind.Words, me), (hook.Mind, hook.Hook.Kind, hook.Hook.OtherGodId));
        Assert.Contains("Stand with me.", hook.Hook.Text);
    }
}
