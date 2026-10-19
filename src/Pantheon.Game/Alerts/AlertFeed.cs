using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Disasters;
using Pantheon.Game.Divine;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;

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
            case ThirstStarted thirst when Settlement(world, thirst.SettlementId) is { } settlement && Concerns(settlement, localGodId):
                return (Key("thirst", settlement.Id), Format("The wells of {0} have run dry.", settlement.Name), AlertSeverity.Warning, settlement.Position);
            case FloodStarted flood:
                return WaterAlerts.DescribeFlood(flood, world, localGodId);
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
                return dominance.Allies.Count > 0 ? DescribeJointDominance(dominance, world, localGodId) : DescribeDominance(dominance, dominant, localGodId);
            case DiplomacyChanged change:
                return DescribeDiplomacy(change, world, localGodId);
            case VictoryAchieved victory when victory.GodIds.Count > 0:
                return localGodId is { } me && victory.GodIds.Contains(me)
                    ? (Key("victory", victory.GodIds[0]), victory.GodIds.Count > 1
                        ? Format("{0} have won together: {1}.", DivineText.Names(world, victory.GodIds, localGodId), Flow.VictoryText.Reason(victory.Kind))
                        : Format("You have won: {0}.", Flow.VictoryText.Reason(victory.Kind)), AlertSeverity.Good, null)
                    : (Key("victory", victory.GodIds[0]), Format("{0} {1} {2}.", DivineText.Names(world, victory.GodIds, localGodId), victory.GodIds.Count > 1 ? "have" : "has", Flow.VictoryText.Deed(victory.Kind)), AlertSeverity.Danger, null);
            case CreatureChanged changed when world.Creatures.Find(c => c.Id == changed.CreatureId) is { } creature && creature.GodId == localGodId:
                return DescribeChange(changed, creature);
            case WeatherSpellChanged spell:
                return DescribeSpell(spell);
            case DroughtChanged drought when Settlement(world, drought.SettlementId) is { } settlement && Concerns(settlement, localGodId):
                return drought.Started
                    ? (Key("drought", settlement.Id), Format("Drought in {0}: its crops wither. Rain would help.", settlement.Name), AlertSeverity.Warning, settlement.Position)
                    : (Key("rained", settlement.Id), Format("The drought in {0} is over.", settlement.Name), AlertSeverity.Good, settlement.Position);
            case EarthquakeStruck quake:
                return DescribeQuake(quake, world, localGodId);
            case WildfireStarted fire:
                return DescribeFire(fire, world, localGodId);
            case SettlementDamaged damage when Settlement(world, damage.SettlementId) is { } settlement && Concerns(settlement, localGodId):
                return (Key("damage" + damage.Cause, settlement.Id), Format("{0} struck {1}: {2} destroyed, {3} dead.", Capitalize(DisasterText.Kind(damage.Cause)), settlement.Name, Count(damage.HousesLost, "house", "houses"), damage.Deaths), AlertSeverity.Danger, settlement.Position);
            case MigrationStarted or MigrationEnded or MigrantsTurnedAway or RefugeePolicyChanged:
                return MigrationAlerts.Describe(worldEvent, world, localGodId);
            case CreaturesMet or CreatureFightEnded:
                return CreatureAlerts.Describe(worldEvent, world, localGodId);
            case DomainChosen or HolySiteClaimed or HolySiteContested or SchismStarted or SchismEnded:
                return FaithAlerts.Describe(worldEvent, world, localGodId);
            case PriorityChanged or BuildingCompleted or SettlementFounded or TechnologyAdvanced:
                return SocietyAlerts.Describe(worldEvent, world, localGodId);
            case Rebellion or SecularismRose or DoctrineEmbraced or TraitEarned:
                return CultureAlerts.Describe(worldEvent, world, localGodId);
            case TreatySigned or TreatyEnded or TreatyRevealed or TreatyInvoked or FavourOwed or RequestAnswered or ObligationEnded
                or CouncilSummoned or CouncilClosed or CouncilDefied or AllyAbsorbed:
                return PoliticsAlerts.Describe(worldEvent, world, localGodId);
            case WarGoalAchieved or PeaceDictated or CovertActExposed or HolyWarDeclared or HolyWarEnded:
                return ConflictAlerts.Describe(worldEvent, world, localGodId);
            case StormStruck storm when Settlement(world, storm.SettlementId) is { } settlement && Concerns(settlement, localGodId):
                return (Key("storm", settlement.Id), storm.Hurricane
                    ? Format("A hurricane strikes {0}: its houses are torn down and its stores ruined.", settlement.Name)
                    : Format("A storm strikes {0}: its stores and building work are being ruined.", settlement.Name), AlertSeverity.Danger, settlement.Position);
            case SeasonChanged or LandScarred:
                return NatureAlerts.Describe(worldEvent, world, localGodId);
            case TsunamiStruck or LandslideStruck or VolcanoChanged or TornadoTouchedDown or MeteorStruck or BlightChanged or SwarmArrived:
                return HazardAlerts.Describe(worldEvent, world, localGodId);
            default:
                return null;
        }
    }

    /// <summary>A diplomatic move that concerns the player (spec §59: "Vael proposes an alliance").</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeDiplomacy(DiplomacyChanged change, WorldState world, int? localGodId)
    {
        var formed = change.Kind == DiplomaticCommandKind.Accept && change.Status == DiplomaticStatus.Alliance;
        if (change.ActorGodId == localGodId || (change.OtherGodId != localGodId && change.Kind != DiplomaticCommandKind.DeclareWar && !change.Betrayal && !formed))
        {
            return null;
        }

        var actor = GodStanding.Find(world, change.ActorGodId)?.Name ?? "A god";
        var key = Key("diplomacy" + change.Kind, (change.ActorGodId * 100) + change.OtherGodId);
        if (change.OtherGodId != localGodId)
        {
            var other = GodStanding.Find(world, change.OtherGodId)?.Name ?? "a god";
            var text = change.Betrayal ? Format("{0} betrayed {1}.", actor, other)
                : formed ? Format("{0} and {1} are now allies.", actor, other)
                : Format("{0} declared war on {1}.", actor, other);
            return (key, text, AlertSeverity.Info, null);
        }

        return change.Kind switch
        {
            DiplomaticCommandKind.OfferAlliance => (key, Format("{0} proposes an alliance (P to answer).", actor), AlertSeverity.Warning, null),
            DiplomaticCommandKind.RequestPeace => (key, Format("{0} asks for peace (P to answer).", actor), AlertSeverity.Warning, null),
            DiplomaticCommandKind.BreakAlliance => (key, Format("{0} has broken your alliance!", actor), AlertSeverity.Danger, null),
            DiplomaticCommandKind.DeclareWar => (key, change.Betrayal ? Format("{0} betrayed your alliance and declared war!", actor) : Format("{0} has declared war on you!", actor), AlertSeverity.Danger, null),
            DiplomaticCommandKind.Accept => change.Answered is ProposalKind.Treaty or ProposalKind.Request or ProposalKind.Surrender ? null
                : (key, change.Status == DiplomaticStatus.Alliance ? Format("{0} accepted your alliance.", actor) : Format("{0} accepted peace.", actor), AlertSeverity.Good, null),
            DiplomaticCommandKind.Decline => change.Answered is ProposalKind.Request ? null : (key, Format("{0} declined your proposal.", actor), AlertSeverity.Info, null),
            DiplomaticCommandKind.ProposeTreaty or DiplomaticCommandKind.Request => PoliticsAlerts.Proposal(world, change.ActorGodId, localGodId!.Value, key),
            DiplomaticCommandKind.BreakTreaty or DiplomaticCommandKind.Absorb => null,
            DiplomaticCommandKind.DemandSurrender => (key, Format("{0} demands your surrender: {1} (P to answer).", actor, Conflict.ConflictText.SurrenderTerms(world, change.ActorGodId, change.OtherGodId)), AlertSeverity.Danger, null),
        };
    }

    /// <summary>A rival's miracle on a settlement where the player has followers.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeRivalCast(MiracleCast cast, WorldState world, int? localGodId)
    {
        var rival = cast.Veiled ? "An unknown god" : GodStanding.Find(world, cast.GodId)?.Name ?? "A rival";
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

    /// <summary>An earthquake that shakes the player's people: who caused it, if a god did, and the settlement it shakes hardest.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeQuake(EarthquakeStruck quake, WorldState world, int? localGodId)
    {
        if (quake.GodId == localGodId || world.Disasters.Earthquakes.Find(q => q.Id == quake.QuakeId) is not { } shaking)
        {
            return null;
        }

        SettlementState? worst = null;
        var strongest = 0f;
        foreach (var settlement in world.Settlements)
        {
            var felt = DisasterModel.Shaking(shaking, settlement.Position);
            if (felt > strongest && Concerns(settlement, localGodId))
            {
                worst = settlement;
                strongest = felt;
            }
        }

        if (worst is null)
        {
            return null;
        }

        var text = quake.GodId is { } god && GodStanding.Find(world, god) is { } cause
            ? Format("{0} shakes the earth under {1} (magnitude {2:0.0})!", cause.Name, worst.Name, quake.Magnitude)
            : Format("An earthquake (magnitude {0:0.0}) shakes {1}!", quake.Magnitude, worst.Name);
        return (Key("quake", quake.QuakeId), text, AlertSeverity.Danger, quake.Epicentre);
    }

    /// <summary>A fire lit near the player's people, other than by the player.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeFire(WildfireStarted fire, WorldState world, int? localGodId)
    {
        var grid = world.Regions;
        var (column, row) = grid.GetCoordinates(fire.RegionId);
        foreach (var settlement in world.Settlements)
        {
            var (c, r) = grid.GetCoordinates(settlement.RegionId);
            if (Math.Max(Math.Abs(c - column), Math.Abs(r - row)) <= 2 && Concerns(settlement, localGodId))
            {
                return (Key("fire", settlement.Id), Format("Fire near {0}, lit by {1}.", settlement.Name, DisasterText.Cause(fire.Cause)), AlertSeverity.Warning, grid.GetRegionCenter(fire.RegionId));
            }
        }

        return null;
    }

    /// <summary>A count with its noun, for example "1 house" or "3 houses".</summary>
    private static string Count(int count, string one, string many) => Format("{0} {1}", count, count == 1 ? one : many);

    /// <summary>The text with its first letter in capitals.</summary>
    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>A new weather spell: a warning for a dry spell, news for a wet one, relief when a dry spell ends.</summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeSpell(WeatherSpellChanged spell) => spell.Spell switch
    {
        WeatherSpell.Dry => (Key("spell", (int)spell.Spell), "A dry spell begins: little rain will fall, and the land will dry out.", AlertSeverity.Warning, null),
        WeatherSpell.Wet => (Key("spell", (int)spell.Spell), "A wet spell begins: rain will fall often, and storms are likely.", AlertSeverity.Info, null),
        WeatherSpell.HeatWave => (Key("spell", (int)spell.Spell), "A heat wave begins: the land will bake, fires will catch easily, and the heat will sicken the weak.", AlertSeverity.Warning, null),
        WeatherSpell.ColdWave => (Key("spell", (int)spell.Spell), "A cold wave begins: frost and snow will spoil the harvest, and the cold will sicken the homeless.", AlertSeverity.Warning, null),
        WeatherSpell.Normal => spell.Previous switch
        {
            WeatherSpell.Dry => (Key("spell", (int)spell.Spell), "The dry spell is over: the rains return.", AlertSeverity.Good, null),
            WeatherSpell.HeatWave => (Key("spell", (int)spell.Spell), "The heat wave breaks.", AlertSeverity.Good, null),
            WeatherSpell.ColdWave => (Key("spell", (int)spell.Spell), "The cold wave passes.", AlertSeverity.Good, null),
            WeatherSpell.Normal or WeatherSpell.Wet => null,
        },
    };

    /// <summary>
    /// An alliance began or ceased to dominate the world's belief together (ADR-015). Every member
    /// reports it, so only the member with the lowest id raises the alert.
    /// </summary>
    private static (string, string, AlertSeverity, GroundPosition?)? DescribeJointDominance(DominanceChanged dominance, WorldState world, int? localGodId)
    {
        var bloc = new List<int>(dominance.Allies) { dominance.GodId };
        bloc.Sort();
        if (bloc[0] != dominance.GodId)
        {
            return null;
        }

        var names = DivineText.Names(world, bloc, localGodId);
        var key = Key(dominance.Started ? "dominant" : "contested", 1000 + bloc[0]);
        var ours = localGodId is { } me && bloc.Contains(me);
        return dominance.Started
            ? (key, ours
                ? Format("{0} hold most of the world's belief together: keep it for ten minutes to win together.", names)
                : Format("{0} hold most of the world's belief together: they win together in ten minutes unless you stop them.", names), ours ? AlertSeverity.Good : AlertSeverity.Danger, null)
            : (key, Format("{0} no longer hold most of the world's belief together.", names), ours ? AlertSeverity.Warning : AlertSeverity.Good, null);
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
    internal static bool Concerns(SettlementState settlement, int? localGodId) =>
        localGodId is { } god && BeliefModel.Find(settlement, god) is { Adherence: > 0f };

    /// <summary>The settlement with an id, if it exists.</summary>
    private static SettlementState? Settlement(WorldState world, int id) => world.Settlements.Find(s => s.Id == id);

    /// <summary>De-duplication key of an alert.</summary>
    private static string Key(string kind, int id) => string.Create(CultureInfo.InvariantCulture, $"{kind}:{id}");

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
