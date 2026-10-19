using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// Simulates every Creature each tick (spec §80 Tier 3, §89, ADR-010): its needs and health, its
/// state machine (deciding, travelling, performing, banished), its walk along its path, and the
/// effects of what it finishes. It decides at most once every <c>DecisionIntervalTicks</c> ticks.
/// </summary>
public sealed class CreatureSystem : ISimulationSystem
{
    // A returning Creature comes back with this much health and hunger.
    private const float ReturnHealth = 0.5f;
    private const float ReturnHunger = 0.3f;

    // A need this pressing makes a waiting Creature give up the wait.
    private const float DesperateNeed = 0.9f;

    // Metres within which a waypoint counts as reached.
    private const float ArrivalTolerance = 1e-3f;

    private readonly CreatureRules _rules;
    private readonly CreatureBrain _brain;
    private readonly CreatureDeeds _deeds;
    private readonly CreatureMeetings _meetings;
    private readonly CreatureCombat _combat;

    /// <summary>Creates the system over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules.</param>
    /// <param name="settlementRules">Settlement rules.</param>
    /// <param name="diplomacy">Diplomacy rules, so Creatures turn on their god's enemies and spare its allies; <c>null</c> to ignore diplomacy.</param>
    /// <param name="ledger">Where attacks on other gods' followers are recorded; <c>null</c> for none.</param>
    /// <param name="domains">Domain rules, for the War domain's harder blows (ADR-021); <c>null</c> for none.</param>
    public CreatureSystem(CreatureRules rules, DivineRules divine, SettlementRules settlementRules, DiplomacyRules? diplomacy = null, IActRecorder? ledger = null, Domain.Gods.DomainRules? domains = null)
    {
        CreatureRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(divine);
        ArgumentNullException.ThrowIfNull(settlementRules);
        _rules = rules;
        _brain = new CreatureBrain(rules, divine, settlementRules, diplomacy);
        _deeds = new CreatureDeeds(rules, divine, settlementRules, ledger);
        _meetings = new CreatureMeetings(rules, _brain);
        _combat = new CreatureCombat(rules, ledger, domains);
    }

    /// <summary>The brain, for previews and commands.</summary>
    public CreatureBrain Brain => _brain;

    /// <summary>The meetings between Creatures, for debug tools.</summary>
    public CreatureMeetings Meetings => _meetings;

    /// <summary>
    /// Steps every Creature, then lets free Creatures that are close meet (on decision ticks), plays a
    /// tick of every fight, and once a second lets the Creatures' views of each other fade (ADR-017).
    /// </summary>
    /// <param name="context">The tick.</param>
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var decides = IntervalSystem.IsDue(world.Tick, _rules.DecisionIntervalTicks);
        foreach (var creature in world.Creatures)
        {
            // A Creature in its god's hand waits there until it is set down (ADR-030).
            if (!CreatureModel.IsHeld(world, creature))
            {
                Step(context, creature, decides);
            }

            CreatureCalling.Grow(_rules.Callings, creature, world.Tick, context.Events);
        }

        if (decides)
        {
            _meetings.Update(world, context.Random, context.Events);
        }

        _combat.Update(world, context.Events);
        if (IntervalSystem.IsDue(world.Tick, SimulationTime.TicksPerSecond))
        {
            foreach (var creature in world.Creatures)
            {
                CreatureBonds.Fade(_rules.Bonds, creature, 1f);
            }
        }
    }

    /// <summary>One tick of one Creature.</summary>
    private void Step(TickContext context, CreatureState creature, bool decides)
    {
        var world = context.World;
        var activity = creature.Activity;
        if (activity.Phase == CreaturePhase.Banished)
        {
            if (--activity.TicksLeft <= 0)
            {
                Return(world, creature, context.Events);
            }

            return;
        }

        CreatureModel.GrowNeeds(_rules, creature.Needs, SimulationTime.SecondsPerTick);
        UpdateHealth(creature);
        if (creature.Health <= 0f)
        {
            Banish(world, creature, context.Events);
            return;
        }

        switch (activity.Phase)
        {
            case CreaturePhase.Deciding:
                if (decides)
                {
                    _brain.Decide(world, creature, context.Random);
                }

                break;
            case CreaturePhase.Travelling:
                if (activity.Action == CreatureAction.GoTo && IsDesperate(creature))
                {
                    activity.Path.Clear();
                    activity.Phase = CreaturePhase.Deciding;
                    break;
                }

                if (activity.TargetCreatureId is { } sought && !IsAround(world, sought))
                {
                    activity.Path.Clear();
                    activity.Phase = CreaturePhase.Deciding;
                    break;
                }

                Walk(creature);
                if (activity.Path.Count == 0)
                {
                    // A Creature that went looking for another thinks again when it gets there; meeting it is up to the meetings.
                    var seeking = activity.Action is CreatureAction.Fight or CreatureAction.Befriend;
                    activity.Phase = seeking ? CreaturePhase.Deciding : CreaturePhase.Performing;
                    activity.TicksLeft = seeking ? 0 : _brain.PerformTicks(creature, activity.Action);
                }

                break;
            case CreaturePhase.Performing when CreatureCombat.IsFighting(creature):
                // The combat plays the fight out.
                break;
            case CreaturePhase.Performing when activity.Action == CreatureAction.Befriend && activity.TicksLeft > 1 && !IsPlayingWith(world, activity.TargetCreatureId, creature.Id):
                // Its playmate went off before the game was over (both finish on the same tick otherwise).
                activity.Phase = CreaturePhase.Deciding;
                break;
            case CreaturePhase.Performing:
                if (activity.Action == CreatureAction.GoTo && IsDesperate(creature))
                {
                    activity.TicksLeft = 0;
                }

                if (--activity.TicksLeft <= 0)
                {
                    _deeds.Complete(world, creature, context.Events);
                    activity.Phase = CreaturePhase.Deciding;
                }

                break;
            case CreaturePhase.Banished:
                break;
        }
    }

    /// <summary>Whether a Creature is playing with the given one.</summary>
    private static bool IsPlayingWith(WorldState world, int? playmateId, int creatureId) =>
        world.Creatures.Find(c => c.Id == playmateId) is { Activity: { Phase: CreaturePhase.Performing, Action: CreatureAction.Befriend } activity } && activity.TargetCreatureId == creatureId;

    /// <summary>Whether another Creature is in the world and not banished.</summary>
    private static bool IsAround(WorldState world, int creatureId) =>
        world.Creatures.Find(c => c.Id == creatureId) is { } other && other.Activity.Phase != CreaturePhase.Banished;

    /// <summary>Whether a need is so pressing that the Creature gives up walking to, or waiting at, where it was sent.</summary>
    private static bool IsDesperate(CreatureState creature) => creature.Needs.Hunger >= DesperateNeed || creature.Needs.Fatigue >= DesperateNeed;

    /// <summary>Starvation wears health down; resting restores it.</summary>
    private void UpdateHealth(CreatureState creature)
    {
        var activity = creature.Activity;
        var resting = activity.Action == CreatureAction.Rest && activity.Phase == CreaturePhase.Performing;
        if (creature.Needs.Hunger >= 1f)
        {
            creature.Health -= _rules.StarvationHealthLossPerSecond * SimulationTime.SecondsPerTick / CreatureAttributes.Resilience(_rules, creature);
        }
        else if (resting)
        {
            creature.Health += _rules.RestHealthPerSecond * SimulationTime.SecondsPerTick;
        }

        creature.Health = SimMath.Clamp01(creature.Health);
    }

    /// <summary>Moves the Creature along its path at its speed, carrying any leftover distance to the next waypoint.</summary>
    private void Walk(CreatureState creature)
    {
        var path = creature.Activity.Path;
        var step = CreatureModel.Speed(_rules, creature) * SimulationTime.SecondsPerTick;
        while (step > 0f && path.Count > 0)
        {
            var next = path[0];
            var dx = next.X - creature.Position.X;
            var dz = next.Z - creature.Position.Z;
            var distance = MathF.Sqrt((dx * dx) + (dz * dz));
            if (distance > 1e-4f)
            {
                creature.Heading = MathF.Atan2(dx, dz);
            }

            // A hair of tolerance, so rounding never leaves the Creature a millimetre short for a tick.
            if (distance <= step + ArrivalTolerance)
            {
                creature.Position = next;
                path.RemoveAt(0);
                step -= distance;
            }
            else
            {
                creature.Position = new GroundPosition(creature.Position.X + (dx / distance * step), creature.Position.Z + (dz / distance * step));
                step = 0f;
            }
        }
    }

    /// <summary>Starvation (spec §28): the Creature leaves for a while.</summary>
    private void Banish(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        CreatureBanishment.Banish(_rules, creature);
        events.Add(new CreatureChanged { Tick = world.Tick, CreatureId = creature.Id, Change = CreatureChange.Banished });
    }

    /// <summary>The Creature comes back to its home settlement, weakened.</summary>
    private static void Return(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        var home = world.Settlements.Find(s => s.Id == creature.HomeSettlementId) ?? (world.Settlements.Count > 0 ? world.Settlements[0] : null);
        if (home is not null)
        {
            creature.Position = CreatureFounder.SpawnPoint(world.Regions, home.Position);
        }

        creature.Health = ReturnHealth;
        creature.Needs.Hunger = ReturnHunger;
        creature.Activity.Phase = CreaturePhase.Deciding;
        creature.Activity.TicksLeft = 0;
        events.Add(new CreatureChanged { Tick = world.Tick, CreatureId = creature.Id, Change = CreatureChange.Returned });
    }
}
