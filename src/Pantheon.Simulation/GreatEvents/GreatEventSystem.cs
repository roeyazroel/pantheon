using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Roads;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// One step of the world's great events (spec §52, ADR-034): the plague breaks out, runs its course
/// and spreads; eclipses come and go; doubting faiths reform; and the watch begins and ends world
/// wars, great droughts and great migrations as the world's state calls for them.
/// </summary>
/// <remarks>It draws from a stream derived from the seed and the tick, like the disasters.</remarks>
public sealed class GreatEventSystem : ISimulationSystem
{
    /// <summary>Purpose name of the step's random streams.</summary>
    public const string RandomStream = "great-events";

    private readonly GreatEventRules _rules;
    private readonly RoadRules _roads;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="roads">Road rules, for the plague's spread along roads.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    public GreatEventSystem(GreatEventRules rules, RoadRules roads, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(roads);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _rules = rules;
        _roads = roads;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        Plague(world, random, seconds, context.Events);
        EclipseModel.Step(_rules, world, context.Events);
        Reformations(world, context.Events);
        GreatEventWatch.Step(_rules, world, seconds, context.Events);
    }

    /// <summary>Outbreaks, the sickness's course in every village, its spread, and the great event while enough villages are sick.</summary>
    private void Plague(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var rules = _rules.Plague;
        if (rules.OutbreakPerHour <= 0f && rules.Transmission <= 0f)
        {
            return;
        }

        var sick = new List<Domain.Settlements.SettlementState>();
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Plague > 0f)
            {
                sick.Add(settlement);
            }
            else if (random.NextDouble() < rules.OutbreakPerHour / 3600.0 * seconds * PlagueModel.Ripeness(rules, world, settlement))
            {
                settlement.Plague = PlagueModel.Onset;
                events.Add(new PlagueStruck { Tick = world.Tick, SettlementId = settlement.Id });
            }
        }

        foreach (var source in sick)
        {
            foreach (var other in world.Settlements)
            {
                var road = RoadModel.Between(world, source.Id, other.Id) is { } between && RoadModel.IsOpen(_roads, between);
                if (other.Id != source.Id && random.NextDouble() < PlagueModel.CatchPerSecond(rules, source, other, road) * seconds)
                {
                    other.Plague = PlagueModel.Onset;
                    events.Add(new PlagueStruck { Tick = world.Tick, SettlementId = other.Id, FromSettlementId = source.Id });
                }
            }
        }

        var count = 0;
        foreach (var settlement in world.Settlements)
        {
            PlagueModel.Advance(rules, settlement, seconds);
            count += settlement.Plague > 0f ? 1 : 0;
        }

        var plague = GreatEventModel.Active(world, GreatEventKind.Plague);
        if (plague is null && count >= rules.EventVillages)
        {
            GreatEventModel.Begin(_rules, world, GreatEventKind.Plague, null, null, count, events);
        }
        else if (plague is not null)
        {
            plague.Peak = Math.Max(plague.Peak, count);
            if (count == 0)
            {
                GreatEventModel.End(world, plague, events);
            }
        }
    }

    /// <summary>Every god whose doubting faith is ripe reforms it; a reformation is told for a minute.</summary>
    private void Reformations(WorldState world, List<WorldEvent> events)
    {
        foreach (var reformed in world.GreatEvents.Events.FindAll(e => e.Kind == GreatEventKind.Reformation && world.Tick >= e.EndTick))
        {
            GreatEventModel.End(world, reformed, events);
        }

        foreach (var god in world.Gods)
        {
            if (ReformationModel.Ripe(_rules.Reformation, world, god.Id))
            {
                Reform(_rules, world, god.Id, events);
            }
        }
    }

    /// <summary>Reforms a god's faith and tells of it, for a minute.</summary>
    /// <param name="rules">Great event rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="events">Receives the great event's beginning.</param>
    /// <returns>The reformation.</returns>
    public static GreatEventState Reform(GreatEventRules rules, WorldState world, int godId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var left = ReformationModel.Reform(rules.Reformation, world, godId);
        return GreatEventModel.Begin(rules, world, GreatEventKind.Reformation, world.Tick + (60 * SimulationTime.TicksPerSecond), godId, left, events);
    }
}
