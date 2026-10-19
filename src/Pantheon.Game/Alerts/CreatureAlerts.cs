using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Alerts about Creatures dealing with each other (spec §27–28, §59, ADR-017): the player's Creature
/// playing, fleeing, being attacked or betrayed, and how its fights end; and fights between rivals'
/// Creatures that end with one beaten. Part of <see cref="AlertFeed"/>.
/// </summary>
internal static class CreatureAlerts
{
    /// <summary>The alert for a meeting or a fight's end, or <c>null</c> when it does not concern the player.</summary>
    /// <param name="worldEvent">The event.</param>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The alert's key, text, severity and place, or <c>null</c>.</returns>
    public static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId) => worldEvent switch
    {
        CreaturesMet met when Find(world, met.CreatureId) is { } actor && Find(world, met.OtherCreatureId) is { } other =>
            DescribeMeeting(met, actor, other, world, localGodId),
        CreatureFightEnded ended when Find(world, ended.CreatureId) is { } winner && Find(world, ended.OtherCreatureId) is { } loser =>
            DescribeEnd(ended, winner, loser, world, localGodId),
        CreatureDefending defending when Find(world, defending.CreatureId) is { } guard && guard.GodId == localGodId
            && world.Settlements.Find(s => s.Id == defending.SettlementId) is { } village =>
            (Key("defend", defending.CreatureId), Format("{0} goes by itself to defend {1} from {2}.", guard.Name, village.Name, world.GreatEvents.Beast?.Name ?? "the beast"), AlertSeverity.Good, village.Position),
        CreatureGaveUp given when Find(world, given.CreatureId) is { } quitter && quitter.GodId == localGodId =>
            (Key("gave-up", given.CreatureId), Format("{0} gives up {1}: {2}.", quitter.Name, Creatures.CreatureText.Doing(given.Action, Creatures.CreatureText.Concerning(world, given.SettlementId, null)), Creatures.CreatureText.GaveUp(given.Reason)),
                AlertSeverity.Info, world.Settlements.Find(s => s.Id == given.SettlementId)?.Position),
        _ => null,
    };

    /// <summary>A meeting of the player's Creature: play, flight or an attack (greetings and passing by are not news).</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeMeeting(CreaturesMet met, CreatureState actor, CreatureState other, WorldState world, int? me)
    {
        var mineActs = actor.GodId == me;
        if (me is null || (!mineActs && other.GodId != me))
        {
            return null;
        }

        var (mine, theirs) = mineActs ? (actor, other) : (other, actor);
        var key = Key("met" + met.Outcome, (actor.Id * 100) + other.Id);
        var where = (GroundPosition?)mine.Position;
        return met.Outcome switch
        {
            MeetingOutcome.Played when met.GodsAtWar => (key, Format("{0} played with {1} though your gods are at war.", mine.Name, Called(world, theirs)), AlertSeverity.Warning, where),
            MeetingOutcome.Played => (key, Format("{0} played with {1}.", mine.Name, Called(world, theirs)), AlertSeverity.Good, where),
            MeetingOutcome.Fled when mineActs => (key, Format("{0} fled from {1}.", mine.Name, Called(world, theirs)), AlertSeverity.Warning, where),
            MeetingOutcome.Fled => (key, Format("{0} fled from {1}.", Called(world, theirs), mine.Name), AlertSeverity.Good, where),
            MeetingOutcome.Fought when mineActs && met.Betrayal => (key, Format("{0} turned on its friend {1}.", mine.Name, Called(world, theirs)), AlertSeverity.Warning, where),
            MeetingOutcome.Fought when mineActs => (key, Format("{0} attacked {1}.", mine.Name, Called(world, theirs)), AlertSeverity.Info, where),
            MeetingOutcome.Fought when met.Betrayal => (key, Format("{0} turned on {1}, its friend.", Called(world, theirs), mine.Name), AlertSeverity.Danger, where),
            MeetingOutcome.Fought => (key, Format("{0} attacked {1}.", Called(world, theirs), mine.Name), AlertSeverity.Danger, where),
            MeetingOutcome.PassedBy or MeetingOutcome.Greeted when met.GodsAtWar && met.Friends =>
                (key, Format("{0} would not fight its friend {1}, though your gods are at war.", mine.Name, Called(world, theirs)), AlertSeverity.Warning, where),
            MeetingOutcome.PassedBy or MeetingOutcome.Greeted => null,
        };
    }

    /// <summary>How a fight ended: the player's Creature's, or one between rivals' Creatures with one beaten.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeEnd(CreatureFightEnded ended, CreatureState winner, CreatureState loser, WorldState world, int? me)
    {
        var key = Key("fight" + ended.Outcome, (winner.Id * 100) + loser.Id);
        if (winner.GodId == me)
        {
            return ended.Outcome switch
            {
                FightOutcome.Banished => (key, Format("{0} beat {1}: it is banished for a while.", winner.Name, Called(world, loser)), AlertSeverity.Good, winner.Position),
                FightOutcome.Spared => (key, Format("{0} beat {1} and let it go.", winner.Name, Called(world, loser)), AlertSeverity.Good, winner.Position),
                FightOutcome.Drew => (key, Format("{0} and {1} fought to a draw.", winner.Name, Called(world, loser)), AlertSeverity.Info, winner.Position),
                FightOutcome.BrokeOff => (key, Format("{0} ran from its fight with {1}.", Called(world, loser), winner.Name), AlertSeverity.Good, winner.Position),
            };
        }

        if (loser.GodId == me)
        {
            return ended.Outcome switch
            {
                FightOutcome.Banished => (key, Format("{0} beat {1}: {1} is banished for a while and scarred.", Called(world, winner), loser.Name), AlertSeverity.Danger, winner.Position),
                FightOutcome.Spared => (key, Format("{0} beat {1} but let it go.", Called(world, winner), loser.Name), AlertSeverity.Warning, loser.Position),
                FightOutcome.Drew => (key, Format("{0} and {1} fought to a draw.", loser.Name, Called(world, winner)), AlertSeverity.Info, loser.Position),
                FightOutcome.BrokeOff => (key, Format("{0} broke off its fight with {1}.", loser.Name, Called(world, winner)), AlertSeverity.Info, loser.Position),
            };
        }

        return ended.Outcome is FightOutcome.Banished or FightOutcome.Spared
            ? (key, Format("{0} beat {1}.", Called(world, winner), Called(world, loser)), AlertSeverity.Info, winner.Position)
            : null;
    }

    /// <summary>A rival's Creature with its god: "Gristle (Vael's Creature)".</summary>
    private static string Called(WorldState world, CreatureState creature) =>
        GodStanding.Find(world, creature.GodId) is { } god ? Format("{0} ({1}'s Creature)", creature.Name, god.Name) : creature.Name;

    /// <summary>A Creature by id.</summary>
    private static CreatureState? Find(WorldState world, int id) => world.Creatures.Find(c => c.Id == id);

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
