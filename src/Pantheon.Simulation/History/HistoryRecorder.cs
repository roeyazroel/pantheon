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
                Add(world, HistoryKind.Victory, god: victory.GodIds[0], other: victory.GodIds.Count > 1 ? victory.GodIds[1] : null, count: (int)victory.Kind);
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
            case SettlementDamaged { Cause: DisasterKind.Flood } flood when flood.Deaths >= _rules.EarthquakeDeaths || flood.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Flood, flood.SettlementId, null, flood.HousesLost);
                break;
            case SettlementDamaged { Cause: DisasterKind.Hurricane } hurricane when hurricane.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Hurricane, hurricane.SettlementId, null, hurricane.HousesLost);
                break;
            case LandScarred scarred:
                Once(world, HistoryKind.Scar, scarred.SettlementId, null, 0);
                break;
            case SettlementDamaged { Cause: DisasterKind.Tsunami } wave when wave.Deaths >= _rules.EarthquakeDeaths || wave.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Tsunami, wave.SettlementId, null, wave.HousesLost);
                break;
            case SettlementDamaged { Cause: DisasterKind.Landslide } slide when slide.Deaths > 0 || slide.HousesLost > 0:
                Once(world, HistoryKind.Landslide, slide.SettlementId, null, slide.Deaths);
                break;
            case SettlementDamaged { Cause: DisasterKind.Tornado } tornado when tornado.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Tornado, tornado.SettlementId, null, tornado.HousesLost);
                break;
            case VolcanoChanged { Phase: VolcanoPhase.Erupting } eruption when NearestId(world, world.Regions.GetRegionCenter(eruption.RegionId)) is { } beside:
                Once(world, HistoryKind.Eruption, beside, null, 0);
                break;
            case MeteorStruck meteor when NearestId(world, meteor.Position) is { } struck:
                Once(world, HistoryKind.Meteor, struck, null, 0);
                break;
            case BlightChanged { Started: true } blight:
                Once(world, HistoryKind.Blight, blight.SettlementId, null, 0);
                break;
            case SwarmArrived swarm:
                Once(world, HistoryKind.Locusts, swarm.SettlementId, null, 0);
                break;
            case CreatureSpecialised calling:
                Add(world, HistoryKind.Calling, god: calling.GodId, creature: calling.CreatureId, count: (int)calling.Specialisation);
                break;
            case VillagersSetDown { Rescued: true } rescue when rescue.People >= _rules.EarthquakeDeaths:
                Once(world, HistoryKind.Rescue, rescue.FromSettlementId, rescue.GodId, rescue.People);
                break;
            case HandStruck { SettlementId: { } stoned } stoning when stoning.Deaths >= _rules.EarthquakeDeaths || stoning.HousesLost >= _rules.HousesLost:
                Once(world, HistoryKind.Stoning, stoned, stoning.GodId, stoning.Deaths);
                break;
            case NotableRose { Kind: Domain.Notables.NotableKind.Prophet } prophet:
                Add(world, HistoryKind.Prophet, god: prophet.GodId, settlement: prophet.SettlementId, count: prophet.Anointed ? 1 : 0, name: world.Notables.Find(n => n.Id == prophet.NotableId)?.Name);
                break;
            case NotableDied { Martyred: true } martyr:
                Add(world, HistoryKind.Martyrdom, god: martyr.GodId, settlement: martyr.SettlementId, name: martyr.Name);
                break;
            case ElderTurned { Resented: true } turned:
                Add(world, HistoryKind.ElderTurned, god: turned.FromGodId, other: turned.ToGodId, settlement: turned.SettlementId, name: world.Notables.Find(n => n.Id == turned.NotableId)?.Name);
                break;
            case GreatEventBegan great:
                Add(world, great.Kind switch
                {
                    Domain.GreatEvents.GreatEventKind.Plague => HistoryKind.Plague,
                    Domain.GreatEvents.GreatEventKind.Eclipse => HistoryKind.Eclipse,
                    Domain.GreatEvents.GreatEventKind.Reformation => HistoryKind.Reformation,
                    Domain.GreatEvents.GreatEventKind.WorldWar => HistoryKind.WorldWar,
                    Domain.GreatEvents.GreatEventKind.GreatDrought => HistoryKind.GreatDrought,
                    Domain.GreatEvents.GreatEventKind.GreatMigration => HistoryKind.GreatMigration,
                }, god: great.GodId, count: world.GreatEvents.Events.Find(e => e.Id == great.EventId)?.Peak ?? 0, name: great.Name);
                break;
            case DivineActDone act:
                Add(world, act.Act switch
                {
                    DivineActKind.Hallowing => HistoryKind.Hallowing,
                    DivineActKind.Awakening => HistoryKind.Awakening,
                    DivineActKind.Stillness => HistoryKind.Stillness,
                }, god: act.GodId, settlement: NearestId(world, act.Position));
                break;
            case ThirstStarted thirst:
                Once(world, HistoryKind.Thirst, thirst.SettlementId, null, 0);
                break;
            case SettlementFounded founded:
                Add(world, HistoryKind.Founding, god: world.Settlements.Find(s => s.Id == founded.SettlementId) is { } village ? Domains.DomainModel.MajorityGodId(village) : null, settlement: founded.SettlementId, count: founded.Settlers);
                break;
            case BuildingCompleted { Kind: Domain.Society.BuildingKind.Temple, GodId: { } templeGod } temple:
                Add(world, HistoryKind.Temple, god: templeGod, settlement: temple.SettlementId);
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
            case DomainChosen domain:
                Add(world, HistoryKind.Domain, god: domain.GodId, count: (int)domain.Domain);
                break;
            case HolySiteClaimed claim:
                Add(world, HistoryKind.Claimed, god: claim.GodId, other: claim.PreviousGodId, count: claim.SiteId);
                break;
            case SchismStarted schism when schism.People >= _rules.SchismPeople:
                Add(world, HistoryKind.Schism, god: schism.GodId, settlement: schism.SettlementId, count: schism.People);
                break;
            case SchismEnded { Reconciled: true } reunion:
                Add(world, HistoryKind.Reunion, god: reunion.GodId, settlement: reunion.SettlementId);
                break;
            case TreatySigned { Secret: false } signed when signed.Kind is Domain.Politics.TreatyKind.NonAggression or Domain.Politics.TreatyKind.MutualDefense
                or Domain.Politics.TreatyKind.JointWar or Domain.Politics.TreatyKind.Tribute or Domain.Politics.TreatyKind.DefensivePact:
                Add(world, HistoryKind.Treaty, god: signed.FirstGodId, other: signed.SecondGodId, count: (int)signed.Kind);
                break;
            case TreatyEnded { Secret: false, BrokenByGodId: { } breaker } broken:
                Add(world, HistoryKind.Betrayal, god: breaker, other: breaker == broken.FirstGodId ? broken.SecondGodId : broken.FirstGodId);
                break;
            case TreatyRevealed revealed:
                Add(world, HistoryKind.Revelation, god: revealed.FirstGodId, other: revealed.SecondGodId, count: (int)revealed.Kind);
                break;
            case CouncilClosed { Passed: true } decree:
                var motion = decree.Motion;
                Add(world, HistoryKind.Decree, god: motion.TargetGodId, settlement: motion.SettlementId, count: ((int)motion.Kind * 1000) + (motion.SiteId ?? (int)(motion.Practice ?? 0)));
                break;
            case HolyWarEnded holyWar when holyWar.Killed + holyWar.Converted >= _rules.SchismPeople:
                Add(world, HistoryKind.HolyWar, god: holyWar.GodId, settlement: holyWar.DefenderSettlementId, count: holyWar.Killed);
                break;
            case PeaceDictated peace:
                Add(world, HistoryKind.DictatedPeace, god: peace.WinnerGodId, other: peace.LoserGodId, count: (int)peace.Goal);
                break;
            case CovertActExposed exposed when !Recent(world, HistoryKind.Exposure, r => r.GodId == exposed.GodId && r.OtherGodId == exposed.VictimGodId):
                Add(world, HistoryKind.Exposure, god: exposed.GodId, other: exposed.VictimGodId, settlement: exposed.SettlementId);
                break;
            case AllyAbsorbed absorbed:
                Add(world, HistoryKind.Absorption, god: absorbed.GodId, other: absorbed.AllyGodId, count: absorbed.People);
                break;
            case Rebellion revolt:
                Add(world, HistoryKind.Revolt, god: revolt.GodId, settlement: revolt.SettlementId, count: revolt.People);
                break;
            case SecularismRose { Stage: Domain.Culture.SecularStage.AntiDivine } renounced:
                Once(world, HistoryKind.Renunciation, renounced.SettlementId, null, 0);
                break;
            case DoctrineEmbraced embraced when world.Settlements.Find(s => s.Id == embraced.SettlementId) is { } place && place.Population >= _rules.ConversionPopulation
                && !Recent(world, HistoryKind.Doctrine, r => r.SettlementId == place.Id && r.Count == (int)embraced.Doctrine):
                Add(world, HistoryKind.Doctrine, settlement: place.Id, count: (int)embraced.Doctrine);
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
    /// <summary>The settlement nearest a point, if any.</summary>
    private static int? NearestId(WorldState world, GroundPosition point)
    {
        int? nearest = null;
        var best = float.MaxValue;
        foreach (var settlement in world.Settlements)
        {
            var distance = ((settlement.Position.X - point.X) * (settlement.Position.X - point.X)) + ((settlement.Position.Z - point.Z) * (settlement.Position.Z - point.Z));
            if (distance < best)
            {
                (nearest, best) = (settlement.Id, distance);
            }
        }

        return nearest;
    }

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
    private static void Add(WorldState world, HistoryKind kind, int? god = null, int? other = null, int? settlement = null, int? creature = null, int? otherCreature = null, int count = 0, string? name = null) =>
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
            Name = name,
        });
}
