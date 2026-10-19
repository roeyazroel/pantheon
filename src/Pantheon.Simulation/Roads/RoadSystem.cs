using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Roads;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Roads;

/// <summary>
/// One step of the roads (spec §29, ADR-033): a village plans a road to its nearest unjoined
/// neighbour and builds it with stone and work; floods, fires and rubble on the way wear a road
/// until it is cut, and its villages mend it; and an open road carries trade and food to the hungry.
/// </summary>
public sealed class RoadSystem : ISimulationSystem
{
    private readonly SettlementRules _settlements;
    private readonly RoadRules _rules;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="settlements">Settlement rules, with the road rules and the people's food needs.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    public RoadSystem(SettlementRules settlements, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _settlements = settlements;
        _rules = settlements.Roads;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        PlanOne(world);
        foreach (var road in world.Roads.ToArray())
        {
            var from = Village(world, road.FromSettlementId);
            var to = Village(world, road.ToSettlementId);
            if (road.Built < road.Length)
            {
                Build(world, road, from, seconds, context.Events);
                continue;
            }

            Wear(world, road, from, to, seconds, context.Events);
            if (from is not null && to is not null && RoadModel.IsOpen(_rules, road))
            {
                Trade(from, to, seconds);
            }
        }
    }

    /// <summary>One village a step, in turn, plans a road if it is large enough and builds none already.</summary>
    private void PlanOne(WorldState world)
    {
        var count = world.Settlements.Count;
        if (count == 0)
        {
            return;
        }

        var settlement = world.Settlements[(int)(world.Tick / _intervalTicks % count)];
        if (settlement.Population < _rules.MinPopulation || world.Roads.Exists(r => r.FromSettlementId == settlement.Id && r.Built < r.Length)
            || RoadModel.Plan(_rules, world, settlement) is not { } plan)
        {
            return;
        }

        world.Roads.Add(new RoadState
        {
            Id = world.NextRoadId++,
            FromSettlementId = settlement.Id,
            ToSettlementId = plan.Neighbour.Id,
            Path = plan.Path,
            Length = RoadModel.Length(plan.Path),
        });
    }

    /// <summary>The builders lay as much road as their work and stone allow; a road whose builders are gone is given up.</summary>
    private void Build(WorldState world, RoadState road, SettlementState? builders, float seconds, List<WorldEvent> events)
    {
        if (builders is null)
        {
            world.Roads.Remove(road);
            return;
        }

        var metres = builders.Population * _settlements.WorkforceShare * _rules.WorkShare * _rules.MetresPerWorkerPerSecond * seconds;
        if (_rules.StonePerMetre > 0f)
        {
            metres = Math.Min(metres, builders.Stone / _rules.StonePerMetre);
        }

        metres = Math.Min(metres, road.Length - road.Built);
        builders.Stone = Math.Max(0f, builders.Stone - (metres * _rules.StonePerMetre));
        road.Built += metres;
        if (road.Built >= road.Length)
        {
            road.Built = road.Length;
            events.Add(new RoadBuilt { Tick = world.Tick, RoadId = road.Id, FromSettlementId = road.FromSettlementId, ToSettlementId = road.ToSettlementId });
        }
    }

    /// <summary>Floods, fires and rubble on the way wear the road, which its villages mend; a road whose village is gone decays away.</summary>
    private void Wear(WorldState world, RoadState road, SettlementState? from, SettlementState? to, float seconds, List<WorldEvent> events)
    {
        var open = RoadModel.IsOpen(_rules, road);
        if (from is null || to is null)
        {
            road.Wear += _rules.DecayPerSecond * seconds;
            if (road.Wear >= 1f)
            {
                world.Roads.Remove(road);
                return;
            }
        }
        else
        {
            var harm = Harm(world, road);
            road.Wear = harm > 0f ? Math.Min(1f, road.Wear + (harm * _rules.WearPerSecond * seconds)) : Math.Max(0f, road.Wear - (_rules.RepairPerSecond * seconds));
        }

        if (open != RoadModel.IsOpen(_rules, road))
        {
            events.Add(new RoadChanged { Tick = world.Tick, RoadId = road.Id, FromSettlementId = road.FromSettlementId, ToSettlementId = road.ToSettlementId, Open = !open });
        }
    }

    /// <summary>The worst flood, fire or rubble on the way, looked at every half region along it.</summary>
    private static float Harm(WorldState world, RoadState road)
    {
        var grid = world.Regions;
        var step = Math.Max(1f, grid.RegionSize * 0.5f);
        var worst = 0f;
        for (var i = 1; i < road.Path.Count; i++)
        {
            var (a, b) = (road.Path[i - 1], road.Path[i]);
            var (dx, dz) = (b.X - a.X, b.Z - a.Z);
            var samples = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt((dx * dx) + (dz * dz)) / step));
            for (var k = i == 1 ? 0 : 1; k <= samples; k++)
            {
                var t = (float)k / samples;
                if (grid.TryGetRegionIdAt(a.X + (dx * t), a.Z + (dz * t), out var id) && grid.Cells[id] is var region)
                {
                    worst = Math.Max(worst, Math.Max(SimMath.Clamp01(region.Flood), Math.Max(SimMath.Clamp01(region.Fire), SimMath.Clamp01(region.Rubble))));
                }
            }
        }

        return worst;
    }

    /// <summary>An open road brings both villages wealth, and carries food from one with food to spare to a hungry one.</summary>
    private void Trade(SettlementState from, SettlementState to, float seconds)
    {
        from.Society.Wealth += _rules.TradeWealthPerSecond * seconds;
        to.Society.Wealth += _rules.TradeWealthPerSecond * seconds;
        Relieve(from, to, seconds);
        Relieve(to, from, seconds);
    }

    /// <summary>Food from a village with food to spare to a hungry one.</summary>
    private void Relieve(SettlementState donor, SettlementState hungry, float seconds)
    {
        var need = (_rules.HungerSteps * SettlementEconomy.ComputeConsumption(_settlements, hungry.Population)) - hungry.Food;
        var spare = donor.Food - (3f * _rules.HungerSteps * SettlementEconomy.ComputeConsumption(_settlements, donor.Population));
        var sent = Math.Min(_rules.FoodReliefPerSecond * seconds, Math.Min(need, spare));
        if (sent > 0f)
        {
            donor.Food -= sent;
            hungry.Food += sent;
        }
    }

    /// <summary>A living village by id.</summary>
    private static SettlementState? Village(WorldState world, int id) => world.Settlements.Find(s => s.Id == id && s.Population > 0);
}
