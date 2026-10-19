using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Creatures;

namespace Pantheon.Tests.Support;

/// <summary>Divine worlds with the god's Creature, for Creature tests.</summary>
internal static class CreatureWorlds
{
    /// <summary>The shipped Creature rules.</summary>
    public static CreatureRules Rules => TestContent.Shipped.Creatures;

    /// <summary>
    /// A <see cref="DivineWorlds"/> world with settlements at the given x positions, the god followed
    /// in the first, and its Creature born at the first settlement.
    /// </summary>
    /// <param name="positionsX">X positions of the settlements.</param>
    /// <returns>The world and its Creature.</returns>
    public static (WorldState World, CreatureState Creature) Create(params float[] positionsX)
    {
        var world = DivineWorlds.Create(100f, positionsX);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.6f, faith: 0.5f, love: 0.3f);
        var god = world.Gods[0];
        var withCreature = new GodState
        {
            Id = god.Id,
            Name = god.Name,
            Controller = god.Controller,
            Personality = god.Personality,
            DominantSinceTick = god.DominantSinceTick,
            DivinePower = god.DivinePower,
            Rank = god.Rank,
            CreatureId = god.Id,
            RefugeePolicy = god.RefugeePolicy,
            Cooldowns = god.Cooldowns,
            Domains = god.Domains,
        };
        world.Gods[0] = withCreature;
        var creature = CreatureFounder.Found(Rules, world.Seed, withCreature, "Testling", world.Settlements[0], world.Regions);
        world.Creatures.Add(creature);
        return (world, creature);
    }
}
