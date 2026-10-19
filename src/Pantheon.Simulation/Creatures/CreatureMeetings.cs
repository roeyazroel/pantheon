using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// Creatures meeting (spec §27, ADR-017). Two free Creatures within <c>MeetRadius</c> of each other
/// meet, at most once every <c>MeetCooldownSeconds</c>. Each answers by its brain: fight, play,
/// flee or walk on. One attacking starts a fight unless the other runs; two wanting to play play
/// together, unless one is on its god's errand; otherwise one wanting to play greets the other.
/// </summary>
public sealed class CreatureMeetings
{
    // Metres between two Creatures that fight or play: room for two grown Mossbacks.
    private const float FightGap = 16f;

    private readonly CreatureRules _rules;
    private readonly CreatureBrain _brain;

    /// <summary>Creates the meetings over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="brain">The brain, for each Creature's answer.</param>
    public CreatureMeetings(CreatureRules rules, CreatureBrain brain)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(brain);
        _rules = rules;
        _brain = brain;
    }

    /// <summary>Lets every pair of free Creatures close enough, and not met lately, meet, in id order.</summary>
    /// <param name="world">World state.</param>
    /// <param name="random">The simulation's random stream (for the brains' jitter and flight).</param>
    /// <param name="events">Receives <see cref="CreaturesMet"/>.</param>
    public void Update(WorldState world, SimulationRandom random, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        var creatures = world.Creatures;
        var bonds = _rules.Bonds;
        for (var i = 0; i < creatures.Count; i++)
        {
            for (var j = i + 1; j < creatures.Count; j++)
            {
                var (a, b) = (creatures[i], creatures[j]);
                if (IsFree(a) && IsFree(b) && Distance(a.Position, b.Position) <= bonds.MeetRadius
                    && CreatureBonds.CanMeet(bonds, CreatureBonds.Find(a, b.Id), world.Tick)
                    && CreatureBonds.CanMeet(bonds, CreatureBonds.Find(b, a.Id), world.Tick))
                {
                    Meet(world, a, b, random, events);
                }
            }
        }
    }

    /// <summary>Two Creatures meet now, wherever they are.</summary>
    /// <param name="world">World state.</param>
    /// <param name="a">One Creature.</param>
    /// <param name="b">The other.</param>
    /// <param name="random">The simulation's random stream.</param>
    /// <param name="events">Receives <see cref="CreaturesMet"/>.</param>
    /// <returns>What came of it.</returns>
    public MeetingOutcome Meet(WorldState world, CreatureState a, CreatureState b, SimulationRandom random, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(events);
        var bonds = _rules.Bonds;
        var tick = world.Tick;
        var viewA = CreatureBonds.Ensure(bonds, a, b.Id);
        var viewB = CreatureBonds.Ensure(bonds, b, a.Id);
        var friends = CreatureBonds.IsFriend(bonds, viewA) && CreatureBonds.IsFriend(bonds, viewB);
        viewA.LastMetTick = tick;
        viewB.LastMetTick = tick;
        var answerA = _brain.Respond(world, a, b, random);
        var answerB = _brain.Respond(world, b, a, random);
        var (outcome, actor, betrayal) = (answerA, answerB) switch
        {
            (CreatureAction.Fight, _) => Attack(world, a, b, answerB, random),
            (_, CreatureAction.Fight) => Attack(world, b, a, answerA, random),
            (CreatureAction.Flee, _) or (_, CreatureAction.Flee) => Scatter(world, a, b, answerA, answerB, random),
            (CreatureAction.Befriend, CreatureAction.Befriend) when !IsOnErrand(a) && !IsOnErrand(b) => Play(world, a, b),
            (CreatureAction.Befriend, _) or (_, CreatureAction.Befriend) => Greet(a, b, answerA == CreatureAction.Befriend ? a : b, tick),
            _ => PassBy(a, b, tick),
        };
        events.Add(new CreaturesMet
        {
            Tick = tick,
            CreatureId = actor,
            OtherCreatureId = actor == a.Id ? b.Id : a.Id,
            Outcome = outcome,
            Betrayal = betrayal,
            GodsAtWar = DiplomacyModel.AreAtWar(world, a.GodId, b.GodId),
            Friends = friends,
        });
        return outcome;
    }

    /// <summary>Whether a Creature is on its god's errand: it greets a friend in passing but does not stop to play.</summary>
    private static bool IsOnErrand(CreatureState creature) => creature.Activity.Action == CreatureAction.GoTo;

    /// <summary>One attacks: the other escapes if it flees and has somewhere to run, else they fight.</summary>
    private (MeetingOutcome, int, bool) Attack(WorldState world, CreatureState attacker, CreatureState defender, CreatureAction defence, SimulationRandom random)
    {
        var bonds = _rules.Bonds;
        if (defence == CreatureAction.Flee && _brain.Flee(world, defender, CreatureBonds.SituationFor(world, defender, attacker), attacker, random))
        {
            CreatureBonds.Fled(bonds, defender, attacker, world.Tick);
            StopSeeking(attacker, defender.Id);
            return (MeetingOutcome.Fled, defender.Id, false);
        }

        var betrayal = defence != CreatureAction.Fight && CreatureBonds.IsFriend(bonds, CreatureBonds.Find(defender, attacker.Id));
        CreatureBonds.FightStarted(bonds, attacker, defender, betrayal, world.Tick);
        FaceOff(world, attacker, defender);
        _brain.Engage(attacker, CreatureAction.Fight, CreatureBonds.SituationFor(world, attacker, defender), defender);
        _brain.Engage(defender, CreatureAction.Fight, CreatureBonds.SituationFor(world, defender, attacker), attacker);
        return (MeetingOutcome.Fought, attacker.Id, betrayal);
    }

    /// <summary>Whoever wants to flees; if neither can, they walk on.</summary>
    private (MeetingOutcome, int, bool) Scatter(WorldState world, CreatureState a, CreatureState b, CreatureAction answerA, CreatureAction answerB, SimulationRandom random)
    {
        int? first = null;
        foreach (var (self, other, answer) in new[] { (a, b, answerA), (b, a, answerB) })
        {
            if (answer == CreatureAction.Flee && _brain.Flee(world, self, CreatureBonds.SituationFor(world, self, other), other, random))
            {
                CreatureBonds.Fled(_rules.Bonds, self, other, world.Tick);
                first ??= self.Id;
            }
            else
            {
                StopSeeking(self, other.Id);
            }
        }

        return first is { } fled ? (MeetingOutcome.Fled, fled, false) : PassBy(a, b, world.Tick);
    }

    /// <summary>Both want to play: they play together, face to face.</summary>
    private (MeetingOutcome, int, bool) Play(WorldState world, CreatureState a, CreatureState b)
    {
        FaceOff(world, a, b);
        _brain.Engage(a, CreatureAction.Befriend, CreatureBonds.SituationFor(world, a, b), b);
        _brain.Engage(b, CreatureAction.Befriend, CreatureBonds.SituationFor(world, b, a), a);
        return (MeetingOutcome.Played, Math.Min(a.Id, b.Id), false);
    }

    /// <summary>One wants to play and the other does not: a greeting.</summary>
    private (MeetingOutcome, int, bool) Greet(CreatureState a, CreatureState b, CreatureState greeter, long tick)
    {
        CreatureBonds.Greeted(_rules.Bonds, a, b, tick);
        StopSeeking(a, b.Id);
        StopSeeking(b, a.Id);
        return (MeetingOutcome.Greeted, greeter.Id, false);
    }

    /// <summary>Neither cares: they walk on.</summary>
    private (MeetingOutcome, int, bool) PassBy(CreatureState a, CreatureState b, long tick)
    {
        CreatureBonds.PassedBy(_rules.Bonds, a, b, tick);
        StopSeeking(a, b.Id);
        StopSeeking(b, a.Id);
        return (MeetingOutcome.PassedBy, Math.Min(a.Id, b.Id), false);
    }

    /// <summary>A Creature that came looking for the other, and found nothing to do with it, thinks again.</summary>
    private static void StopSeeking(CreatureState creature, int otherId)
    {
        if (CreatureBrain.IsSeeking(creature, otherId))
        {
            creature.Activity.Path.Clear();
            creature.Activity.Phase = CreaturePhase.Deciding;
        }
    }

    /// <summary>Brings two Creatures to <see cref="FightGap"/> metres apart about their midpoint, where the land allows it.</summary>
    private static void FaceOff(WorldState world, CreatureState a, CreatureState b)
    {
        var dx = b.Position.X - a.Position.X;
        var dz = b.Position.Z - a.Position.Z;
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        if (length <= FightGap)
        {
            return;
        }

        var (ux, uz) = (dx / length, dz / length);
        var (mx, mz) = ((a.Position.X + b.Position.X) / 2f, (a.Position.Z + b.Position.Z) / 2f);
        var half = FightGap / 2f;
        var toA = new GroundPosition(mx - (ux * half), mz - (uz * half));
        var toB = new GroundPosition(mx + (ux * half), mz + (uz * half));
        if (IsLand(world, toA) && IsLand(world, toB))
        {
            a.Position = toA;
            b.Position = toB;
        }
    }

    /// <summary>Whether a point is walkable land.</summary>
    private static bool IsLand(WorldState world, GroundPosition point) =>
        world.Regions.TryGetRegionIdAt(point.X, point.Z, out var region) && RegionPathfinder.IsWalkable(world.Regions.Cells[region]);

    /// <summary>A Creature that is not banished, fighting or playing.</summary>
    private static bool IsFree(CreatureState creature) => creature.Activity.Phase != CreaturePhase.Banished && !CreatureBrain.IsEngaged(creature);

    /// <summary>Distance between two ground positions in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
