using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.History;
using Pantheon.Domain.World;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.History;

/// <summary>
/// Turns what happens into the world's history (spec §50, §92, ADR-020): it weighs every event
/// as it is raised and keeps only the significant ones: pacts, wars, peace and betrayal; dominion,
/// victory and ranks; deadly quakes and fires, famines, exoduses, conversions and falls of
/// settlements; and the Creatures' battles and legends. The same kind of moment for the same
/// settlement or pair is not remembered again within <c>RepeatSeconds</c>.
/// </summary>
public sealed class HistoryRecorder
{
    // A settlement's falls and resettlements are checked once every this many ticks.
    private const int FallCheckTicks = 10;

    private readonly HistoryRules _rules;

    /// <summary>Creates the recorder over validated rules.</summary>
    /// <param name="rules">History rules.</param>
    public HistoryRecorder(HistoryRules rules)
    {
        HistoryRulesValidator.Validate(rules);
        _rules = rules;
    }

    /// <summary>The rules.</summary>
    public HistoryRules Rules => _rules;

    /// <summary>Records the significant events among those raised from an index on, then the settlements that fell or were settled again.</summary>
    /// <param name="world">World state.</param>
    /// <param name="events">The events.</param>
    /// <param name="from">The first new event's index.</param>
    public void Record(WorldState world, IReadOnlyList<WorldEvent> events, int from)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        for (var i = Math.Max(0, from); i < events.Count; i++)
        {
            Consider(world, events[i]);
        }

        if (IntervalSystem.IsDue(world.Tick, FallCheckTicks))
        {
            CheckFalls(world);
        }

        if (world.History.Count > _rules.Capacity)
        {
            world.History.RemoveRange(0, world.History.Count - _rules.Capacity);
        }
    }

    /// <summary>One event: remembered when it is significant.</summary>
    private void Consider(WorldState world, WorldEvent worldEvent)
    {
        switch (worldEvent)
        {
            case DiplomacyChanged { Betrayal: true } move:
                Add(world, HistoryKind.Betrayal, god: move.ActorGodId, other: move.OtherGodId);
                break;
            case DiplomacyChanged { Kind: DiplomaticCommandKind.DeclareWar } move:
                Add(world, HistoryKind.War, god: move.ActorGodId, other: move.OtherGodId);
                break;
            case DiplomacyChanged { Kind: DiplomaticCommandKind.Accept, Status: DiplomaticStatus.Alliance } move:
                Add(world, HistoryKind.Alliance, god: move.OtherGodId, other: move.ActorGodId);
                break;
            case DiplomacyChanged { Kind: DiplomaticCommandKind.Accept } move:
                Add(world, HistoryKind.Peace, god: move.OtherGodId, other: move.ActorGodId);
                break;
            case DominanceChanged { Started: true } dominance:
                Add(world, HistoryKind.Dominion, god: dominance.GodId, other: dominance.Allies.Count > 0 ? dominance.Allies[0] : null);
                break;
            case VictoryAchieved { GodIds.Count: > 0 } victory:
                Add(world, HistoryKind.Victory, god: victory.GodIds[0], other: victory.GodIds.Count > 1 ? victory.GodIds[1] : null);
                break;
            case RankAchieved rank:
                Add(world, HistoryKind.Rank, god: rank.GodId, count: (int)rank.Rank);
                break;
            case FollowingChanged { Gained: true } change:
                Convert(world, change);
                break;
            case SettlementDamaged { Cause: DisasterKind.Earthquake } quake when quake.Deaths >= _rules.EarthquakeDeaths || quake.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Earthquake, quake.SettlementId, null, quake.Deaths);
                break;
            case SettlementDamaged { Cause: DisasterKind.Fire } fire when fire.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Fire, fire.SettlementId, null, fire.HousesLost);
                break;
            case FamineStarted famine:
                Once(world, HistoryKind.Famine, famine.SettlementId, null, 0);
                break;
            case MigrationStarted exodus when exodus.People >= _rules.ExodusPeople:
                Once(world, HistoryKind.Exodus, exodus.OriginSettlementId, exodus.GodId, exodus.People);
                break;
            case CreatureFightEnded { Outcome: FightOutcome.Banished } battle when !Recent(world, HistoryKind.Battle, r => Pair(r, battle.CreatureId, battle.OtherCreatureId)):
                Add(world, HistoryKind.Battle, creature: battle.CreatureId, otherCreature: battle.OtherCreatureId, god: world.Creatures.Find(c => c.Id == battle.CreatureId)?.GodId, other: world.Creatures.Find(c => c.Id == battle.OtherCreatureId)?.GodId);
                break;
            case CreatureChanged { Change: CreatureChange.LevelledUp } grown when world.Creatures.Find(c => c.Id == grown.CreatureId) is { } creature && _rules.LegendLevels.Contains(creature.Level):
                Add(world, HistoryKind.Legend, creature: creature.Id, god: creature.GodId, count: creature.Level);
                break;
            default:
                break;
        }
    }

    /// <summary>A settlement of some size turns to a god, unless it was last remembered turning to that god, or turning lately.</summary>
    private void Convert(WorldState world, FollowingChanged change)
    {
        if (world.Settlements.Find(s => s.Id == change.SettlementId) is not { } settlement || settlement.Population < _rules.ConversionPopulation)
        {
            return;
        }

        var last = world.History.FindLast(r => r.Kind == HistoryKind.Conversion && r.SettlementId == settlement.Id);
        if (last is null || (last.GodId != change.GodId && world.Tick - last.Tick >= RepeatTicks))
        {
            Add(world, HistoryKind.Conversion, god: change.GodId, settlement: settlement.Id);
        }
    }

    /// <summary>A settlement whose last people are gone fell; one that has people again after a fall was settled again.</summary>
    private void CheckFalls(WorldState world)
    {
        foreach (var settlement in world.Settlements)
        {
            var last = world.History.FindLast(r => r.SettlementId == settlement.Id && r.Kind is HistoryKind.Fall or HistoryKind.Resettled);
            if (settlement.Population <= 0 && last?.Kind != HistoryKind.Fall)
            {
                Add(world, HistoryKind.Fall, settlement: settlement.Id);
            }
            else if (settlement.Population > 0 && last?.Kind == HistoryKind.Fall)
            {
                Add(world, HistoryKind.Resettled, settlement: settlement.Id, count: settlement.Population);
            }
        }
    }

    /// <summary>Records a settlement's moment unless the same kind was recorded there lately.</summary>
    private void Once(WorldState world, HistoryKind kind, int settlementId, int? godId, int count)
    {
        if (!Recent(world, kind, r => r.SettlementId == settlementId))
        {
            Add(world, kind, god: godId, settlement: settlementId, count: count);
        }
    }

    /// <summary>Whether a record of the kind matching the test was made within the repeat time.</summary>
    private bool Recent(WorldState world, HistoryKind kind, Func<HistoryRecord, bool> same)
    {
        for (var i = world.History.Count - 1; i >= 0; i--)
        {
            var record = world.History[i];
            if (world.Tick - record.Tick >= RepeatTicks)
            {
                return false;
            }

            if (record.Kind == kind && same(record))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a record concerns the same two Creatures, either way round.</summary>
    private static bool Pair(HistoryRecord record, int a, int b) =>
        (record.CreatureId == a && record.OtherCreatureId == b) || (record.CreatureId == b && record.OtherCreatureId == a);

    /// <summary>Ticks of the repeat time.</summary>
    private long RepeatTicks => (long)MathF.Round(_rules.RepeatSeconds * SimulationTime.TicksPerSecond);

    /// <summary>Adds a record.</summary>
    private static void Add(WorldState world, HistoryKind kind, int? god = null, int? other = null, int? settlement = null, int? creature = null, int? otherCreature = null, int count = 0) =>
        world.History.Add(new HistoryRecord
        {
            Tick = world.Tick,
            Kind = kind,
            GodId = god,
            OtherGodId = other,
            SettlementId = settlement,
            CreatureId = creature,
            OtherCreatureId = otherCreature,
            Count = count,
        });
}
