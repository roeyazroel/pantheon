using System;
using System.Collections.Generic;
using System.Globalization;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Alerts;

/// <summary>
/// Turns world events into alerts for the local player (spec §59). It keeps only alerts that
/// concern the player's god or its followers, never repeats the same alert within
/// <see cref="RepeatSeconds"/> of simulated time, and keeps the most urgent recent alerts.
/// </summary>
public sealed class AlertFeed
{
    private readonly List<Alert> _recent = [];
    private readonly Dictionary<string, double> _lastRaised = new(StringComparer.Ordinal);

    /// <summary>Creates an empty feed.</summary>
    /// <param name="repeatSeconds">Simulated seconds before the same alert may be raised again.</param>
    /// <param name="capacity">Most alerts kept in <see cref="Recent"/>.</param>
    public AlertFeed(double repeatSeconds = 30.0, int capacity = 6)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(repeatSeconds);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        RepeatSeconds = repeatSeconds;
        Capacity = capacity;
    }

    /// <summary>Simulated seconds before the same alert may be raised again.</summary>
    public double RepeatSeconds { get; }

    /// <summary>Most alerts kept.</summary>
    public int Capacity { get; }

    /// <summary>Recent alerts, newest first; when full, the least urgent oldest alert makes room.</summary>
    public IReadOnlyList<Alert> Recent => _recent;

    /// <summary>Forgets every alert, for example when the world is replaced.</summary>
    public void Clear()
    {
        _recent.Clear();
        _lastRaised.Clear();
    }

    /// <summary>Raises the alerts for a batch of events.</summary>
    /// <param name="events">Events since the last call, oldest first.</param>
    /// <param name="world">Current world.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <param name="simulationSeconds">Current simulated time.</param>
    /// <returns>The alerts raised, oldest first.</returns>
    public IReadOnlyList<Alert> Push(IReadOnlyList<WorldEvent> events, WorldState world, int? localGodId, double simulationSeconds)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(world);
        var raised = new List<Alert>();
        foreach (var worldEvent in events)
        {
            if (Describe(worldEvent, world, localGodId) is not { } entry)
            {
                continue;
            }

            if (_lastRaised.TryGetValue(entry.Key, out var last) && simulationSeconds - last < RepeatSeconds)
            {
                continue;
            }

            _lastRaised[entry.Key] = simulationSeconds;
            var alert = new Alert(entry.Text, entry.Severity, entry.Location, simulationSeconds);
            raised.Add(alert);
            Add(alert);
        }

        return raised;
    }

    /// <summary>Adds an alert at the front, dropping the least urgent, oldest alert when full.</summary>
    private void Add(Alert alert)
    {
        _recent.Insert(0, alert);
        if (_recent.Count <= Capacity)
        {
            return;
        }

        var drop = _recent.Count - 1;
        for (var i = _recent.Count - 1; i >= 0; i--)
        {
            if (_recent[i].Severity < _recent[drop].Severity)
            {
                drop = i;
            }
        }

        _recent.RemoveAt(drop);
    }

    /// <summary>The alert for an event, or <c>null</c> when the event does not concern the player.</summary>
    private static (string Key, string Text, AlertSeverity Severity, GroundPosition? Location)? Describe(WorldEvent worldEvent, WorldState world, int? localGodId)
    {
        switch (worldEvent)
        {
            case FamineStarted famine when Settlement(world, famine.SettlementId) is { } settlement && Concerns(settlement, localGodId):
                return (Key("famine", settlement.Id), Format("{0} is starving.", settlement.Name), AlertSeverity.Warning, settlement.Position);
            case FollowingChanged change when Settlement(world, change.SettlementId) is { } settlement:
                return DescribeFollowing(change, settlement, world, localGodId);
            case RankAchieved rank when rank.GodId == localGodId:
                return (Key("rank", (int)rank.Rank), Format("You are now a {0}.", DivineText.Rank(rank.Rank)), AlertSeverity.Good, null);
            case RankAchieved rank when GodStanding.Find(world, rank.GodId) is { } god:
                return (Key("rank", (rank.GodId * 100) + (int)rank.Rank), Format("{0} is now a {1}.", god.Name, DivineText.Rank(rank.Rank)), AlertSeverity.Info, null);
            case CreatureActed acted when world.Creatures.Find(c => c.Id == acted.CreatureId) is { } creature:
                return DescribeDeed(acted, creature, world, localGodId);
            case MiracleCast cast when cast.GodId != localGodId:
                return DescribeRivalCast(cast, world, localGodId);
            case DominanceChanged dominance when GodStanding.Find(world, dominance.GodId) is { } dominant:
                return DescribeDominance(dominance, dominant, localGodId);
            case DiplomacyChanged change:
                return DescribeDiplomacy(change, world, localGodId);
            case VictoryAchieved victory when GodStanding.Find(world, victory.GodId) is { } winner:
                return victory.GodId == localGodId
                    ? (Key("victory", winner.Id), "You have won: your followers held the world's belief for ten minutes.", AlertSeverity.Good, null)
                    : (Key("victory", winner.Id), Format("{0} has won the world's belief.", winner.Name), AlertSeverity.Danger, null);
            case CreatureChanged changed when world.Creatures.Find(c => c.Id == changed.CreatureId) is { } creature && creature.GodId == localGodId:
                return DescribeChange(changed, creature);
            default:
                return null;
        }
    }

    /// <summary>A diplomatic move that concerns the player (spec §59: "Vael proposes an alliance").</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeDiplomacy(DiplomacyChanged change, WorldState world, int? localGodId)
    {
        if (change.ActorGodId == localGodId || (change.OtherGodId != localGodId && change.Kind != DiplomaticCommandKind.DeclareWar && !change.Betrayal))
        {
            return null;
        }

        var actor = GodStanding.Find(world, change.ActorGodId)?.Name ?? "A god";
        var key = Key("diplomacy" + change.Kind, (change.ActorGodId * 100) + change.OtherGodId);
        if (change.OtherGodId != localGodId)
        {
            var victim = GodStanding.Find(world, change.OtherGodId)?.Name ?? "a god";
            return (key, change.Betrayal ? Format("{0} betrayed {1}.", actor, victim) : Format("{0} declared war on {1}.", actor, victim), AlertSeverity.Info, null);
        }

        return change.Kind switch
        {
            DiplomaticCommandKind.OfferAlliance => (key, Format("{0} proposes an alliance (P to answer).", actor), AlertSeverity.Warning, null),
            DiplomaticCommandKind.RequestPeace => (key, Format("{0} asks for peace (P to answer).", actor), AlertSeverity.Warning, null),
            DiplomaticCommandKind.BreakAlliance => (key, Format("{0} has broken your alliance!", actor), AlertSeverity.Danger, null),
            DiplomaticCommandKind.DeclareWar => (key, change.Betrayal ? Format("{0} betrayed your alliance and declared war!", actor) : Format("{0} has declared war on you!", actor), AlertSeverity.Danger, null),
            DiplomaticCommandKind.Accept => (key, change.Status == DiplomaticStatus.Alliance ? Format("{0} accepted your alliance.", actor) : Format("{0} accepted peace.", actor), AlertSeverity.Good, null),
            DiplomaticCommandKind.Decline => (key, Format("{0} declined your proposal.", actor), AlertSeverity.Info, null),
        };
    }

    /// <summary>A rival's miracle on a settlement where the player has followers.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeRivalCast(MiracleCast cast, WorldState world, int? localGodId)
    {
        var rival = GodStanding.Find(world, cast.GodId)?.Name ?? "A rival";
        foreach (var id in cast.AffectedSettlementIds)
        {
            if (Settlement(world, id) is { } settlement && Concerns(settlement, localGodId))
            {
                var deaths = cast.Deaths > 0 ? Format(": {0} died", cast.Deaths) : string.Empty;
                var severity = cast.Deaths > 0 ? AlertSeverity.Danger : AlertSeverity.Warning;
                return (Key("rival" + cast.MiracleId, (cast.GodId * 1000) + id), Format("{0} worked a miracle ({1}) on {2}{3}.", rival, cast.MiracleId.Replace('_', ' '), settlement.Name, deaths), severity, settlement.Position);
            }
        }

        return null;
    }

    /// <summary>A god began or ceased to dominate the world's belief.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeDominance(DominanceChanged dominance, Domain.Gods.GodState god, int? localGodId)
    {
        var key = Key(dominance.Started ? "dominant" : "contested", god.Id);
        if (god.Id == localGodId)
        {
            return dominance.Started
                ? (key, "You hold most of the world's belief: keep it for ten minutes to win.", AlertSeverity.Good, null)
                : (key, "You no longer hold most of the world's belief.", AlertSeverity.Warning, null);
        }

        return dominance.Started
            ? (key, Format("{0} holds most of the world's belief: it wins in ten minutes unless you stop it.", god.Name), AlertSeverity.Danger, null)
            : (key, Format("{0} no longer holds most of the world's belief.", god.Name), AlertSeverity.Good, null);
    }

    /// <summary>A Creature did something notable to a settlement (spec §59: "Your Creature attacked neutral civilians"); a rival's Creature only where the player has followers.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeDeed(CreatureActed acted, Domain.Creatures.CreatureState creature, WorldState world, int? localGodId)
    {
        if (acted.SettlementId is not { } id || Settlement(world, id) is not { } settlement)
        {
            return null;
        }

        if (creature.GodId != localGodId)
        {
            var owner = GodStanding.Find(world, creature.GodId)?.Name ?? "a rival";
            return acted.Action == CreatureAction.Attack && Concerns(settlement, localGodId)
                ? (Key("rivaldeed", (creature.Id * 1000) + id), Format("{0}, {1}'s Creature, attacked {2}: {3} died.", creature.Name, owner, settlement.Name, acted.Deaths), AlertSeverity.Danger, settlement.Position)
                : null;
        }

        var key = Key("deed" + acted.Action, id);
        return acted.Action switch
        {
            CreatureAction.Attack => (key, Format("{0} attacked {1}: {2} died.", creature.Name, settlement.Name, acted.Deaths), AlertSeverity.Danger, settlement.Position),
            CreatureAction.EatFromStores => (key, Format("{0} ate {1:0} food from the stores of {2}.", creature.Name, -acted.Food, settlement.Name), AlertSeverity.Warning, settlement.Position),
            CreatureAction.Feed => (key, Format("{0} brought {1:0} food to {2}.", creature.Name, acted.Food, settlement.Name), AlertSeverity.Good, settlement.Position),
            CreatureAction.HelpBuild => (key, Format("{0} helped {1} build.", creature.Name, settlement.Name), AlertSeverity.Info, settlement.Position),
            _ => null,
        };
    }

    /// <summary>The player's Creature levelled up, was banished or came back.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeChange(CreatureChanged changed, Domain.Creatures.CreatureState creature) => changed.Change switch
    {
        CreatureChange.LevelledUp => (Key("level", creature.Level), Format("{0} reached level {1}.", creature.Name, creature.Level), AlertSeverity.Good, creature.Position),
        CreatureChange.Banished => (Key("banished", creature.Id), Format("{0} starved and was banished. It will return home.", creature.Name), AlertSeverity.Danger, null),
        CreatureChange.Returned => (Key("returned", creature.Id), Format("{0} has returned.", creature.Name), AlertSeverity.Info, creature.Position),
        CreatureChange.Rewarded or CreatureChange.Punished => null,
    };

    /// <summary>A settlement gained or lost a god's majority.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeFollowing(FollowingChanged change, SettlementState settlement, WorldState world, int? localGodId)
    {
        var key = Key(change.Gained ? "gained" : "lost", (settlement.Id * 1000) + change.GodId);
        if (change.GodId == localGodId)
        {
            return change.Gained
                ? (key, Format("{0} now follows you.", settlement.Name), AlertSeverity.Good, settlement.Position)
                : (key, Format("{0} has turned away from you.", settlement.Name), AlertSeverity.Danger, settlement.Position);
        }

        var god = GodStanding.Find(world, change.GodId)?.Name ?? "another god";
        return change.Gained && Concerns(settlement, localGodId)
            ? (key, Format("{0} now follows {1}.", settlement.Name, god), AlertSeverity.Warning, settlement.Position)
            : null;
    }

    /// <summary>Whether a settlement matters to the player: some of its people follow the player's god.</summary>
    private static bool Concerns(SettlementState settlement, int? localGodId) =>
        localGodId is { } god && BeliefModel.Find(settlement, god) is { Adherence: > 0f };

    /// <summary>The settlement with an id, if it exists.</summary>
    private static SettlementState? Settlement(WorldState world, int id) => world.Settlements.Find(s => s.Id == id);

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
