using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Game.Creatures;
using Pantheon.Game.Disasters;
using Pantheon.Game.Politics;
using Pantheon.Simulation.Politics;

namespace Pantheon.Game.Minds.Agents;

/// <summary>
/// Reads what wakes the gods' and Creatures' minds from the world's events (ADR-072): each event that
/// matters to a god or a Creature becomes a hook for it, in words from its side, with the scribes'
/// moment when they have something to say of it. Every god and Creature concerned gets one; which of
/// them have minds is for the agents to decide.
/// </summary>
public static class HookReader
{
    /// <summary>The hooks in a frame's events.</summary>
    /// <param name="events">The events, in order.</param>
    /// <param name="world">The world, for names.</param>
    /// <param name="content">The content: the miracles, and how far a Creature sees.</param>
    /// <returns>Each hook with the mind it wakes.</returns>
    public static List<(Mind Mind, Hook Hook)> Read(IReadOnlyList<WorldEvent> events, WorldState world, GameContent content)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(content);
        var hooks = new List<(Mind, Hook)>();
        foreach (var worldEvent in events)
        {
            switch (worldEvent)
            {
                case CreatureWatched watched:
                    Watched(hooks, watched, world);
                    break;
                case DiplomacyChanged change:
                    Diplomacy(hooks, change, world);
                    break;
                case TreatySigned signed:
                    hooks.Add((Mind.God(signed.FirstGodId), new Hook(HookKind.Treaty, signed.Tick, Format("You and {0} signed a {1}.", GodName(world, signed.SecondGodId), PoliticsText.Name(signed.Kind)), OtherGodId: signed.SecondGodId)));
                    hooks.Add((Mind.God(signed.SecondGodId), new Hook(HookKind.Treaty, signed.Tick, Format("You and {0} signed a {1}.", GodName(world, signed.FirstGodId), PoliticsText.Name(signed.Kind)), OtherGodId: signed.FirstGodId)));
                    break;
                case TreatyEnded ended:
                    var broken = ended.BrokenByGodId is { } breaker ? Format(", broken by {0}", GodName(world, breaker)) : string.Empty;
                    hooks.Add((Mind.God(ended.FirstGodId), new Hook(HookKind.Treaty, ended.Tick, Format("Your {0} with {1} ended{2}.", PoliticsText.Name(ended.Kind), GodName(world, ended.SecondGodId), broken), OtherGodId: ended.SecondGodId)));
                    hooks.Add((Mind.God(ended.SecondGodId), new Hook(HookKind.Treaty, ended.Tick, Format("Your {0} with {1} ended{2}.", PoliticsText.Name(ended.Kind), GodName(world, ended.FirstGodId), broken), OtherGodId: ended.FirstGodId)));
                    break;
                case FollowingChanged following when world.Settlements.Find(s => s.Id == following.SettlementId) is { } village:
                    hooks.Add((Mind.God(following.GodId), new Hook(HookKind.Village, following.Tick, following.Gained ? Format("{0} now follows you.", village.Name) : Format("{0} no longer follows you.", village.Name), SettlementId: village.Id)));
                    break;
                case MiracleCast cast:
                    Cast(hooks, cast, world, content);
                    break;
                case SettlementDamaged damage when world.Settlements.Find(s => s.Id == damage.SettlementId) is { } struck:
                    Damaged(hooks, damage, struck, world, content.Creatures.PerceptionRadius);
                    break;
                case CreatureFightEnded fight:
                    Fought(hooks, fight, world);
                    break;
                case CreaturesMet met when met.Outcome != MeetingOutcome.PassedBy:
                    Met(hooks, met, met.CreatureId, met.OtherCreatureId, world);
                    Met(hooks, met, met.OtherCreatureId, met.CreatureId, world);
                    break;
                case CreatureChanged changed when world.Creatures.Find(c => c.Id == changed.CreatureId) is { } creature:
                    Changed(hooks, changed, creature, world);
                    break;
                case CreatureSpecialised calling:
                    hooks.Add((Mind.Creature(calling.CreatureId), new Hook(HookKind.Grew, calling.Tick, Format("You took the path of the {0}.", CreatureText.Path(calling.Specialisation).ToLowerInvariant()), "Grew")));
                    break;
                case GodSpoke spoke:
                    hooks.Add((Mind.God(spoke.ToGodId), new Hook(HookKind.Words, spoke.Tick, Format("{0} says to you: “{1}”", GodName(world, spoke.GodId), spoke.Text), OtherGodId: spoke.GodId)));
                    break;
                case GodsMet gods:
                    var at = world.Settlements.Find(s => s.Id == gods.SettlementId)?.Name is { } place ? " at " + place : string.Empty;
                    hooks.Add((Mind.God(gods.GodId), new Hook(HookKind.Met, gods.Tick, Format("You met {0}{1}.", GodName(world, gods.OtherGodId), at), "Met", gods.OtherGodId)));
                    hooks.Add((Mind.God(gods.OtherGodId), new Hook(HookKind.Met, gods.Tick, Format("You met {0}{1}.", GodName(world, gods.GodId), at), "Met", gods.GodId)));
                    break;
            }
        }

        return hooks;
    }

    /// <summary>A Creature in a god's lands: the god's, the visitor god's, the visitor's and the guard's hooks.</summary>
    private static void Watched(List<(Mind, Hook)> hooks, CreatureWatched watched, WorldState world)
    {
        var visitor = CreatureName(world, watched.CreatureId);
        var village = world.Settlements.Find(s => s.Id == watched.SettlementId)?.Name ?? "a village";
        var land = GodName(world, watched.GodId);
        var owner = GodName(world, watched.VisitorGodId);
        var answer = watched.Answer switch
        {
            WatchAnswer.Welcome => "welcomed it",
            WatchAnswer.Watch => "sent its Creature to watch it, and holds the trespass against its god",
            WatchAnswer.Repel => "set its Creature on it to drive it out",
            WatchAnswer.Unanswered => "was told of it",
        };
        var voice = watched.Answer == WatchAnswer.Unanswered ? string.Empty : "Visitor." + watched.Answer;
        hooks.Add((Mind.God(watched.GodId), new Hook(HookKind.Visitor, watched.Tick, Format("{0}, {1}'s Creature, came into {2}, your village. Your instinct {3}.", visitor, owner, village, answer), voice, watched.VisitorGodId, watched.CreatureId, watched.SettlementId)));
        hooks.Add((Mind.God(watched.VisitorGodId), new Hook(HookKind.Answered, watched.Tick, Format("{0} noticed your Creature {1} in {2}, and {3}.", land, visitor, village, answer), OtherGodId: watched.GodId, OtherCreatureId: watched.CreatureId, SettlementId: watched.SettlementId)));
        hooks.Add((Mind.Creature(watched.CreatureId), new Hook(HookKind.Noticed, watched.Tick, Format("{0} noticed you in {1}, its village, and {2}.", land, village, answer), "Noticed.Watched", watched.GodId, watched.GuardId, watched.SettlementId)));
        if (watched.GuardId is { } guard)
        {
            hooks.Add((Mind.Creature(guard), new Hook(HookKind.Noticed, watched.Tick, Format("Your god sent you to {0} {1}, {2}'s Creature, in {3}.", watched.Answer == WatchAnswer.Repel ? "drive out" : watched.Answer == WatchAnswer.Welcome ? "greet" : "watch", visitor, owner, village), "Noticed.Guard", watched.VisitorGodId, watched.CreatureId, watched.SettlementId)));
        }
    }

    /// <summary>A diplomatic move: the actor's and the other god's hooks, each with what the scribes would say to the other.</summary>
    private static void Diplomacy(List<(Mind, Hook)> hooks, DiplomacyChanged change, WorldState world)
    {
        var words = change.Kind switch
        {
            DiplomaticCommandKind.OfferAlliance => "offered an alliance to",
            DiplomaticCommandKind.BreakAlliance => "broke the alliance with",
            DiplomaticCommandKind.DeclareWar => "declared war on",
            DiplomaticCommandKind.RequestPeace => "asked for peace from",
            DiplomaticCommandKind.Accept => Format("accepted the {0} of", Proposal(change.Answered)),
            DiplomaticCommandKind.Decline => Format("declined the {0} of", Proposal(change.Answered)),
            DiplomaticCommandKind.ProposeTreaty => "proposed a treaty to",
            DiplomaticCommandKind.BreakTreaty => "broke a treaty with",
            DiplomaticCommandKind.Request => "asked a favour of",
            DiplomaticCommandKind.Absorb => "took into its own following",
            DiplomaticCommandKind.DemandSurrender => "demanded the surrender of",
        };
        var formed = change.Kind == DiplomaticCommandKind.Accept && change.Status == DiplomaticStatus.Alliance;
        var peace = change.Kind == DiplomaticCommandKind.Accept && change.Answered == ProposalKind.Peace;
        var said = change.Kind switch
        {
            DiplomaticCommandKind.DeclareWar => "War.Declared",
            DiplomaticCommandKind.OfferAlliance => "Alliance.Offered",
            DiplomaticCommandKind.RequestPeace => "Peace.Asked",
            DiplomaticCommandKind.BreakAlliance => "Betraying",
            _ => formed ? "Alliance.Formed" : peace ? "Peace.Made" : string.Empty,
        };
        var heard = change.Kind switch
        {
            DiplomaticCommandKind.DeclareWar => "War.Answered",
            DiplomaticCommandKind.BreakAlliance => "Betrayed",
            _ => formed ? "Alliance.Formed" : peace ? "Peace.Made" : string.Empty,
        };
        var other = GodName(world, change.OtherGodId);
        var actor = GodName(world, change.ActorGodId);
        hooks.Add((Mind.God(change.ActorGodId), new Hook(HookKind.Diplomacy, change.Tick, Format("You {0} {1}.", words, other), said, change.OtherGodId)));
        hooks.Add((Mind.God(change.OtherGodId), new Hook(HookKind.Diplomacy, change.Tick, Format("{0} {1} you.", actor, words), heard, change.ActorGodId)));
    }

    /// <summary>A harmful miracle on villages: a hook for each god whose village it struck, naming the caster unless it was veiled.</summary>
    private static void Cast(List<(Mind, Hook)> hooks, MiracleCast cast, WorldState world, GameContent content)
    {
        if (content.Miracles.Find(cast.MiracleId) is not { } miracle || !Simulation.Culture.CultureModel.Harms(miracle))
        {
            return;
        }

        foreach (var id in cast.AffectedSettlementIds)
        {
            if (world.Settlements.Find(s => s.Id == id) is { } village && TreatyModel.Leader(village) is { } leader && leader != cast.GodId)
            {
                var caster = cast.Veiled ? "An unseen god" : GodName(world, cast.GodId);
                hooks.Add((Mind.God(leader), new Hook(HookKind.Struck, cast.Tick, Format("{0} struck {1}, your village, with {2}{3}.", caster, village.Name, miracle.Name, cast.Deaths > 0 ? Format(": {0} dead", cast.Deaths) : string.Empty),
                    cast.Veiled ? string.Empty : "Struck", cast.Veiled ? null : cast.GodId, SettlementId: village.Id)));
            }
        }
    }

    /// <summary>A disaster on a village: its god's hook, and its god's Creatures' near it.</summary>
    private static void Damaged(List<(Mind, Hook)> hooks, SettlementDamaged damage, Domain.Settlements.SettlementState village, WorldState world, float sight)
    {
        if (TreatyModel.Leader(village) is not { } leader)
        {
            return;
        }

        var what = DisasterText.Kind(damage.Cause);
        hooks.Add((Mind.God(leader), new Hook(HookKind.Struck, damage.Tick, Format("{0} struck {1}, your village: {2} houses lost, {3} dead.", Capital(what), village.Name, damage.HousesLost, damage.Deaths), SettlementId: village.Id)));
        foreach (var creature in world.Creatures)
        {
            var dx = creature.Position.X - village.Position.X;
            var dz = creature.Position.Z - village.Position.Z;
            if (creature.GodId == leader && (dx * dx) + (dz * dz) <= sight * sight)
            {
                hooks.Add((Mind.Creature(creature.Id), new Hook(HookKind.Home, damage.Tick, Format("{0} struck {1}, a village of your god's.", Capital(what), village.Name), "Home.Struck", SettlementId: village.Id)));
            }
        }
    }

    /// <summary>A fight's end: both Creatures' hooks and both gods'.</summary>
    private static void Fought(List<(Mind, Hook)> hooks, CreatureFightEnded fight, WorldState world)
    {
        if (world.Creatures.Find(c => c.Id == fight.CreatureId) is not { } winner || world.Creatures.Find(c => c.Id == fight.OtherCreatureId) is not { } loser)
        {
            return;
        }

        var drew = fight.Outcome == FightOutcome.Drew;
        var fled = fight.Outcome == FightOutcome.BrokeOff;
        var (won, lost) = drew ? ("fought {0} to a draw", "fought {0} to a draw")
            : fled ? ("drove off {0}, which ran", "ran from {0}")
            : fight.Outcome == FightOutcome.Banished ? ("beat {0}, which is banished a while", "was beaten by {0} and is banished a while")
            : ("beat {0} and let it go", "was beaten by {0} and let go");
        hooks.Add((Mind.Creature(winner.Id), new Hook(HookKind.Fight, fight.Tick, "You " + Format(won, winner.Name == loser.Name ? "it" : loser.Name) + ".", drew ? "Fight.Drew" : "Fight.Won", loser.GodId, loser.Id)));
        hooks.Add((Mind.Creature(loser.Id), new Hook(HookKind.Fight, fight.Tick, "You " + Format(lost, winner.Name) + ".", drew ? "Fight.Drew" : fled ? "Fight.Fled" : "Fight.Lost", winner.GodId, winner.Id)));
        hooks.Add((Mind.God(winner.GodId), new Hook(HookKind.CreatureFight, fight.Tick, Format("Your Creature {0} {1}, {2}'s Creature.", winner.Name, Format(won, loser.Name), GodName(world, loser.GodId)), drew ? string.Empty : "Fight.Won", loser.GodId, winner.Id)));
        hooks.Add((Mind.God(loser.GodId), new Hook(HookKind.CreatureFight, fight.Tick, Format("Your Creature {0} {1}, {2}'s Creature.", loser.Name, Format(lost, winner.Name), GodName(world, winner.GodId)), drew ? string.Empty : "Fight.Lost", winner.GodId, loser.Id)));
    }

    /// <summary>A meeting of Creatures, from one of them.</summary>
    private static void Met(List<(Mind, Hook)> hooks, CreaturesMet met, int creatureId, int otherId, WorldState world)
    {
        if (world.Creatures.Find(c => c.Id == otherId) is not { } other)
        {
            return;
        }

        var how = met.Outcome switch
        {
            MeetingOutcome.PassedBy => "you passed each other by",
            MeetingOutcome.Greeted => "you greeted each other",
            MeetingOutcome.Played => "you played together",
            MeetingOutcome.Fled => "one of you ran",
            MeetingOutcome.Fought => "you fought",
        };
        var voice = met.Friends ? "Meeting.Friend" : met.GodsAtWar ? "Meeting.Foe" : "Meeting.Stranger";
        var stance = met.Friends ? ", a friend" : met.GodsAtWar ? ", whose god is at war with yours" : string.Empty;
        hooks.Add((Mind.Creature(creatureId), new Hook(HookKind.Meeting, met.Tick, Format("You met {0}, {1}'s Creature{2}: {3}.", other.Name, GodName(world, other.GodId), stance, how), voice, other.GodId, other.Id)));
    }

    /// <summary>A Creature rewarded, punished or grown.</summary>
    private static void Changed(List<(Mind, Hook)> hooks, CreatureChanged changed, CreatureState creature, WorldState world)
    {
        var god = GodName(world, creature.GodId);
        var hook = changed.Change switch
        {
            CreatureChange.Rewarded => new Hook(HookKind.Judged, changed.Tick, Format("Your god {0} rewarded you for what you did.", god), "Judged.Rewarded"),
            CreatureChange.Punished => new Hook(HookKind.Judged, changed.Tick, Format("Your god {0} punished you for what you did.", god), "Judged.Punished"),
            CreatureChange.LevelledUp => new Hook(HookKind.Grew, changed.Tick, Format("You grew: you are level {0} now.", creature.Level), "Grew"),
            CreatureChange.Banished or CreatureChange.Returned => null,
        };
        if (hook is not null)
        {
            hooks.Add((Mind.Creature(creature.Id), hook));
        }
    }

    /// <summary>A proposal's kind in a word or two.</summary>
    private static string Proposal(ProposalKind? kind) => kind switch
    {
        ProposalKind.Alliance => "alliance",
        ProposalKind.Peace => "peace",
        ProposalKind.Treaty => "treaty",
        ProposalKind.Request => "request",
        ProposalKind.Surrender => "surrender terms",
        null => "proposal",
    };

    /// <summary>A god's name.</summary>
    private static string GodName(WorldState world, int godId) => world.Gods.Find(g => g.Id == godId)?.Name ?? "a god";

    /// <summary>A Creature's name.</summary>
    private static string CreatureName(WorldState world, int creatureId) => world.Creatures.Find(c => c.Id == creatureId)?.Name ?? "a Creature";

    /// <summary>A text with its first letter capital.</summary>
    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>Formats with the invariant culture.</summary>
    private static string Format(string format, params object?[] values) => string.Format(CultureInfo.InvariantCulture, format, values);
}
