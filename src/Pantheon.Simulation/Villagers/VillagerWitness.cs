using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Villagers;

/// <summary>
/// What followed villagers see (spec §80's individual memory, ADR-043): miracles near them, and the
/// disasters, famines, buildings, prophets and offerings of their village, and the Creatures that
/// feed, build, raid or attack it. A miracle or a prophet of their own god strengthens their faith;
/// another god's shakes it.
/// </summary>
internal static class VillagerWitness
{
    // How long the same Creature's deeds of a kind in a village are one memory.
    private const float SameSeconds = 300f;

    /// <summary>Lets every followed villager witness a run of events.</summary>
    /// <param name="rules">Villager rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="events">The events.</param>
    /// <param name="from">The first event to witness.</param>
    public static void Observe(VillagerRules rules, WorldState world, IReadOnlyList<WorldEvent> events, int from)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        if (world.Villagers.Count == 0)
        {
            return;
        }

        for (var i = Math.Max(0, from); i < events.Count; i++)
        {
            foreach (var villager in world.Villagers)
            {
                See(rules, world, villager, events[i]);
            }
        }
    }

    /// <summary>What one villager makes of one event.</summary>
    private static void See(VillagerRules rules, WorldState world, VillagerState villager, WorldEvent seen)
    {
        var home = villager.SettlementId;
        var tick = world.Tick;
        switch (seen)
        {
            case MiracleCast cast when VillagerModel.Distance(villager.Position, cast.Target) <= rules.WitnessRadius
                || (System.Linq.Enumerable.Contains(cast.AffectedSettlementIds, home) && VillagerModel.InVillage(rules, villager, world.Settlements.Find(s => s.Id == home))):
                VillagerModel.Remember(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.SawMiracle, SettlementId = home, GodId = cast.Veiled ? null : cast.GodId, Detail = cast.MiracleId, Count = cast.Deaths });
                Moved(rules, villager, cast.Veiled ? null : cast.GodId);
                break;
            case SettlementDamaged damaged when damaged.SettlementId == home:
                Once(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.Survived, SettlementId = home, Detail = damaged.Cause.ToString(), Count = damaged.Deaths }, rules.HardshipSeconds);
                break;
            case StormStruck storm when storm.SettlementId == home:
                Once(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.Survived, SettlementId = home, Detail = storm.Hurricane ? "Hurricane" : "Storm" }, rules.HardshipSeconds);
                break;
            case FamineStarted famine when famine.SettlementId == home:
                Once(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.Hungered, SettlementId = home, Detail = "famine" }, rules.HardshipSeconds);
                break;
            case ThirstStarted thirst when thirst.SettlementId == home:
                Once(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.Hungered, SettlementId = home, Detail = "thirst" }, rules.HardshipSeconds);
                break;
            case CreatureActed acted when acted.SettlementId == home && acted.Action is CreatureAction.Feed or CreatureAction.HelpBuild or CreatureAction.Farm or CreatureAction.GatherWood or CreatureAction.EatFromStores or CreatureAction.Attack:
                var creature = world.Creatures.Find(c => c.Id == acted.CreatureId);
                Once(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.SawCreature, SettlementId = home, GodId = creature?.GodId, OtherId = acted.CreatureId, Detail = acted.Action.ToString(), Count = acted.Deaths }, SameSeconds);
                break;
            case BuildingCompleted built when built.SettlementId == home:
                VillagerModel.Remember(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.Built, SettlementId = home, GodId = built.GodId, Detail = built.Kind.ToString() });
                break;
            case NotablePreached preached when preached.SettlementId == home:
                VillagerModel.Remember(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.HeardPreaching, SettlementId = home, GodId = preached.GodId, OtherId = preached.NotableId, Count = preached.Converted });
                Moved(rules, villager, preached.GodId);
                break;
            case OfferingGiven offering when offering.FromSettlementId == home || offering.ToSettlementId == home:
                VillagerModel.Remember(rules, villager, new VillagerMemory { Tick = tick, Deed = VillagerDeed.Offering, SettlementId = home, GodId = offering.GodId, Detail = offering.FromSettlementId == home ? "gave" : "received" });
                break;
            default:
                break;
        }
    }

    /// <summary>A sign of their own god strengthens their faith; another god's shakes it.</summary>
    private static void Moved(VillagerRules rules, VillagerState villager, int? godId)
    {
        if (godId is { } god && villager.GodId is { } own)
        {
            villager.Faith = SimMath.Clamp01(villager.Faith + (god == own ? rules.WitnessFaith : -0.5f * rules.WitnessFaith));
        }
    }

    /// <summary>Remembers a thing once while it lasts.</summary>
    private static void Once(VillagerRules rules, VillagerState villager, VillagerMemory memory, float seconds) => VillagerModel.RememberOnce(rules, villager, memory, seconds);
}
