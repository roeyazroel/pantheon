using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Villagers;

/// <summary>
/// Which villager each god follows (spec §80's selected villager, ADR-043). Following one of a
/// village's people makes them an individual of the world, simulated in full: the same person each
/// time for the same figure, named and aged from the seed, believing as their village does. A god
/// follows one villager at a time; following another, or letting them go, returns the first to the
/// village's crowd.
/// </summary>
public sealed class VillagerAuthority
{
    private readonly VillagerRules _rules;

    /// <summary>Creates the authority.</summary>
    /// <param name="rules">Villager rules.</param>
    public VillagerAuthority(VillagerRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    /// <summary>Whether a god may follow a villager.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The villager to follow.</param>
    /// <returns>Why not, or <see cref="VillagerRejection.None"/>.</returns>
    public VillagerRejection Check(WorldState world, FollowVillager command)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (!_rules.Enabled)
        {
            return VillagerRejection.NoVillagers;
        }

        if (!world.Gods.Exists(g => g.Id == command.GodId))
        {
            return VillagerRejection.UnknownGod;
        }

        if (world.Settlements.Find(s => s.Id == command.SettlementId) is not { Population: > 0 } village || command.Slot < 0)
        {
            return VillagerRejection.UnknownSettlement;
        }

        foreach (var spot in new[] { command.Home, command.WorkPlace, command.Shrine, command.Position })
        {
            if (!float.IsFinite(spot.X) || !float.IsFinite(spot.Z) || VillagerModel.Distance(spot, village.Position) > _rules.SpotRange)
            {
                return VillagerRejection.TooFar;
            }
        }

        return VillagerRejection.None;
    }

    /// <summary>A god follows a villager, letting go of any it followed before.</summary>
    /// <param name="world">World state.</param>
    /// <param name="command">The villager to follow.</param>
    /// <param name="events">Receives <see cref="VillagerFollowed"/>.</param>
    /// <returns>Why not, or <see cref="VillagerRejection.None"/>.</returns>
    public VillagerRejection Follow(WorldState world, FollowVillager command, ICollection<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var rejection = Check(world, command);
        if (rejection != VillagerRejection.None)
        {
            return rejection;
        }

        var village = world.Settlements.Find(s => s.Id == command.SettlementId)!;
        var random = new SimulationRandom(SeedMixer.Derive(world.Seed, VillagerModel.RandomStream, -(((long)command.SettlementId << 20) + command.Slot + 1)));
        var name = _rules.Names[random.NextInt(_rules.Names.Count)];
        var age = _rules.MinAge + ((_rules.MaxAge - _rules.MinAge) * random.NextFloat());
        var life = _rules.MinLifeYears + ((_rules.MaxLifeYears - _rules.MinLifeYears) * random.NextFloat());
        var belief = Believer(village.Beliefs, random.NextFloat());
        var villager = new VillagerState
        {
            Id = world.NextVillagerId++,
            Name = name,
            FollowerGodId = command.GodId,
            SettlementId = village.Id,
            Slot = command.Slot,
            Work = command.Work,
            GodId = belief?.GodId,
            Faith = SimMath.Clamp01((belief?.Faith ?? 0f) + (0.2f * (random.NextFloat() - 0.5f))),
            Home = command.Home,
            WorkPlace = command.WorkPlace,
            Shrine = command.Shrine,
            Position = command.Position,
            Hunger = 0.1f + (0.3f * random.NextFloat()),
            Fatigue = 0.1f + (0.3f * random.NextFloat()),
            Health = Math.Clamp(village.Health, 0.3f, 1f),
            AgeYears = age,
            LifeYears = Math.Max(life, age + 3f + (5f * random.NextFloat())),
            FollowedTick = world.Tick,
        };
        world.Villagers.RemoveAll(v => v.FollowerGodId == command.GodId);
        world.Villagers.Add(villager);
        world.Villagers.Sort((a, b) => a.Id.CompareTo(b.Id));
        VillagerModel.Remember(_rules, villager, new VillagerMemory { Tick = world.Tick, Deed = VillagerDeed.Followed, SettlementId = village.Id, GodId = command.GodId });
        events.Add(new VillagerFollowed { Tick = world.Tick, VillagerId = villager.Id, Name = villager.Name, GodId = command.GodId, SettlementId = village.Id });
        return VillagerRejection.None;
    }

    /// <summary>A god lets its villager go back to the village's crowd.</summary>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>Why not, or <see cref="VillagerRejection.None"/>.</returns>
    public VillagerRejection Release(WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Villagers.RemoveAll(v => v.FollowerGodId == godId) > 0 ? VillagerRejection.None : VillagerRejection.NotFollowing;
    }

    /// <summary>The god a villager follows, drawn by their village's adherence; <c>null</c> for one of the godless.</summary>
    private static Domain.Belief.GodBelief? Believer(IReadOnlyList<Domain.Belief.GodBelief> beliefs, float roll)
    {
        var sum = 0f;
        foreach (var belief in beliefs)
        {
            sum += Math.Max(0f, belief.Adherence);
            if (roll < sum)
            {
                return belief;
            }
        }

        return null;
    }
}
