using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// A Creature's choice of what to do next (spec §25, §86, ADR-010): it lists the options its
/// needs and the settlements it sees offer, scores each by learned preference × drive ×
/// personality × closeness, adds a little seeded jitter and takes the best one it can reach.
/// </summary>
public sealed class CreatureBrain
{
    // A Creature stands this far from a settlement's centre while it acts there.
    private const float StandOff = 30f;

    // Wander targets are tried this many times before the Creature stays put.
    private const int WanderAttempts = 6;

    private readonly CreatureRules _rules;
    private readonly DivineRules _divine;
    private readonly SettlementRules _settlementRules;

    /// <summary>Creates a brain over validated rules.</summary>
    /// <param name="rules">Creature rules.</param>
    /// <param name="divine">Divine rules, for how settlements are faring.</param>
    /// <param name="settlementRules">Settlement rules, for construction.</param>
    public CreatureBrain(CreatureRules rules, DivineRules divine, SettlementRules settlementRules)
    {
        _rules = rules;
        _divine = divine;
        _settlementRules = settlementRules;
    }

    /// <summary>Every option the Creature has now, best first, without jitter (for decisions and the debug view).</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <returns>The options.</returns>
    public List<CreatureOption> ListOptions(WorldState world, CreatureState creature)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        var options = new List<CreatureOption>();
        var needs = creature.Needs;
        var here = creature.Position;
        if (needs.Hunger >= _rules.HungryThreshold)
        {
            Add(options, creature, CreatureSituation.Hungry, CreatureAction.Forage, null, here, needs.Hunger);
        }

        if (needs.Fatigue >= _rules.TiredThreshold)
        {
            Add(options, creature, CreatureSituation.Tired, CreatureAction.Rest, null, here, needs.Fatigue);
        }

        if (needs.Boredom >= _rules.BoredThreshold)
        {
            Add(options, creature, CreatureSituation.Bored, CreatureAction.Wander, null, here, needs.Boredom);
        }

        Add(options, creature, CreatureSituation.Idle, CreatureAction.Wander, null, here, 0.2f + (0.3f * needs.Boredom));
        Add(options, creature, CreatureSituation.Idle, CreatureAction.Rest, null, here, 0.5f * needs.Fatigue);
        foreach (var settlement in world.Settlements)
        {
            var distance = Distance(here, settlement.Position);
            if (distance <= _rules.PerceptionRadius && settlement.Population > 0)
            {
                AddVillageOptions(options, creature, settlement, distance);
            }
        }

        options.Sort(static (a, b) => b.Utility.CompareTo(a.Utility));
        return options;
    }

    /// <summary>
    /// Chooses and starts the Creature's next activity: the option with the highest utility plus
    /// jitter that it can reach over land. It always finds one, because resting needs no path.
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature, which is changed.</param>
    /// <param name="random">The simulation's random stream (for jitter and wandering).</param>
    public void Decide(WorldState world, CreatureState creature, SimulationRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var options = ListOptions(world, creature);
        var scored = new List<(float Score, CreatureOption Option)>(options.Count);
        foreach (var option in options)
        {
            scored.Add((option.Utility + (_rules.DecisionJitter * random.NextFloat()), option));
        }

        scored.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        foreach (var (_, option) in scored)
        {
            if (TryStart(world, creature, option, random))
            {
                return;
            }
        }

        Start(creature, CreatureSituation.Idle, CreatureAction.Rest, null, creature.Position, []);
    }

    /// <summary>Sends the Creature to a place at its god's command.</summary>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="target">Where to go.</param>
    /// <returns><c>false</c> when it cannot get there over land.</returns>
    public bool Command(WorldState world, CreatureState creature, GroundPosition target)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        if (RegionPathfinder.FindPath(world.Regions, creature.Position, target) is not { } path)
        {
            return false;
        }

        Start(creature, CreatureSituation.Commanded, CreatureAction.GoTo, null, target, path);
        return true;
    }

    /// <summary>Options a settlement offers, by how it is faring.</summary>
    private void AddVillageOptions(List<CreatureOption> options, CreatureState creature, SettlementState settlement, float distance)
    {
        var conditions = BeliefModel.AssessConditions(_divine, settlement);
        var need = Math.Max(conditions.FoodNeed, conditions.HealthNeed);
        var closeness = 1f / (1f + (distance / _rules.DistanceHalfUtility));
        var hunger = creature.Needs.Hunger;
        var spot = StandBeside(settlement.Position, creature.Position);
        var id = settlement.Id;
        if (hunger >= _rules.HungryThreshold && settlement.Food > 0f)
        {
            Add(options, creature, CreatureSituation.Hungry, CreatureAction.EatFromStores, id, spot, hunger * closeness);
        }

        if (need >= _rules.VillageNeedThreshold)
        {
            const CreatureSituation inNeed = CreatureSituation.VillageInNeed;
            Add(options, creature, inNeed, CreatureAction.Feed, id, spot, need * closeness);
            Add(options, creature, inNeed, CreatureAction.EatFromStores, id, spot, (0.3f + (0.7f * hunger)) * closeness * (settlement.Food > 0f ? 1f : 0f));
            Add(options, creature, inNeed, CreatureAction.Attack, id, spot, 0.35f * closeness);
            Add(options, creature, inNeed, CreatureAction.Ignore, id, spot, 0.3f * closeness);
        }
        else if (SettlementEconomy.WantsConstruction(_settlementRules, settlement))
        {
            const CreatureSituation building = CreatureSituation.VillageBuilding;
            Add(options, creature, building, CreatureAction.HelpBuild, id, spot, 0.5f * closeness);
            Add(options, creature, building, CreatureAction.Attack, id, spot, 0.35f * closeness);
            Add(options, creature, building, CreatureAction.Ignore, id, spot, 0.3f * closeness);
        }
        else
        {
            const CreatureSituation thriving = CreatureSituation.VillageThriving;
            Add(options, creature, thriving, CreatureAction.Play, id, spot, (0.3f + (0.7f * creature.Needs.Boredom)) * closeness);
            Add(options, creature, thriving, CreatureAction.Attack, id, spot, 0.35f * closeness);
            Add(options, creature, thriving, CreatureAction.Ignore, id, spot, 0.3f * closeness);
        }
    }

    /// <summary>Adds an option scored by preference × drive × personality.</summary>
    private static void Add(List<CreatureOption> options, CreatureState creature, CreatureSituation situation, CreatureAction action, int? settlementId, GroundPosition target, float drive)
    {
        if (drive <= 0f)
        {
            return;
        }

        var personality = CreatureModel.TraitOf(action) is { } trait ? 0.5f + CreatureModel.Get(creature.Personality, trait) : 1f;
        var utility = CreatureModel.Preference(creature, situation, action) * drive * personality;
        options.Add(new CreatureOption(situation, action, settlementId, target, utility));
    }

    /// <summary>Plans the route to an option's target and starts it; wandering and ignoring pick a destination first.</summary>
    private bool TryStart(WorldState world, CreatureState creature, CreatureOption option, SimulationRandom random)
    {
        var target = option.Action switch
        {
            CreatureAction.Wander => PickRoamTarget(world, creature.Position, creature.Position, random),
            CreatureAction.Ignore => PickRoamTarget(world, creature.Position, option.Target, random, away: true),
            CreatureAction.Forage or CreatureAction.Rest or CreatureAction.EatFromStores or CreatureAction.Feed
                or CreatureAction.HelpBuild or CreatureAction.Play or CreatureAction.Attack or CreatureAction.GoTo => option.Target,
        };
        if (target is not { } destination)
        {
            return false;
        }

        if (Distance(creature.Position, destination) < 0.5f)
        {
            Start(creature, option.Situation, option.Action, option.SettlementId, destination, []);
            return true;
        }

        if (RegionPathfinder.FindPath(world.Regions, creature.Position, destination) is not { } path)
        {
            return false;
        }

        Start(creature, option.Situation, option.Action, option.SettlementId, destination, path);
        return true;
    }

    /// <summary>A land point about <c>Wander.Amount</c> metres away, in a random direction (or away from a place).</summary>
    private GroundPosition? PickRoamTarget(WorldState world, GroundPosition from, GroundPosition avoid, SimulationRandom random, bool away = false)
    {
        var distance = CreatureModel.RuleFor(_rules, CreatureAction.Wander).Amount;
        var baseAngle = MathF.Atan2(from.X - avoid.X, from.Z - avoid.Z);
        for (var i = 0; i < WanderAttempts; i++)
        {
            var angle = away ? baseAngle + ((random.NextFloat() - 0.5f) * MathF.PI * 0.6f) : random.NextFloat() * MathF.Tau;
            var point = new GroundPosition(from.X + (MathF.Sin(angle) * distance), from.Z + (MathF.Cos(angle) * distance));
            if (world.Regions.TryGetRegionIdAt(point.X, point.Z, out var region) && RegionPathfinder.IsWalkable(world.Regions.Cells[region]))
            {
                return point;
            }
        }

        return null;
    }

    /// <summary>Starts an activity: travelling when there is a path, performing at once when it is already there.</summary>
    private void Start(CreatureState creature, CreatureSituation situation, CreatureAction action, int? settlementId, GroundPosition target, List<GroundPosition> path)
    {
        var activity = creature.Activity;
        activity.Action = action;
        activity.Situation = situation;
        activity.TargetSettlementId = settlementId;
        activity.Target = target;
        activity.Path.Clear();
        activity.Path.AddRange(path);
        activity.Phase = path.Count > 0 ? CreaturePhase.Travelling : CreaturePhase.Performing;
        activity.TicksLeft = path.Count > 0 ? 0 : PerformTicks(creature, action);
    }

    /// <summary>Ticks an action takes at its target; a commanded wait lasts longer the more loyal the Creature is.</summary>
    /// <param name="creature">The Creature.</param>
    /// <param name="action">The action.</param>
    /// <returns>Ticks, at least 1.</returns>
    public int PerformTicks(CreatureState creature, CreatureAction action)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var seconds = action == CreatureAction.GoTo
            ? _rules.CommandStaySeconds * SimMath.Clamp01(creature.Loyalty)
            : CreatureModel.RuleFor(_rules, action).Seconds;
        return Math.Max(1, (int)MathF.Round(seconds * SimulationTime.TicksPerSecond));
    }

    /// <summary>A spot a little way from a settlement's centre, on the side the Creature comes from.</summary>
    private static GroundPosition StandBeside(GroundPosition center, GroundPosition from)
    {
        var dx = from.X - center.X;
        var dz = from.Z - center.Z;
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        return length < 1f
            ? new GroundPosition(center.X + StandOff, center.Z)
            : new GroundPosition(center.X + (dx / length * StandOff), center.Z + (dz / length * StandOff));
    }

    /// <summary>Distance between two ground positions in metres.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
