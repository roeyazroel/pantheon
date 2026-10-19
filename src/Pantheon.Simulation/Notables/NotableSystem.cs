using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Notables;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Notables;

/// <summary>
/// One step of the world's notables (spec §80's Tier 3, ADR-032): every sizeable village has an
/// elder, who keeps its faith and may turn; devout villages call prophets, who walk from village to
/// village preaching; and danger or age takes them, a prophet who dies in danger a martyr.
/// </summary>
/// <remarks>Like the disasters, it draws from a stream derived from the world seed and the tick.</remarks>
public sealed class NotableSystem : ISimulationSystem
{
    /// <summary>Purpose name of the notables' random streams.</summary>
    public const string RandomStream = "notables";

    private readonly NotableRules _rules;
    private readonly SettlementRules _settlements;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="rules">Notable rules.</param>
    /// <param name="settlements">Settlement rules, for the danger villages are in.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    public NotableSystem(NotableRules rules, SettlementRules settlements, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _rules = rules;
        _settlements = settlements;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        if (_rules.Names.Count == 0)
        {
            return;
        }

        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        Rise(world, random, seconds, context.Events);
        foreach (var notable in world.Notables.ToArray())
        {
            if (Dies(world, notable, random, seconds, context.Events))
            {
                continue;
            }

            if (notable.Kind == NotableKind.Prophet)
            {
                Wander(world, notable, seconds, context.Events);
            }
            else
            {
                Lead(world, notable, seconds, context.Events);
            }
        }
    }

    /// <summary>Sizeable villages without an elder choose one; devout villages may call a prophet.</summary>
    private void Rise(WorldState world, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population >= _rules.ElderPopulation && MigrationModel.MainGod(settlement.Beliefs) is { } god
                && !world.Notables.Exists(n => n.Kind == NotableKind.Elder && n.SettlementId == settlement.Id))
            {
                NotableModel.Raise(_rules, world, NotableKind.Elder, god, settlement, anointed: false, events);
            }

            if (NotableModel.DevoutTo(_rules, settlement) is { } devout && NotableModel.ProphetsOf(world, devout) < _rules.MaxProphetsPerGod
                && random.NextDouble() < _rules.ProphetsPerHour / 3600.0 * seconds)
            {
                NotableModel.Raise(_rules, world, NotableKind.Prophet, devout, settlement, anointed: false, events);
            }
        }
    }

    /// <summary>Age, danger or the loss of their village takes a notable; a prophet who dies in danger is a martyr.</summary>
    private bool Dies(WorldState world, NotableState notable, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var village = world.Settlements.Find(s => s.Id == notable.SettlementId);
        var danger = notable.TargetSettlementId is null && village is not null ? DisasterModel.DangerTo(_settlements, world, village) : DangerAt(world, notable.Position);
        var martyr = notable.Kind == NotableKind.Prophet && danger > 0f && random.NextDouble() < _rules.Mortality * danger * seconds;
        var gone = village is null || village.Population <= 0;
        if (!martyr && !gone && world.Tick < notable.DiesAtTick)
        {
            // Danger lived through is remembered once for each place, the open road being nowhere.
            var place = notable.TargetSettlementId is null ? village?.Id : null;
            var remembered = notable.Memories.Count > 0 && notable.Memories[^1].Deed == NotableDeed.Endured && notable.Memories[^1].SettlementId == place;
            if (danger > 0f && !remembered)
            {
                NotableModel.Remember(_rules, notable, world.Tick, NotableDeed.Endured, place, 0);
            }

            return false;
        }

        world.Notables.Remove(notable);
        events.Add(new NotableDied { Tick = world.Tick, NotableId = notable.Id, Name = notable.Name, Kind = notable.Kind, GodId = notable.GodId, SettlementId = notable.SettlementId, Martyred = martyr });
        if (martyr)
        {
            foreach (var settlement in world.Settlements)
            {
                var near = HazardModel.Falloff(settlement.Position, notable.Position, _rules.MartyrRadius);
                if (near > 0f && BeliefModel.Find(settlement, notable.GodId) is { } belief)
                {
                    BeliefModel.ApplyImpulse(belief, _rules.Martyrdom, near, 1f);
                }
            }
        }

        return true;
    }

    /// <summary>A prophet walks to the next village, preaches there for a while, and sets out again.</summary>
    private void Wander(WorldState world, NotableState prophet, float seconds, List<WorldEvent> events)
    {
        if (prophet.TargetSettlementId is { } target)
        {
            Walk(prophet, _rules.ProphetSpeed * seconds);
            if (prophet.Path.Count == 0)
            {
                (prophet.SettlementId, prophet.TargetSettlementId, prophet.TicksLeft) = (target, null, NotableModel.Ticks(_rules.PreachSeconds));
            }

            return;
        }

        prophet.TicksLeft -= _intervalTicks;
        if (prophet.TicksLeft > 0)
        {
            return;
        }

        if (world.Settlements.Find(s => s.Id == prophet.SettlementId) is { Population: > 0 } village)
        {
            var won = NotableModel.Preach(_rules, village, prophet.GodId);
            NotableModel.Remember(_rules, prophet, world.Tick, NotableDeed.Preached, village.Id, won);
            events.Add(new NotablePreached { Tick = world.Tick, NotableId = prophet.Id, GodId = prophet.GodId, SettlementId = village.Id, Converted = won });
        }

        if (NotableModel.NextVillage(_rules, world, prophet) is { } next)
        {
            NotableModel.SetOut(world, prophet, next);
        }
        else
        {
            prophet.TicksLeft = NotableModel.Ticks(_rules.PreachSeconds);
        }
    }

    /// <summary>
    /// An elder keeps their village's faith in their god. They turn from a god the village resents,
    /// leaving doubt behind, and follow a village that has left their god for another.
    /// </summary>
    private void Lead(WorldState world, NotableState elder, float seconds, List<WorldEvent> events)
    {
        if (world.Settlements.Find(s => s.Id == elder.SettlementId) is not { } village)
        {
            return;
        }

        var belief = BeliefModel.Find(village, elder.GodId);
        var resented = belief is not null && belief.Resentment >= _rules.ElderTurnResentment;
        if ((resented || Deserted(village, belief)) && Other(village, elder.GodId) is { } other)
        {
            var from = elder.GodId;
            elder.GodId = other;
            if (resented)
            {
                belief!.Doubt = BeliefModel.ApplyChange(belief.Doubt, _rules.TurnDoubt);
            }

            NotableModel.Remember(_rules, elder, world.Tick, NotableDeed.Turned, village.Id, 0);
            events.Add(new ElderTurned { Tick = world.Tick, NotableId = elder.Id, SettlementId = village.Id, FromGodId = from, ToGodId = other, Resented = resented });
            return;
        }

        if (belief is null)
        {
            return;
        }

        var devotion = _rules.ElderDevotionPerSecond * seconds;
        belief.Faith = BeliefModel.ApplyChange(belief.Faith, devotion);
        belief.Doubt = BeliefModel.ApplyChange(belief.Doubt, -devotion);
    }

    /// <summary>Whether the village has left the elder's god: another leads it with more than twice its following.</summary>
    private static bool Deserted(SettlementState village, GodBelief? belief) =>
        MigrationModel.MainGod(village.Beliefs) is { } main && main != belief?.GodId
            && (belief?.Adherence ?? 0f) * 2f < (BeliefModel.Find(village, main)?.Adherence ?? 0f);

    /// <summary>The god most of the village follows after the one named, if any follow another.</summary>
    private static int? Other(SettlementState village, int godId)
    {
        GodBelief? best = null;
        foreach (var belief in village.Beliefs)
        {
            if (belief.GodId != godId && belief.Adherence > 0.05f && (best is null || belief.Adherence > best.Adherence))
            {
                best = belief;
            }
        }

        return best?.GodId;
    }

    /// <summary>The danger on the open road: fire or flood in the region underfoot.</summary>
    private static float DangerAt(WorldState world, GroundPosition position) =>
        world.Regions.TryGetRegionIdAt(position.X, position.Z, out var regionId) && world.Regions.Cells[regionId] is var region
            ? Math.Max(SimMath.Clamp01(region.Fire), SimMath.Clamp01(region.Flood))
            : 0f;

    /// <summary>Moves along the path by a distance.</summary>
    private static void Walk(NotableState prophet, float distance)
    {
        while (distance > 0f && prophet.Path.Count > 0)
        {
            var next = prophet.Path[0];
            var (dx, dz) = (next.X - prophet.Position.X, next.Z - prophet.Position.Z);
            var length = MathF.Sqrt((dx * dx) + (dz * dz));
            if (length <= distance)
            {
                prophet.Position = next;
                prophet.Path.RemoveAt(0);
                distance -= length;
            }
            else
            {
                prophet.Position = new GroundPosition(prophet.Position.X + (dx / length * distance), prophet.Position.Z + (dz / length * distance));
                distance = 0f;
            }
        }
    }
}
