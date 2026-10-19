using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Events;
using Pantheon.Domain.Migration;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Simulation.Navigation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Migration;

/// <summary>
/// One migration step (spec §31–32, §82, ADR-016). Hard lives build a settlement's wish to leave;
/// once enough people want to go, a group sets out for the settlement that draws them most and
/// walks there over land, eating what it carries. On arrival the god followed most there welcomes
/// them, makes them convert or turns them away, and the gods remember it. It raises
/// <see cref="MigrationStarted"/>, <see cref="MigrantsTurnedAway"/> and <see cref="MigrationEnded"/>.
/// </summary>
/// <remarks>
/// Deaths on the road draw from a stream derived from the world seed and the tick, so migration
/// never shifts the shared stream.
/// </remarks>
public sealed class MigrationSystem : ISimulationSystem
{
    /// <summary>Purpose name of migration's random streams.</summary>
    public const string RandomStream = "migration";

    private const float ArrivalTolerance = 1e-3f;
    private const int RouteAttempts = 3;

    private readonly MigrationRules _rules;
    private readonly SettlementRules _settlementRules;
    private readonly DivineRules _divine;
    private readonly IActRecorder? _ledger;

    /// <summary>Creates the system with validated rules.</summary>
    /// <param name="rules">Migration rules.</param>
    /// <param name="settlementRules">Settlement rules, for the farmland's hazards.</param>
    /// <param name="divine">Divine rules, for the settlements' conditions.</param>
    /// <param name="ledger">Where the gods' acts towards each other's migrants are recorded; <c>null</c> for none.</param>
    public MigrationSystem(MigrationRules rules, SettlementRules settlementRules, DivineRules divine, IActRecorder? ledger = null)
    {
        MigrationRulesValidator.Validate(rules);
        ArgumentNullException.ThrowIfNull(settlementRules);
        ArgumentNullException.ThrowIfNull(divine);
        _rules = rules;
        _settlementRules = settlementRules;
        _divine = divine;
        _ledger = ledger;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        var seconds = _rules.StepIntervalTicks * SimulationTime.SecondsPerTick;
        foreach (var settlement in world.Settlements)
        {
            BuildUrge(world, settlement, seconds, context.Events);
        }

        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, RandomStream, world.Tick));
        foreach (var group in world.Migration.Groups.ToArray())
        {
            Travel(world, group, random, seconds, context.Events);
        }
    }

    /// <summary>The pressure on a settlement's people now.</summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The pressure and its cause.</returns>
    public MigrationPressure PressureOn(WorldState world, SettlementState settlement) =>
        MigrationModel.Push(_rules, world, settlement, ConditionsOf(world, settlement));

    /// <summary>
    /// Sends a group of <paramref name="people"/> out of a settlement now, as if they had long
    /// wanted to leave (the debug tools' forced migration).
    /// </summary>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="people">How many leave (at most the settlement's people above <c>MinRemaining</c>).</param>
    /// <param name="events">Receives the departure.</param>
    /// <returns>The group, or <c>null</c> when nobody could leave or there is nowhere to go.</returns>
    public MigrationGroup? SendOut(WorldState world, SettlementState settlement, int people, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        var pressure = PressureOn(world, settlement);
        return Depart(world, settlement, Math.Min(people, settlement.Population - _rules.MinRemaining), pressure with { Value = 1f }, events);
    }

    /// <summary>The wish to leave builds under pressure and fades without it; enough of it sends a group out.</summary>
    private void BuildUrge(WorldState world, SettlementState settlement, float seconds, ICollection<WorldEvent> events)
    {
        var pressure = PressureOn(world, settlement);
        var cap = settlement.Population * _rules.MaxGroupShare;
        if (pressure.Value <= _rules.PressureThreshold || settlement.Population <= _rules.MinRemaining)
        {
            settlement.Emigration = Math.Max(0f, settlement.Emigration * (1f - (_rules.UrgeFadePerSecond * seconds)));
            return;
        }

        var excess = (pressure.Value - _rules.PressureThreshold) / (1f - _rules.PressureThreshold);
        settlement.Emigration = Math.Min(cap, settlement.Emigration + (settlement.Population * _rules.EmigrationPerSecond * excess * seconds));
        if (settlement.Emigration >= Math.Max(_rules.MinGroupSize, settlement.Population * _rules.GroupShare) && world.Migration.Groups.Count < _rules.MaxGroups)
        {
            var people = Math.Min((int)settlement.Emigration, settlement.Population - _rules.MinRemaining);
            if (Depart(world, settlement, people, pressure, events) is { } group)
            {
                settlement.Emigration = Math.Max(0f, settlement.Emigration - group.Population);
            }
        }
    }

    /// <summary>A group leaves for the best destination it can reach, taking its share of the stores.</summary>
    private MigrationGroup? Depart(WorldState world, SettlementState origin, int people, MigrationPressure pressure, ICollection<WorldEvent> events)
    {
        if (people < _rules.MinGroupSize || origin.Population <= 0)
        {
            return null;
        }

        var (beliefs, abandoned) = MigrationModel.MigrantBeliefs(_rules, origin.Beliefs);
        var fleeing = pressure.Cause is MigrationCause.Fear or MigrationCause.Doubt ? abandoned : null;
        if (Choose(world, origin.Position, beliefs, pressure.Value, [origin.Id], fleeing) is not { } route)
        {
            return null;
        }

        var food = Math.Min(Math.Max(0f, origin.Food) * people / origin.Population, people * _rules.FoodTakenPerPerson);
        origin.Food -= food;
        origin.Population -= people;
        var group = new MigrationGroup
        {
            Id = world.Migration.NextGroupId++,
            OriginSettlementId = origin.Id,
            DestinationSettlementId = route.Destination.Id,
            Cause = pressure.Cause,
            Population = people,
            Food = food,
            Health = SimMath.Clamp01(origin.Health),
            Position = origin.Position,
            Path = route.Path,
            Beliefs = beliefs,
            TurnedAwayFrom = [],
            DepartedTick = world.Tick,
        };
        world.Migration.Groups.Add(group);
        events.Add(new MigrationStarted
        {
            Tick = world.Tick,
            GroupId = group.Id,
            OriginSettlementId = origin.Id,
            DestinationSettlementId = group.DestinationSettlementId,
            People = people,
            Cause = pressure.Cause,
            GodId = MigrationModel.MainGod(beliefs),
            AbandonedGodId = abandoned,
        });
        return group;
    }

    /// <summary>
    /// The most appealing settlement within reach whose people are under less pressure (by
    /// <c>BetterBy</c>) than <paramref name="pressure"/> and that does not mainly follow the god the
    /// migrants flee, and the way there; <c>null</c> when none.
    /// </summary>
    private (SettlementState Destination, List<GroundPosition> Path)? Choose(WorldState world, GroundPosition from, List<GodBelief> beliefs, float pressure, IReadOnlyCollection<int> excluded, int? fleeing)
    {
        var candidates = new List<(SettlementState Settlement, float Appeal)>();
        foreach (var settlement in world.Settlements)
        {
            var distance = Distance(from, settlement.Position);
            if (excluded.Contains(settlement.Id) || distance > _rules.MaxDistance || (fleeing is { } god && MigrationModel.MainGod(settlement.Beliefs) == god))
            {
                continue;
            }

            var conditions = ConditionsOf(world, settlement);
            if (MigrationModel.Push(_rules, world, settlement, conditions).Value <= pressure - _rules.BetterBy)
            {
                candidates.Add((settlement, MigrationModel.Appeal(_rules, settlement, conditions, beliefs, distance)));
            }
        }

        candidates.Sort(static (a, b) => b.Appeal != a.Appeal ? b.Appeal.CompareTo(a.Appeal) : a.Settlement.Id.CompareTo(b.Settlement.Id));
        for (var i = 0; i < Math.Min(RouteAttempts, candidates.Count); i++)
        {
            if (RegionPathfinder.FindPath(world.Regions, from, candidates[i].Settlement.Position) is { } path)
            {
                return (candidates[i].Settlement, path);
            }
        }

        return null;
    }

    /// <summary>A step on the road: walk, eat, perhaps die, perhaps arrive.</summary>
    private void Travel(WorldState world, MigrationGroup group, SimulationRandom random, float seconds, ICollection<WorldEvent> events)
    {
        Walk(group, _rules.Speed * seconds);
        var need = group.Population * _rules.FoodPerPersonPerSecond * seconds;
        if (group.Food >= need)
        {
            group.Food -= need;
            group.Health = SimMath.Clamp01(group.Health + (_rules.HealthRecoveryPerSecond * seconds));
        }
        else
        {
            group.Food = 0f;
            group.Health = SimMath.Clamp01(group.Health - (_rules.HungerHealthLossPerSecond * seconds));
        }

        var deaths = Math.Min(group.Population, random.NextPoisson(group.Population * _rules.DeathsPerSecond * (1f - group.Health) * seconds));
        group.Population -= deaths;
        if (group.Population <= 0)
        {
            End(world, group, group.DestinationSettlementId, deaths, MigrationOutcome.Perished, null, events);
            return;
        }

        if (group.Path.Count == 0)
        {
            Arrive(world, group, events);
        }
    }

    /// <summary>The group reaches its destination: welcomed, converted or turned away by the god followed most there.</summary>
    private void Arrive(WorldState world, MigrationGroup group, ICollection<WorldEvent> events)
    {
        if (world.Settlements.Find(s => s.Id == group.DestinationSettlementId) is not { } host)
        {
            world.Migration.Groups.Remove(group);
            return;
        }

        var hostGod = MigrationModel.MainGod(host.Beliefs);
        var theirs = MigrationModel.MainGod(group.Beliefs);
        if (host.Id == group.OriginSettlementId)
        {
            Settle(world, group, host, hostGod, MigrationOutcome.Returned, events);
            return;
        }

        var policy = hostGod is { } godId && godId != theirs && GodStanding.Find(world, godId) is { } god ? god.RefugeePolicy : RefugeePolicy.Welcome;
        switch (policy)
        {
            case RefugeePolicy.TurnAway:
                TurnAway(world, group, host, hostGod!.Value, theirs, events);
                break;
            case RefugeePolicy.DemandConversion:
                MigrationModel.Convert(_rules, group.Beliefs, hostGod!.Value, BeliefModel.Find(host, hostGod.Value)?.Faith ?? 0f);
                Remember(world, theirs, hostGod.Value, DiplomaticAct.Converted, host.Id);
                Settle(world, group, host, hostGod, MigrationOutcome.Converted, events);
                break;
            case RefugeePolicy.Welcome:
                var arrived = group.Population;
                Settle(world, group, host, hostGod, MigrationOutcome.Welcomed, events);
                if (hostGod is { } welcoming && welcoming != theirs)
                {
                    MigrationModel.Welcome(_rules, host, welcoming, arrived);
                    Remember(world, theirs, welcoming, DiplomaticAct.Sheltered, host.Id);
                }

                break;
        }
    }

    /// <summary>The group is sent on: to the next best place that has not turned it away, or home.</summary>
    private void TurnAway(WorldState world, MigrationGroup group, SettlementState host, int byGodId, int? theirs, ICollection<WorldEvent> events)
    {
        group.TurnedAwayFrom.Add(host.Id);
        MigrationModel.Resent(_rules, group.Beliefs, byGodId);
        Remember(world, theirs, byGodId, DiplomaticAct.TurnedAway, host.Id);
        var excluded = new List<int>(group.TurnedAwayFrom) { group.OriginSettlementId };
        var origin = world.Settlements.Find(s => s.Id == group.OriginSettlementId);
        var next = Choose(world, group.Position, group.Beliefs, 1f, excluded, byGodId);
        if (next is null && origin is not null && RegionPathfinder.FindPath(world.Regions, group.Position, origin.Position) is { } home)
        {
            next = (origin, home);
        }

        events.Add(new MigrantsTurnedAway
        {
            Tick = world.Tick,
            GroupId = group.Id,
            SettlementId = host.Id,
            ByGodId = byGodId,
            People = group.Population,
            GodId = theirs,
            NextSettlementId = next?.Destination.Id ?? host.Id,
        });
        if (next is { } route)
        {
            group.DestinationSettlementId = route.Destination.Id;
            group.Path.Clear();
            group.Path.AddRange(route.Path);
        }
        else
        {
            // Nowhere left to go: they stay where they were refused after all.
            Settle(world, group, host, byGodId, MigrationOutcome.Welcomed, events);
        }
    }

    /// <summary>The group joins the settlement and leaves the road.</summary>
    private static void Settle(WorldState world, MigrationGroup group, SettlementState host, int? hostGod, MigrationOutcome outcome, ICollection<WorldEvent> events)
    {
        var people = group.Population;
        var theirs = MigrationModel.MainGod(group.Beliefs);
        MigrationModel.Merge(host, group);
        End(world, group, host.Id, people, outcome, hostGod, events, theirs);
    }

    /// <summary>Removes the group and reports how its journey ended.</summary>
    private static void End(WorldState world, MigrationGroup group, int settlementId, int people, MigrationOutcome outcome, int? hostGod, ICollection<WorldEvent> events, int? godId = null)
    {
        world.Migration.Groups.Remove(group);
        events.Add(new MigrationEnded
        {
            Tick = world.Tick,
            GroupId = group.Id,
            OriginSettlementId = group.OriginSettlementId,
            SettlementId = settlementId,
            People = people,
            Outcome = outcome,
            GodId = godId ?? MigrationModel.MainGod(group.Beliefs),
            HostGodId = hostGod,
        });
    }

    /// <summary>The migrants' god remembers what the host god did with its people.</summary>
    private void Remember(WorldState world, int? theirs, int hostGod, DiplomaticAct act, int settlementId)
    {
        if (theirs is { } observer && observer != hostGod)
        {
            _ledger?.Record(world, observer, hostGod, act, settlementId);
        }
    }

    /// <summary>A settlement's conditions now, hazards included.</summary>
    private SettlementConditions ConditionsOf(WorldState world, SettlementState settlement) =>
        BeliefModel.AssessConditions(_divine, settlement, Hazards.Of(_settlementRules, world, settlement));

    /// <summary>Moves the group along its path, carrying leftover distance to the next waypoint.</summary>
    private static void Walk(MigrationGroup group, float step)
    {
        while (step > 0f && group.Path.Count > 0)
        {
            var next = group.Path[0];
            var distance = Distance(group.Position, next);
            if (distance <= step + ArrivalTolerance)
            {
                group.Position = next;
                group.Path.RemoveAt(0);
                step -= distance;
            }
            else
            {
                group.Position = new GroundPosition(
                    group.Position.X + ((next.X - group.Position.X) / distance * step),
                    group.Position.Z + ((next.Z - group.Position.Z) / distance * step));
                step = 0f;
            }
        }
    }

    /// <summary>Distance between two ground points.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var (dx, dz) = (a.X - b.X, a.Z - b.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
