using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Ecology;
using Pantheon.Simulation.Migration;

namespace Pantheon.Simulation.Creatures;

/// <summary>
/// What a Creature's paths add to its deeds (spec §24, ADR-030): a Healer heals the people it
/// helps, a Missionary wins the godless, a Trickster sows doubt in rival gods, a Nature Keeper heals
/// the land, a Destroyer kills more, and a Guardian and a Commander shield their god's villages.
/// </summary>
public static class CallingEffects
{
    /// <summary>The paths' effects after a finished deed.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="creature">The Creature.</param>
    /// <param name="settlement">The settlement it acted on, if any.</param>
    /// <param name="action">The deed.</param>
    public static void AfterDeed(CallingRules rules, WorldState world, CreatureState creature, SettlementState? settlement, CreatureAction action)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(creature);
        if (CreatureCalling.Has(creature, CreatureSpecialisation.NatureKeeper) && world.Regions.TryGetRegionIdAt(creature.Position.X, creature.Position.Z, out var regionId))
        {
            foreach (var region in EcologyModel.Around(world.Regions, regionId, 1))
            {
                region.Damage = Math.Max(region.Scar, region.Damage - rules.KeeperHealing);
                region.Blight = Math.Max(0f, region.Blight - rules.KeeperHealing);
            }
        }

        if (settlement is null || settlement.Population <= 0 || action is CreatureAction.Attack or CreatureAction.EatFromStores)
        {
            return;
        }

        if (CreatureCalling.Has(creature, CreatureSpecialisation.Healer))
        {
            settlement.Health = SimMath.Clamp01(settlement.Health + rules.HealerHealth);
        }

        if (CreatureCalling.Has(creature, CreatureSpecialisation.Missionary) && BeliefModel.Find(settlement, creature.GodId) is { } belief)
        {
            belief.Adherence = SimMath.Clamp01(belief.Adherence + (rules.MissionaryConversion * Math.Max(0f, 1f - BeliefModel.TotalAdherence(settlement.Beliefs))));
        }

        if (CreatureCalling.Has(creature, CreatureSpecialisation.Trickster) && MigrationModel.MainGod(settlement.Beliefs) is { } rival && rival != creature.GodId && BeliefModel.Find(settlement, rival) is { } theirs)
        {
            theirs.Doubt = BeliefModel.ApplyChange(theirs.Doubt, rules.TricksterDoubt);
        }
    }

    /// <summary>A Destroyer's attack kills more.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="creature">The attacker.</param>
    /// <returns>The factor on the people its attack kills.</returns>
    public static float AttackFactor(CallingRules rules, CreatureState creature) =>
        CreatureCalling.Has(creature, CreatureSpecialisation.Destroyer) ? 1f + rules.DestroyerAttack : 1f;

    /// <summary>A Warrior fights harder.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="creature">The fighter.</param>
    /// <returns>The factor on its combat power.</returns>
    public static float PowerFactor(CallingRules rules, CreatureState creature) =>
        CreatureCalling.Has(creature, CreatureSpecialisation.Warrior) ? 1f + rules.WarriorPower : 1f;

    /// <summary>Defence a Commander of the village's main god, standing near it, gives it against a Creature's attack.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>Extra defence (0..1).</returns>
    public static float CommandedDefence(CallingRules rules, WorldState world, SettlementState settlement) =>
        Guarded(rules, world, settlement, CreatureSpecialisation.Commander) ? rules.CommanderDefence : 0f;

    /// <summary>Share of a disaster's harm a Guardian of the village's main god, standing near it, spares it.</summary>
    /// <param name="rules">Calling rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The factor on the harm (0..1).</returns>
    public static float Shield(CallingRules rules, WorldState world, SettlementState settlement) =>
        Guarded(rules, world, settlement, CreatureSpecialisation.Guardian) ? 1f - rules.GuardianShield : 1f;

    /// <summary>Whether the village's main god's Creature, walking the path, stands within reach of it.</summary>
    private static bool Guarded(CallingRules rules, WorldState world, SettlementState settlement, CreatureSpecialisation path)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        return MigrationModel.MainGod(settlement.Beliefs) is { } god && CreatureCalling.Near(world, god, path, settlement.Position, rules.GuardRadius) is not null;
    }
}
