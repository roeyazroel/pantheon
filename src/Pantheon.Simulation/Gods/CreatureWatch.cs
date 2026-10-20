using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Politics;

namespace Pantheon.Simulation.Gods;

/// <summary>
/// Gods keeping watch over the villages they lead (ADR-070). When another god's Creature comes near
/// one, the god answers at once, by how it stands with the visitor's god: a friend's is welcomed and
/// its own Creature comes to greet it; a stranger's is watched, its own Creature coming to follow it,
/// and the trespass held against the visitor's god; a foe's is driven out, its own Creature set on it
/// when it is a match for it. A player's god is told, and chooses for itself.
/// </summary>
public sealed class CreatureWatch
{
    private readonly WatchRules _rules;
    private readonly CreatureRules _creatureRules;
    private readonly CreatureAuthority _creatures;
    private readonly IActRecorder? _ledger;
    private readonly Func<GodState, GodProfile?> _profileOf;

    /// <summary>Creates the watch.</summary>
    /// <param name="rules">How gods keep watch.</param>
    /// <param name="creatureRules">Creature rules, for a Creature's confidence and fitness.</param>
    /// <param name="creatures">The Creature authority, through which a god orders its Creature.</param>
    /// <param name="ledger">Where a god remembers a trespass or a friend's visit; <c>null</c> for none.</param>
    /// <param name="profileOf">A god's profile, its temper applied, for its appetite for war.</param>
    public CreatureWatch(WatchRules rules, CreatureRules creatureRules, CreatureAuthority creatures, IActRecorder? ledger, Func<GodState, GodProfile?> profileOf)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(creatureRules);
        ArgumentNullException.ThrowIfNull(creatures);
        ArgumentNullException.ThrowIfNull(profileOf);
        (_rules, _creatureRules, _creatures, _ledger, _profileOf) = (rules, creatureRules, creatures, ledger, profileOf);
    }

    /// <summary>Every god looks over its villages, when the watch is due.</summary>
    /// <param name="world">World state.</param>
    /// <param name="events">Receives <see cref="CreatureWatched"/> and the orders' events.</param>
    public void Update(WorldState world, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        if (_rules.Radius <= 0f || world.Tick % Math.Max(1, _rules.IntervalTicks) != 0)
        {
            return;
        }

        foreach (var god in world.Gods)
        {
            Look(world, god, events);
        }
    }

    /// <summary>
    /// How hostile a god is to another god's Creature in its lands (0..1): by their standing, its view
    /// of the other (rivalry and suspicion against trust) and its appetite for war; a treaty that spares
    /// their Creatures, or an alliance, keeps it friendly.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="god">The god whose land it is.</param>
    /// <param name="visitorGodId">The visitor's god.</param>
    /// <param name="profile">The god's profile, if it has one.</param>
    /// <returns>Its hostility.</returns>
    public static float Hostility(WorldState world, GodState god, int visitorGodId, GodProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(god);
        var standing = DiplomacyModel.StatusOf(world, god.Id, visitorGodId) switch
        {
            DiplomaticStatus.War => 1f,
            DiplomaticStatus.Hostile => 0.72f,
            DiplomaticStatus.Neutral => 0.42f,
            DiplomaticStatus.Friendly => 0.15f,
            DiplomaticStatus.Alliance => 0f,
        };
        var view = DiplomacyModel.FindRelation(world, god.Id, visitorGodId);
        var feeling = view is null ? 0f : 0.3f * (view.Rivalry + (0.5f * view.Suspicion) - view.Trust);
        var temper = profile is null ? 0f : 0.25f * (profile.Aggression - 0.5f);
        var hostility = SimMath.Clamp01(standing + feeling + temper);
        return TreatyModel.SparesCreatures(world, god.Id, visitorGodId) || DiplomacyModel.AreAllied(world, god.Id, visitorGodId)
            ? Math.Min(hostility, 0.2f)
            : DiplomacyModel.AreAtWar(world, god.Id, visitorGodId) ? 1f : hostility;
    }

    /// <summary>How a god answers a visitor of a hostility: welcome, watch or drive out.</summary>
    /// <param name="rules">How gods keep watch.</param>
    /// <param name="hostility">Its hostility (0..1).</param>
    /// <returns>The answer.</returns>
    public static WatchAnswer AnswerFor(WatchRules rules, float hostility)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return hostility >= rules.RepelHostility ? WatchAnswer.Repel : hostility <= rules.WelcomeHostility ? WatchAnswer.Welcome : WatchAnswer.Watch;
    }

    /// <summary>
    /// One god's look over its villages: every visitor is remembered and told of once in the cooldown,
    /// and its own Creature goes after the most pressing visitor (a foe's before a stranger's before a
    /// friend's, the nearest first).
    /// </summary>
    private void Look(WorldState world, GodState god, List<WorldEvent> events)
    {
        (CreatureState Visitor, SettlementState Village, WatchAnswer Answer, CreatureWatched? Told)? first = null;
        var told = new List<CreatureWatched>();
        foreach (var (visitor, village) in Visits(world, god))
        {
            var feeling = AnswerFor(_rules, Hostility(world, god, visitor.GodId, _profileOf(god)));
            var answer = god.Controller == GodController.Ai ? feeling : WatchAnswer.Unanswered;
            CreatureWatched? news = null;
            if (Fresh(world, god, visitor.GodId))
            {
                _ledger?.Record(world, god.Id, visitor.GodId, feeling == WatchAnswer.Welcome ? DiplomaticAct.Visited : DiplomaticAct.Trespassed, village.Id);
                news = new CreatureWatched { Tick = world.Tick, GodId = god.Id, VisitorGodId = visitor.GodId, CreatureId = visitor.Id, SettlementId = village.Id, Answer = answer };
                told.Add(news);
            }

            if (answer != WatchAnswer.Unanswered && (first is null || Urgency(answer) > Urgency(first.Value.Answer)))
            {
                first = (visitor, village, answer, news);
            }
        }

        var guard = god.CreatureId is { } id ? world.Creatures.Find(c => c.Id == id) : null;
        var (sent, ordered) = first is { } pressing ? Send(world, god, guard, pressing.Visitor, pressing.Answer, pressing.Village, pressing.Told is not null, events) : (null, false);
        foreach (var news in told)
        {
            events.Add(first is { } top && ReferenceEquals(top.Told, news) ? news with { GuardId = sent } : news);
        }

        if (ordered && first is { Told: null, Answer: WatchAnswer.Repel } later && guard is not null && CreatureBrain.IsHunting(guard, later.Visitor.Id))
        {
            // A guard set on a foe later, once it could go, is told of too.
            events.Add(new CreatureWatched { Tick = world.Tick, GodId = god.Id, VisitorGodId = later.Visitor.GodId, CreatureId = later.Visitor.Id, SettlementId = later.Village.Id, Answer = later.Answer, GuardId = sent });
        }
    }

    /// <summary>How pressing an answer is: driving a foe out before watching a stranger before greeting a friend.</summary>
    private static int Urgency(WatchAnswer answer) => answer switch
    {
        WatchAnswer.Repel => 3,
        WatchAnswer.Watch => 2,
        WatchAnswer.Welcome => 1,
        WatchAnswer.Unanswered => 0,
    };

    /// <summary>Every other god's Creature near a village the god leads, with the nearest such village, nearest first.</summary>
    private List<(CreatureState Visitor, SettlementState Village)> Visits(WorldState world, GodState god)
    {
        var visits = new List<(CreatureState Visitor, SettlementState Village, float Squared)>();
        foreach (var visitor in world.Creatures)
        {
            if (visitor.GodId == god.Id || visitor.Activity.Phase == CreaturePhase.Banished || CreatureModel.IsHeld(world, visitor))
            {
                continue;
            }

            SettlementState? nearest = null;
            var bestSquared = _rules.Radius * _rules.Radius;
            foreach (var village in world.Settlements)
            {
                var (dx, dz) = (village.Position.X - visitor.Position.X, village.Position.Z - visitor.Position.Z);
                var squared = (dx * dx) + (dz * dz);
                if (squared <= bestSquared && village.Population > 0 && TreatyModel.Leader(village) == god.Id)
                {
                    (nearest, bestSquared) = (village, squared);
                }
            }

            if (nearest is not null)
            {
                visits.Add((visitor, nearest, bestSquared));
            }
        }

        visits.Sort((a, b) => a.Squared.CompareTo(b.Squared));
        return visits.ConvertAll(v => (v.Visitor, v.Village));
    }

    /// <summary>
    /// Orders the god's own Creature, when it can go (not fighting, held, banished or badly hurt) and is
    /// not already about it: set on a foe's visitor it is a match for, else sent to it, to greet or to
    /// follow it; a foe it is no match for, it guards the village against. Returns the Creature, and
    /// whether a new order went out.
    /// </summary>
    private (int? Guard, bool Ordered) Send(WorldState world, GodState god, CreatureState? guard, CreatureState visitor, WatchAnswer answer, SettlementState village, bool fresh, List<WorldEvent> events)
    {
        if (guard is null || guard.Activity.Phase == CreaturePhase.Banished || CreatureModel.IsHeld(world, guard) || CreatureCombat.IsFighting(guard) || guard.Health < 0.4f)
        {
            return (null, false);
        }

        var repel = answer == WatchAnswer.Repel && !CreatureCombat.Recovering(_creatureRules, guard, world.Tick)
            && CreatureBonds.Confidence(_creatureRules, guard, visitor) >= _rules.RepelConfidence;
        var post = repel || answer != WatchAnswer.Repel ? visitor.Position : village.Position;
        if (repel ? CreatureBrain.IsHunting(guard, visitor.Id) : OnItsWay(guard, post))
        {
            return (guard.Id, false);
        }

        // Welcomed, a visitor is greeted once: the guard does not trail a friend about.
        if (answer == WatchAnswer.Welcome && !fresh)
        {
            return (null, false);
        }

        var order = new CreatureCommand(god.Id, guard.Id, repel ? CreatureCommandKind.Fight : CreatureCommandKind.GoTo, post);
        return _creatures.Execute(world, order, god.Id, events).IsAccepted ? (guard.Id, true) : (null, false);
    }

    /// <summary>Whether the guard is already going to a place.</summary>
    private bool OnItsWay(CreatureState guard, GroundPosition place)
    {
        var activity = guard.Activity;
        var (dx, dz) = (activity.Target.X - place.X, activity.Target.Z - place.Z);
        var near = _creatureRules.Bonds.MeetRadius;
        return activity.Action == CreatureAction.GoTo && activity.Phase != CreaturePhase.Deciding && (dx * dx) + (dz * dz) <= near * near;
    }

    /// <summary>Whether the god has not remembered a visit of this god's Creature within the cooldown.</summary>
    private bool Fresh(WorldState world, GodState god, int visitorGodId)
    {
        var cooldown = (long)(_rules.CooldownSeconds / SimulationTime.SecondsPerTick);
        var view = DiplomacyModel.FindRelation(world, god.Id, visitorGodId);
        return view is null || !view.Memories.Exists(m => m.Act is DiplomaticAct.Trespassed or DiplomaticAct.Visited && world.Tick - m.Tick < cooldown);
    }
}
