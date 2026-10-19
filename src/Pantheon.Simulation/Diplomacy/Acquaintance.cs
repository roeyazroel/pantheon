using System;
using System.Collections.Generic;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>
/// How gods come to know of each other (spec §11's KnownGods, ADR-036). A god knows only the gods
/// it has met: their villages are neighbours, share a road or share believers, their Creatures
/// met, one's deeds touched the other's people, or they sat in the Divine Council. Once met, a pair
/// knows each other for good. Until then neither may ally with, war on, sign with or ask of the other.
/// </summary>
public static class Acquaintance
{
    /// <summary>Whether a god knows another: itself, any god when meeting first is not required, or one it has met.</summary>
    /// <param name="rules">Acquaintance rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <returns><c>true</c> when it knows the other.</returns>
    public static bool Knows(AcquaintanceRules rules, WorldState world, int godId, int otherGodId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        return !rules.MeetFirst || godId == otherGodId || GodStanding.Find(world, godId)?.KnownGods.Contains(otherGodId) == true;
    }

    /// <summary>Brings two gods to know each other, announcing it when they had not met.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">One god.</param>
    /// <param name="otherGodId">The other.</param>
    /// <param name="way">How they met.</param>
    /// <param name="settlementId">Where, if at a village.</param>
    /// <param name="events">Receives <see cref="GodsMet"/>.</param>
    public static void Introduce(WorldState world, int godId, int otherGodId, MeetingWay way, int? settlementId, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        if (godId == otherGodId || GodStanding.Find(world, godId) is not { } god || GodStanding.Find(world, otherGodId) is not { } other
            || (god.KnownGods.Contains(otherGodId) && other.KnownGods.Contains(godId)))
        {
            return;
        }

        Learn(god.KnownGods, otherGodId);
        Learn(other.KnownGods, godId);
        events.Add(new GodsMet { Tick = world.Tick, GodId = Math.Min(godId, otherGodId), OtherGodId = Math.Max(godId, otherGodId), Way = way, SettlementId = settlementId });
    }

    /// <summary>One step: every pair that crossed paths and had not met meets.</summary>
    /// <param name="rules">Acquaintance rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="events">Receives <see cref="GodsMet"/>.</param>
    public static void Step(AcquaintanceRules rules, WorldState world, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        if (!rules.MeetFirst || AllMet(world))
        {
            return;
        }

        if (world.Diplomacy.Council.Session is not null)
        {
            foreach (var god in world.Gods)
            {
                foreach (var other in world.Gods)
                {
                    Introduce(world, god.Id, other.Id, MeetingWay.Council, null, events);
                }
            }
        }

        MeetInDealings(world, events);
        MeetThroughCreatures(world, events);
        MeetAlongRoads(world, events);
        MeetInVillages(rules, world, events);
    }

    /// <summary>Gods whose deeds touched each other, or that have dealings already.</summary>
    private static void MeetInDealings(WorldState world, ICollection<WorldEvent> events)
    {
        foreach (var relation in world.Diplomacy.Relations)
        {
            if (relation.Memories.Count > 0)
            {
                Introduce(world, relation.ObserverGodId, relation.SubjectGodId, MeetingWay.Deeds, null, events);
            }
        }

        foreach (var stance in world.Diplomacy.Stances)
        {
            Introduce(world, stance.GodA, stance.GodB, MeetingWay.Diplomacy, null, events);
        }

        foreach (var treaty in world.Diplomacy.Treaties)
        {
            Introduce(world, treaty.FirstGodId, treaty.SecondGodId, MeetingWay.Diplomacy, null, events);
        }

        foreach (var proposal in world.Diplomacy.Proposals)
        {
            Introduce(world, proposal.FromGodId, proposal.ToGodId, MeetingWay.Diplomacy, null, events);
        }
    }

    /// <summary>Gods whose Creatures know each other.</summary>
    private static void MeetThroughCreatures(WorldState world, ICollection<WorldEvent> events)
    {
        foreach (var creature in world.Creatures)
        {
            foreach (var relation in creature.Relations)
            {
                if (world.Creatures.Find(c => c.Id == relation.OtherCreatureId) is { } other)
                {
                    Introduce(world, creature.GodId, other.GodId, MeetingWay.Creatures, null, events);
                }
            }
        }
    }

    /// <summary>Gods whose villages a whole road joins.</summary>
    private static void MeetAlongRoads(WorldState world, ICollection<WorldEvent> events)
    {
        foreach (var road in world.Roads)
        {
            if (road.Built >= road.Length && world.Settlements.Find(s => s.Id == road.FromSettlementId) is { } from && world.Settlements.Find(s => s.Id == road.ToSettlementId) is { } to
                && TreatyModel.Leader(from) is { } a && TreatyModel.Leader(to) is { } b)
            {
                Introduce(world, a, b, MeetingWay.Road, to.Id, events);
            }
        }
    }

    /// <summary>Gods followed in one village, and gods whose villages are neighbours.</summary>
    private static void MeetInVillages(AcquaintanceRules rules, WorldState world, ICollection<WorldEvent> events)
    {
        var reach = rules.NeighbourMetres * rules.NeighbourMetres;
        for (var i = 0; i < world.Settlements.Count; i++)
        {
            var settlement = world.Settlements[i];
            if (settlement.Population <= 0)
            {
                continue;
            }

            foreach (var belief in settlement.Beliefs)
            {
                foreach (var other in settlement.Beliefs)
                {
                    if (belief.GodId < other.GodId && belief.Adherence >= rules.SharedAdherence && other.Adherence >= rules.SharedAdherence)
                    {
                        Introduce(world, belief.GodId, other.GodId, MeetingWay.SharedVillage, settlement.Id, events);
                    }
                }
            }

            if (TreatyModel.Leader(settlement) is not { } leader)
            {
                continue;
            }

            for (var j = i + 1; j < world.Settlements.Count; j++)
            {
                var neighbour = world.Settlements[j];
                var dx = neighbour.Position.X - settlement.Position.X;
                var dz = neighbour.Position.Z - settlement.Position.Z;
                if (neighbour.Population > 0 && (dx * dx) + (dz * dz) <= reach && TreatyModel.Leader(neighbour) is { } theirs && theirs != leader)
                {
                    Introduce(world, leader, theirs, MeetingWay.Neighbours, neighbour.Id, events);
                }
            }
        }
    }

    /// <summary>Whether every god already knows every other.</summary>
    private static bool AllMet(WorldState world)
    {
        foreach (var god in world.Gods)
        {
            foreach (var other in world.Gods)
            {
                if (other.Id != god.Id && god.KnownGods.BinarySearch(other.Id) < 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Adds a god to a list of known gods, keeping it sorted.</summary>
    private static void Learn(List<int> known, int godId)
    {
        var at = known.BinarySearch(godId);
        if (at < 0)
        {
            known.Insert(~at, godId);
        }
    }
}
