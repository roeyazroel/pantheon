using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// One step of the ancient beast (spec §52, ADR-035): asleep, it wakes by itself in time or when a
/// quake or the volcano disturbs it; awake, it walks to the nearest village and ravages it, then the
/// next, until it tires and goes home to sleep. Creatures near it fight it, and AI gods send theirs to
/// the villages it ravages; fire and smiting miracles harm it; and when it falls, the gods that harmed
/// it share the glory.
/// </summary>
public sealed class BeastSystem : ISimulationSystem
{
    /// <summary>Purpose name of the step's random streams.</summary>
    public const string RandomStream = "beast";

    // Seconds between the beast's calls to the AI gods' Creatures.
    private const int CallSeconds = 5;

    // How near its village, in metres, the beast ravages it.
    private const float RavageReach = 40f;

    private readonly GreatEventRules _rules;
    private readonly BeastRules _beast;
    private readonly CreatureRules _creatures;
    private readonly DisasterDamage _damage;
    private readonly CreatureBrain? _brain;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="disasters">Disaster rules, with the great events' and how a blow harms a village.</param>
    /// <param name="settlements">Settlement rules, for houses.</param>
    /// <param name="divine">Divine rules, for how a blow frightens.</param>
    /// <param name="creatures">Creature rules, for the Creatures' power and their flight.</param>
    /// <param name="brain">The Creatures' brain, to send the AI gods' Creatures; <c>null</c> sends none.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    /// <param name="domains">Domain rules, for the houses the Protection domain saves; <c>null</c> for none.</param>
    public BeastSystem(DisasterRules disasters, Domain.Settlements.SettlementRules settlements, Domain.Belief.DivineRules divine, CreatureRules creatures, CreatureBrain? brain, int intervalTicks, DomainRules? domains = null)
    {
        ArgumentNullException.ThrowIfNull(disasters);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _rules = disasters.GreatEvents;
        _beast = _rules.Beast;
        _creatures = creatures;
        _damage = new DisasterDamage(disasters, settlements, divine, domains, creatures.Callings);
        _brain = brain;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        if (BeastModel.Find(_beast, world) is not { } beast)
        {
            return;
        }

        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        switch (beast.Phase)
        {
            case BeastPhase.Sleeping:
                var rested = world.Tick >= (long)(_beast.FirstSleepSeconds * SimulationTime.TicksPerSecond);
                // Nothing wakes it in its first sleep, so it comes when the world is grown.
                if (rested && (BeastModel.Disturbed(_beast, world, beast) || random.NextDouble() < _beast.WakePerHour / 3600.0 * seconds))
                {
                    BeastModel.Wake(_rules, world, beast, context.Events);
                }

                break;
            case BeastPhase.Awake:
                Rampage(world, beast, random, seconds, context.Events);
                break;
            case BeastPhase.Returning:
                Fight(world, beast, seconds);
                if (Walk(beast, _beast.Speed * seconds))
                {
                    (beast.Phase, beast.Health) = (BeastPhase.Sleeping, _beast.Health);
                    if (GreatEventModel.Active(world, GreatEventKind.Beast) is { } waking)
                    {
                        GreatEventModel.End(world, waking, context.Events);
                    }
                }

                Slain(world, beast, context.Events);
                break;
            case BeastPhase.Slain:
                break;
        }
    }

    /// <summary>Awake: it fights what fights it, then walks to its village or ravages it, until it tires.</summary>
    private void Rampage(WorldState world, BeastState beast, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        Fight(world, beast, seconds);
        if (Slain(world, beast, events))
        {
            return;
        }

        if (world.Tick - beast.WokeTick >= (long)(_beast.AwakeSeconds * SimulationTime.TicksPerSecond))
        {
            GoHome(world, beast);
            return;
        }

        var village = beast.TargetSettlementId is { } id ? world.Settlements.Find(s => s.Id == id && s.Population > 0) : null;
        if (village is null && !Hunt(world, beast))
        {
            GoHome(world, beast);
            return;
        }

        village ??= world.Settlements.Find(s => s.Id == beast.TargetSettlementId)!;
        Walk(beast, _beast.Speed * seconds);
        if (BeastModel.Distance(beast.Position, village.Position) > RavageReach)
        {
            return;
        }

        var strikeTicks = Math.Max(1, (int)MathF.Round(_beast.StrikeSeconds * SimulationTime.TicksPerSecond));
        if (beast.RavagedTicks % strikeTicks < _intervalTicks)
        {
            _damage.Strike(world, village, DisasterKind.Beast, _beast.StrikeHousing, _beast.StrikeDeaths, 1f, random, events);
        }

        if (beast.RavagedTicks % (CallSeconds * SimulationTime.TicksPerSecond) < _intervalTicks)
        {
            Call(world, beast, village);
        }

        beast.RavagedTicks += _intervalTicks;
        if (beast.RavagedTicks >= (int)(_beast.RavageSeconds * SimulationTime.TicksPerSecond))
        {
            (beast.LastSettlementId, beast.TargetSettlementId, beast.RavagedTicks) = (village.Id, null, 0);
        }
    }

    /// <summary>The Creatures near it fight it, losing health until they fly; fire harms it; and it heals while nothing does.</summary>
    private void Fight(WorldState world, BeastState beast, float seconds)
    {
        var harmed = false;
        foreach (var creature in world.Creatures)
        {
            if (creature.Activity.Phase == CreaturePhase.Banished || CreatureModel.IsHeld(world, creature) || BeastModel.Distance(creature.Position, beast.Position) > _beast.FightRange)
            {
                continue;
            }

            BeastModel.Harm(beast, creature.GodId, CreatureBonds.Power(_creatures, creature) * _beast.CreatureHarm * seconds);
            creature.Health = Math.Max(0f, creature.Health - (_beast.Bite * seconds));
            harmed = true;
            if (creature.Health <= _beast.FleeHealth)
            {
                CreatureBanishment.Banish(_creatures, creature);
            }
        }

        if (world.Regions.TryGetRegionIdAt(beast.Position.X, beast.Position.Z, out var regionId) && world.Regions.Cells[regionId].Fire > 0f)
        {
            BeastModel.Harm(beast, null, world.Regions.Cells[regionId].Fire * _beast.FireHarmPerSecond * seconds);
            harmed = true;
        }

        // A beast struck down does not get up again.
        if (!harmed && beast.Health > 0f)
        {
            beast.Health = Math.Min(_beast.Health, beast.Health + (_beast.RegenPerSecond * seconds));
        }
    }

    /// <summary>Whether it has fallen, and if so its fall.</summary>
    private bool Slain(WorldState world, BeastState beast, List<WorldEvent> events)
    {
        if (beast.Health > 0f || beast.Phase == BeastPhase.Slain)
        {
            return false;
        }

        BeastModel.Fall(_rules, world, beast, events);
        return true;
    }

    /// <summary>Sets it on the way to the nearest village it did not ravage last; <c>false</c> when none can be reached.</summary>
    private static bool Hunt(WorldState world, BeastState beast)
    {
        Domain.Settlements.SettlementState? nearest = null;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0 && settlement.Id != beast.LastSettlementId
                && (nearest is null || BeastModel.Distance(beast.Position, settlement.Position) < BeastModel.Distance(beast.Position, nearest.Position)))
            {
                nearest = settlement;
            }
        }

        if (nearest is null || RegionPathfinder.FindPath(world.Regions, beast.Position, nearest.Position) is not { } way)
        {
            return false;
        }

        (beast.TargetSettlementId, beast.RavagedTicks) = (nearest.Id, 0);
        beast.Path.Clear();
        beast.Path.AddRange(way);
        return true;
    }

    /// <summary>It tires and turns for its lair.</summary>
    private static void GoHome(WorldState world, BeastState beast)
    {
        var lair = world.Regions.GetRegionCenter(beast.LairRegionId);
        (beast.Phase, beast.TargetSettlementId) = (BeastPhase.Returning, null);
        beast.Path.Clear();
        beast.Path.AddRange(RegionPathfinder.FindPath(world.Regions, beast.Position, lair) is { } way ? way : [lair]);
    }

    /// <summary>The AI gods with followers in the village send their Creatures, if near enough, to fight it.</summary>
    private void Call(WorldState world, BeastState beast, Domain.Settlements.SettlementState village)
    {
        if (_brain is null)
        {
            return;
        }

        foreach (var creature in world.Creatures)
        {
            var god = world.Gods.Find(g => g.Id == creature.GodId);
            if (god is not { Controller: GodController.Ai } || creature.Activity.Phase == CreaturePhase.Banished || CreatureModel.IsHeld(world, creature)
                || BeliefModel.Find(village, god.Id) is not { Adherence: >= 0.2f } || BeastModel.Distance(creature.Position, beast.Position) > _beast.CallRange
                || creature.Health <= _beast.FleeHealth * 2f)
            {
                continue;
            }

            _brain.Command(world, creature, beast.Position);
        }
    }

    /// <summary>Moves it along its way; <c>true</c> when the way is done.</summary>
    private static bool Walk(BeastState beast, float distance)
    {
        while (distance > 0f && beast.Path.Count > 0)
        {
            var next = beast.Path[0];
            var length = BeastModel.Distance(beast.Position, next);
            if (length <= distance)
            {
                beast.Position = next;
                beast.Path.RemoveAt(0);
                distance -= length;
            }
            else
            {
                var t = distance / length;
                beast.Position = new GroundPosition(beast.Position.X + ((next.X - beast.Position.X) * t), beast.Position.Z + ((next.Z - beast.Position.Z) * t));
                distance = 0f;
            }
        }

        return beast.Path.Count == 0;
    }
}
