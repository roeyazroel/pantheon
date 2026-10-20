using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Creatures;

/// <summary>What a node of a behaviour tree reports after a tick.</summary>
public enum TreeStatus
{
    /// <summary>Still going.</summary>
    Running,

    /// <summary>Done.</summary>
    Succeeded,

    /// <summary>Given up.</summary>
    Failed,
}

/// <summary>
/// The behaviour trees that run a free Creature's actions (spec §86, ADR-047). Every action's tree
/// is the same shape: an interrupt (a pressing need ends a walk or wait its god commanded), a guard
/// (the reason the action was chosen must still hold), and a sequence of travel, perform and deed.
/// The sequence's place is the activity's phase and the performance its ticks left, so the tree
/// keeps no state of its own. The utility brain chooses the action; the tree carries it out.
/// </summary>
public sealed class CreatureTree
{
    // A need this pressing ends any action that does not meet it.
    private const float DesperateNeed = 0.9f;

    // A village in need stays worth helping until its need falls below this share of the threshold that drew the Creature.
    private const float NeedHysteresis = 0.5f;

    // Metres within which a waypoint counts as reached.
    private const float ArrivalTolerance = 1e-3f;

    private readonly CreatureRules _rules;
    private readonly DivineRules _divine;
    private readonly SettlementRules _settlements;
    private readonly CreatureBrain _brain;
    private readonly CreatureDeeds _deeds;

    /// <summary>Creates the trees over the Creature's rules, brain and deeds.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules, for a village's needs.</param>
    /// <param name="settlements">Settlement rules, for a village's building.</param>
    /// <param name="brain">The brain, for how long an action takes.</param>
    /// <param name="deeds">The deeds, for what an action does when done.</param>
    internal CreatureTree(CreatureRules rules, DivineRules divine, SettlementRules settlements, CreatureBrain brain, CreatureDeeds deeds)
    {
        (_rules, _divine, _settlements, _brain, _deeds) = (rules, divine, settlements, brain, deeds);
    }

    /// <summary>Ticks a free Creature's action: travelling, performing and the deed, unless an interrupt or the guard ends it.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature, travelling or performing.</param>
    /// <param name="events">Receives the deed's events and <see cref="CreatureGaveUp"/>.</param>
    /// <returns>Running, or how it ended; the Creature then decides again.</returns>
    public TreeStatus Tick(WorldState world, CreatureState creature, List<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(events);
        var activity = creature.Activity;
        if (activity.Action == CreatureAction.GoTo && activity.Phase == CreaturePhase.Performing && Interrupt(creature) is not null)
        {
            // A pressing need ends a commanded wait as done, as its god would want.
            activity.TicksLeft = 1;
        }
        else if ((Interrupt(creature) ?? Guard(world, creature)) is { } reason)
        {
            activity.Path.Clear();
            events.Add(new CreatureGaveUp { Tick = world.Tick, CreatureId = creature.Id, Action = activity.Action, SettlementId = activity.TargetSettlementId, Reason = reason });
            return TreeStatus.Failed;
        }

        if (activity.Phase == CreaturePhase.Travelling && Hunt(world, creature) is { } lost)
        {
            activity.Path.Clear();
            events.Add(new CreatureGaveUp { Tick = world.Tick, CreatureId = creature.Id, Action = activity.Action, SettlementId = null, Reason = lost });
            return TreeStatus.Failed;
        }

        if (activity.Phase == CreaturePhase.Travelling)
        {
            Walk(creature);
            if (activity.Path.Count > 0)
            {
                return TreeStatus.Running;
            }

            // A Creature that went looking for another is done once there: meeting it is up to the
            // meetings. One its god set on a rival keeps after it until they meet (ADR-062): the hunt
            // finds it a new way next tick.
            if (activity.TargetCreatureId is { } hunted && CreatureBrain.IsHunting(creature, hunted))
            {
                return TreeStatus.Running;
            }

            if (activity.Action is CreatureAction.Fight or CreatureAction.Befriend)
            {
                return TreeStatus.Succeeded;
            }

            activity.Phase = CreaturePhase.Performing;
            activity.TicksLeft = _brain.PerformTicks(creature, activity.Action);
            return TreeStatus.Running;
        }

        if (--activity.TicksLeft > 0)
        {
            return TreeStatus.Running;
        }

        _deeds.Complete(world, creature, events);
        return TreeStatus.Succeeded;
    }

    /// <summary>
    /// Keeps a commanded hunt on its rival's trail (ADR-062): about once a second, and whenever the
    /// way runs out, it re-plans to where the rival is now. It gives up when its time runs out or
    /// the rival cannot be reached.
    /// </summary>
    /// <returns>Why it gave up, or <c>null</c> while it hunts (or does not hunt at all).</returns>
    private static GiveUpReason? Hunt(WorldState world, CreatureState creature)
    {
        var activity = creature.Activity;
        if (activity.TargetCreatureId is not { } id || !CreatureBrain.IsHunting(creature, id) || world.Creatures.Find(c => c.Id == id) is not { } rival)
        {
            return null;
        }

        if (--activity.TicksLeft <= 0)
        {
            return GiveUpReason.OtherGone;
        }

        var moved = MathF.Abs(rival.Position.X - activity.Target.X) + MathF.Abs(rival.Position.Z - activity.Target.Z);
        var due = (world.Tick + creature.Id) % SimulationTime.TicksPerSecond == 0 && moved > 10f;
        if ((due || activity.Path.Count == 0) && !CreatureBrain.Chase(world, creature, rival))
        {
            return GiveUpReason.OtherGone;
        }

        return null;
    }

    /// <summary>What the tree is doing, for the debug panel: its interrupt, its guard and where the sequence stands.</summary>
    /// <param name="creature">The Creature.</param>
    /// <returns>For example "guard: still in need ▸ travel ▶ perform · deed".</returns>
    public static string Describe(CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var phase = creature.Activity.Phase;
        var steps = phase == CreaturePhase.Travelling ? "travel ▶ perform · deed"
            : phase == CreaturePhase.Performing ? "travel ✓ perform ▶ deed"
            : "idle";
        return GuardName(creature.Activity.Action) + " ▸ " + steps;
    }

    /// <summary>
    /// The interrupt: a pressing hunger or weariness ends a walk or wait its god commanded. The
    /// Creature's own choices already weigh its needs, so they are not interrupted: one that chose
    /// to rest while starving would only choose it again.
    /// </summary>
    private static GiveUpReason? Interrupt(CreatureState creature) =>
        creature.Activity.Action == CreatureAction.GoTo && (creature.Needs.Hunger >= DesperateNeed || creature.Needs.Fatigue >= DesperateNeed) ? GiveUpReason.Desperate : null;

    /// <summary>The guard: the reason the action was chosen must still hold.</summary>
    private GiveUpReason? Guard(WorldState world, CreatureState creature)
    {
        var activity = creature.Activity;
        if (activity.TargetCreatureId is { } other && activity.Phase == CreaturePhase.Travelling
            && world.Creatures.Find(c => c.Id == other) is not { Activity.Phase: not CreaturePhase.Banished })
        {
            return GiveUpReason.OtherGone;
        }

        if (activity.Phase == CreaturePhase.Performing && activity.Action == CreatureAction.Befriend && activity.TicksLeft > 1
            && !IsPlayingWith(world, activity.TargetCreatureId, creature.Id))
        {
            return GiveUpReason.OtherGone;
        }

        if (activity.TargetSettlementId is not { } id || activity.Action == CreatureAction.GoTo)
        {
            return null;
        }

        if (world.Settlements.Find(s => s.Id == id) is not { Population: > 0 } village)
        {
            return GiveUpReason.VillageGone;
        }

        return activity.Action switch
        {
            CreatureAction.Feed => Need(village) < _rules.VillageNeedThreshold * NeedHysteresis ? GiveUpReason.NoLongerInNeed : null,
            CreatureAction.HelpBuild => SettlementEconomy.WantsConstruction(_settlements, village) ? null : GiveUpReason.NothingToBuild,
            CreatureAction.EatFromStores => village.Food > 0f ? null : GiveUpReason.StoresEmpty,
            CreatureAction.Attack => Migration.MigrationModel.MainGod(village.Beliefs) is { } main && DiplomacyModel.AreAllied(world, creature.GodId, main) ? GiveUpReason.NowAnAlly : null,
            CreatureAction.Wander or CreatureAction.Rest or CreatureAction.Forage or CreatureAction.Play or CreatureAction.Ignore
                or CreatureAction.GoTo or CreatureAction.Fight or CreatureAction.Befriend or CreatureAction.Flee => null,
        };
    }

    /// <summary>A village's hunger or illness, as the Creature sees it.</summary>
    private float Need(SettlementState village)
    {
        var conditions = BeliefModel.AssessConditions(_divine, village, Hazards.None);
        return Math.Max(conditions.FoodNeed, conditions.HealthNeed);
    }

    /// <summary>The guard's words, for the debug panel.</summary>
    private static string GuardName(CreatureAction action) => action switch
    {
        CreatureAction.Feed => "guard: still in need",
        CreatureAction.HelpBuild => "guard: still building",
        CreatureAction.EatFromStores => "guard: stores not empty",
        CreatureAction.Attack => "guard: not an ally",
        CreatureAction.Fight or CreatureAction.Befriend => "guard: the other still there",
        CreatureAction.Wander or CreatureAction.Rest or CreatureAction.Forage or CreatureAction.Play or CreatureAction.Ignore
            or CreatureAction.GoTo or CreatureAction.Flee => "no guard",
    };

    /// <summary>Whether a Creature is playing with the given one.</summary>
    private static bool IsPlayingWith(WorldState world, int? playmateId, int creatureId) =>
        world.Creatures.Find(c => c.Id == playmateId) is { Activity: { Phase: CreaturePhase.Performing, Action: CreatureAction.Befriend } activity } && activity.TargetCreatureId == creatureId;

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
}
