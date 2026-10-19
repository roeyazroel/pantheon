using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Migration;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Roads;
using Pantheon.Simulation.Systems;

namespace Pantheon.Simulation.Villagers;

/// <summary>
/// One step of the villagers the gods follow (spec §80's selected villager, ADR-043). Each eats from
/// their village's stores, works, rests and prays as their needs and faith move them; flees danger
/// to a safe village and comes home after it; seeks a new home when theirs is lost; turns to the god
/// most of their village follows when their own faith fails; and dies of age, hunger, illness or
/// danger. They witness what happens around them (<see cref="Witness"/>), and what they remember
/// moves them: hardship drives them to pray, and danger lived through makes them flee it sooner.
/// </summary>
/// <remarks>Like the notables, it draws from a stream derived from the world seed and the tick.</remarks>
public sealed class VillagerSystem : ISimulationSystem
{
    private readonly SettlementRules _settlements;
    private readonly VillagerRules _rules;
    private readonly int _intervalTicks;

    /// <summary>Creates the system.</summary>
    /// <param name="settlements">Settlement rules, with the villager rules.</param>
    /// <param name="intervalTicks">Ticks between its steps.</param>
    public VillagerSystem(SettlementRules settlements, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(settlements);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _settlements = settlements;
        _rules = settlements.Villagers;
        _intervalTicks = intervalTicks;
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var world = context.World;
        if (world.Villagers.Count == 0)
        {
            return;
        }

        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, VillagerModel.RandomStream, world.Tick));
        var seconds = _intervalTicks * SimulationTime.SecondsPerTick;
        foreach (var villager in world.Villagers.ToArray())
        {
            Live(world, villager, random, seconds, context.Events);
        }
    }

    /// <summary>The followed villagers witness events: miracles near them, and what befalls their village.</summary>
    /// <param name="world">World state.</param>
    /// <param name="events">The events.</param>
    /// <param name="from">The first event to witness.</param>
    public void Witness(WorldState world, IReadOnlyList<WorldEvent> events, int from) => VillagerWitness.Observe(_rules, world, events, from);

    /// <summary>One step of a villager's life.</summary>
    private void Live(WorldState world, VillagerState villager, SimulationRandom random, float seconds, List<WorldEvent> events)
    {
        var home = world.Settlements.Find(s => s.Id == villager.SettlementId);
        var (danger, cause) = VillagerModel.DangerTo(_settlements, world, villager, home);
        var harm = Needs(world, villager, home, seconds);
        var fate = VillagerModel.Age(_rules, villager, world.Tick) >= villager.LifeYears ? VillagerFate.OldAge
            : danger > 0f && random.NextDouble() < _rules.Mortality * danger * seconds ? cause
            : villager.Health <= 0f ? harm
            : (VillagerFate?)null;
        if (fate is { } end)
        {
            world.Villagers.Remove(villager);
            events.Add(new VillagerDied
            {
                Tick = world.Tick, VillagerId = villager.Id, Name = villager.Name, GodId = villager.FollowerGodId, SettlementId = villager.SettlementId,
                Fate = end, Age = (int)VillagerModel.Age(_rules, villager, world.Tick),
            });
            return;
        }

        if (villager.Activity is not (VillagerActivity.Fleeing or VillagerActivity.Sheltering) && danger >= VillagerModel.FleeAt(_rules, villager))
        {
            Flee(world, villager, home, cause, events);
        }
        else if (home is not { Population: > 0 } && villager.Activity is not (VillagerActivity.Fleeing or VillagerActivity.Sheltering or VillagerActivity.Seeking))
        {
            Seek(world, villager);
        }

        var pace = villager.Activity == VillagerActivity.Fleeing ? 1.6f : 1f;
        VillagerModel.Walk(villager, _rules.Speed * pace * RoadModel.SpeedAt(_settlements.Roads, world, villager.Position) * seconds);
        Act(world, villager, home);
        Believe(world, villager, home, seconds, events);
    }

    /// <summary>
    /// Hunger, weariness and health: they eat from the stores while there is food (a refuge's when
    /// sheltering there, once hungry), starve without it and sicken with the plague.
    /// </summary>
    /// <returns>What harms their health most, should it fail.</returns>
    private VillagerFate Needs(WorldState world, VillagerState villager, SettlementState? home, float seconds)
    {
        var there = villager.Path.Count == 0;
        var fed = home is { Food: >= 1f };
        var sheltered = villager.Activity == VillagerActivity.Sheltering && villager.Hunger > 0.3f && world.Settlements.Find(s => s.Id == villager.RefugeId) is { Food: >= 1f };
        villager.Hunger = SimMath.Clamp01(villager.Hunger + (_rules.HungerPerSecond * seconds)
            - (there && ((fed && villager.Activity == VillagerActivity.Eating) || sheltered) ? _rules.EatPerSecond * seconds : 0f));
        villager.Fatigue = SimMath.Clamp01(villager.Fatigue + (!there ? 0.5f * _rules.FatiguePerSecond * seconds
            : villager.Activity == VillagerActivity.Working ? _rules.FatiguePerSecond * seconds
            : villager.Activity == VillagerActivity.Resting ? -_rules.RestPerSecond * seconds
            : 0f));
        var starving = villager.Hunger >= 1f;
        var sick = home is { Plague: > 0f } && VillagerModel.InVillage(_rules, villager, home);
        if (starving || (!fed && villager.Activity == VillagerActivity.Eating && there))
        {
            Hardship(world, villager, VillagerDeed.Hungered, "famine");
        }

        if (sick)
        {
            Hardship(world, villager, VillagerDeed.FellIll, "plague");
        }

        var loss = (starving ? _rules.StarvePerSecond : 0f) + (sick ? _rules.PlagueHarmPerSecond * home!.Plague : 0f);
        villager.Health = SimMath.Clamp01(villager.Health + ((loss > 0f ? -loss : _rules.HealPerSecond) * seconds));
        return sick && !starving ? VillagerFate.Illness : VillagerFate.Hunger;
    }

    /// <summary>Remembers a hardship once while it lasts.</summary>
    private void Hardship(WorldState world, VillagerState villager, VillagerDeed deed, string detail) =>
        VillagerModel.RememberOnce(_rules, villager, new VillagerMemory { Tick = world.Tick, Deed = deed, SettlementId = villager.SettlementId, Detail = detail }, _rules.HardshipSeconds);

    /// <summary>Runs from danger to the nearest safe village within reach, or out of the village when none is safe.</summary>
    private void Flee(WorldState world, VillagerState villager, SettlementState? home, VillagerFate cause, List<WorldEvent> events)
    {
        var fleeAt = VillagerModel.FleeAt(_rules, villager);
        SettlementState? refuge = null;
        var best = _rules.FleeRange;
        foreach (var settlement in world.Settlements)
        {
            var distance = VillagerModel.Distance(villager.Position, settlement.Position);
            if (settlement.Id != villager.SettlementId && settlement.Population > 0 && distance <= best
                && Disasters.DisasterModel.DangerTo(_settlements, world, settlement) < fleeAt)
            {
                (refuge, best) = (settlement, distance);
            }
        }

        var centre = home?.Position ?? villager.Position;
        var away = refuge?.Position ?? Away(villager, centre);
        villager.Activity = VillagerActivity.Fleeing;
        villager.RefugeId = refuge?.Id;
        if (!VillagerModel.SetOut(world, villager, away, _settlements.Roads, villager.SettlementId, refuge?.Id))
        {
            villager.Activity = VillagerActivity.Sheltering;
        }

        VillagerModel.Remember(_rules, villager, new VillagerMemory { Tick = world.Tick, Deed = VillagerDeed.Fled, SettlementId = home?.Id, OtherId = refuge?.Id, Detail = cause.ToString() });
        events.Add(new VillagerFled { Tick = world.Tick, VillagerId = villager.Id, Name = villager.Name, GodId = villager.FollowerGodId, SettlementId = villager.SettlementId, RefugeId = refuge?.Id, Cause = cause });
    }

    /// <summary>A point out of the village, away from its centre past where the villager stands.</summary>
    private GroundPosition Away(VillagerState villager, GroundPosition centre)
    {
        var (dx, dz) = (villager.Position.X - centre.X, villager.Position.Z - centre.Z);
        var length = MathF.Sqrt((dx * dx) + (dz * dz));
        (dx, dz) = length > 1f ? (dx / length, dz / length) : (MathF.Cos(villager.Id), MathF.Sin(villager.Id));
        var reach = _rules.VillageRadius + _rules.FleeDistance;
        return new GroundPosition(centre.X + (dx * reach), centre.Z + (dz * reach));
    }

    /// <summary>Their village lost, they set out for the nearest village of their god, else the nearest at all.</summary>
    private void Seek(WorldState world, VillagerState villager)
    {
        SettlementState? best = null;
        var bestScore = float.MaxValue;
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0 || settlement.Id == villager.SettlementId)
            {
                continue;
            }

            var theirs = villager.GodId is { } god && MigrationModel.MainGod(settlement.Beliefs) == god;
            var score = VillagerModel.Distance(villager.Position, settlement.Position) * (theirs ? 0.5f : 1f);
            if (score < bestScore)
            {
                (best, bestScore) = (settlement, score);
            }
        }

        villager.Activity = VillagerActivity.Seeking;
        villager.RefugeId = best?.Id;
        if (best is null || !VillagerModel.SetOut(world, villager, best.Position, _settlements.Roads, villager.SettlementId, best.Id))
        {
            villager.Path.Clear();
        }
    }

    /// <summary>Carries on with what they are about: shelters until home is safe, settles in a new home, or chooses what to do next.</summary>
    private void Act(WorldState world, VillagerState villager, SettlementState? home)
    {
        switch (villager.Activity)
        {
            case VillagerActivity.Fleeing:
                if (villager.Path.Count == 0)
                {
                    villager.Activity = VillagerActivity.Sheltering;
                }

                return;
            case VillagerActivity.Sheltering:
                if (home is { Population: > 0 } && Disasters.DisasterModel.DangerTo(_settlements, world, home) < 0.5f * VillagerModel.FleeAt(_rules, villager))
                {
                    villager.RefugeId = null;
                    Begin(world, villager, VillagerActivity.Resting, across: true);
                    VillagerModel.Remember(_rules, villager, new VillagerMemory { Tick = world.Tick, Deed = VillagerDeed.Returned, SettlementId = home.Id });
                }
                else if (home is not { Population: > 0 })
                {
                    Seek(world, villager);
                }

                return;
            case VillagerActivity.Seeking:
                if (villager.Path.Count == 0 && world.Settlements.Find(s => s.Id == villager.RefugeId) is { Population: > 0 } refuge)
                {
                    Settle(world, villager, refuge);
                }
                else if (villager.Path.Count == 0 && world.Tick % VillagerModel.Ticks(_rules.RestSeconds) < _intervalTicks)
                {
                    Seek(world, villager);
                }

                return;
            case VillagerActivity.Resting or VillagerActivity.Eating or VillagerActivity.Working or VillagerActivity.Worshipping:
                if (villager.Path.Count > 0)
                {
                    return;
                }

                if (villager.Activity == VillagerActivity.Worshipping)
                {
                    villager.LastWorshipTick = world.Tick;
                }

                villager.TicksLeft -= _intervalTicks;
                var starving = villager.Hunger >= 0.95f && villager.Activity != VillagerActivity.Eating && home is { Food: >= 1f };
                if (villager.TicksLeft <= 0 || starving)
                {
                    Begin(world, villager, VillagerMind.Options(_rules, world, villager)[0].Activity, across: false);
                }

                return;
        }
    }

    /// <summary>Starts an activity and walks to where it is done: within the village, or home from elsewhere.</summary>
    private void Begin(WorldState world, VillagerState villager, VillagerActivity activity, bool across)
    {
        villager.Activity = activity;
        villager.TicksLeft = VillagerModel.Ticks(VillagerMind.Seconds(_rules, activity));
        var place = VillagerMind.Place(villager, activity);
        villager.Path.Clear();
        if (across)
        {
            VillagerModel.SetOut(world, villager, place, _settlements.Roads, null, null);
        }
        else if (VillagerModel.Distance(villager.Position, place) > 0.5f)
        {
            villager.Path.Add(place);
        }
    }

    /// <summary>Makes a new home in the village they reached.</summary>
    private void Settle(WorldState world, VillagerState villager, SettlementState village)
    {
        villager.SettlementId = village.Id;
        villager.Slot = null;
        villager.RefugeId = null;
        (villager.Home, villager.WorkPlace, villager.Shrine) = VillagerModel.Spots(world.Seed, villager, village);
        Begin(world, villager, VillagerMind.Options(_rules, world, villager)[0].Activity, across: false);
        VillagerModel.Remember(_rules, villager, new VillagerMemory { Tick = world.Tick, Deed = VillagerDeed.Moved, SettlementId = village.Id });
    }

    /// <summary>
    /// Their faith follows their village's in their god, and grows in prayer; when it fails, or they
    /// have none, they turn to the god most of their village follows.
    /// </summary>
    private void Believe(WorldState world, VillagerState villager, SettlementState? home, float seconds, List<WorldEvent> events)
    {
        if (home is not { Population: > 0 })
        {
            return;
        }

        if (villager.GodId is { } god)
        {
            var village = BeliefModel.Find(home, god)?.Faith ?? 0f;
            villager.Faith += (village - villager.Faith) * Math.Min(1f, _rules.FaithDrift * seconds);
            if (villager.Activity == VillagerActivity.Worshipping && villager.Path.Count == 0)
            {
                villager.Faith += _rules.WorshipFaith * seconds;
            }

            villager.Faith = SimMath.Clamp01(villager.Faith);
        }

        if (MigrationModel.MainGod(home.Beliefs) is not { } main || main == villager.GodId || BeliefModel.Find(home, main) is not { Adherence: >= 0.5f } belief
            || (villager.GodId is not null && villager.Faith >= _rules.ConvertFaith))
        {
            return;
        }

        var from = villager.GodId;
        (villager.GodId, villager.Faith) = (main, belief.Faith);
        VillagerModel.Remember(_rules, villager, new VillagerMemory { Tick = world.Tick, Deed = VillagerDeed.Turned, SettlementId = home.Id, GodId = main, OtherId = from });
        events.Add(new VillagerTurned { Tick = world.Tick, VillagerId = villager.Id, Name = villager.Name, GodId = villager.FollowerGodId, FromGodId = from, ToGodId = main });
    }
}
