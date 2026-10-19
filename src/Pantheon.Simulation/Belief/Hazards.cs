using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Belief;

/// <summary>What the land threatens a settlement with, beyond its stores and health (ADR-013, ADR-014).</summary>
/// <param name="Drought">How dry its farmland is (0..1).</param>
/// <param name="Danger">Fire on its farmland, the ground shaking under it or a flood over it (0..1).</param>
/// <param name="Thirst">How low its wells run (0..1, ADR-022): 0 while they are at least half full, 1 when dry.</param>
public readonly record struct Hazards(float Drought, float Danger, float Thirst = 0f)
{
    /// <summary>No drought, no danger and full wells.</summary>
    public static Hazards None { get; } = new(0f, 0f);

    /// <summary>The hazards a settlement faces now.</summary>
    /// <param name="rules">Settlement rules, for the farmland.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Its drought and danger.</returns>
    public static Hazards Of(SettlementRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new Hazards(SettlementEconomy.DroughtOf(rules, world.Regions, settlement), DisasterModel.DangerTo(rules, world, settlement), ThirstOf(rules, settlement));
    }

    /// <summary>How low a settlement's wells run: 0 while they are at least half full or nobody drinks, 1 when they are dry.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Thirst (0..1).</returns>
    public static float ThirstOf(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var half = 0.5f * SettlementEconomy.ComputeWaterStorage(rules, settlement.Housing);
        return rules.WaterPerPersonPerStep <= 0f || half <= 0f || settlement.Population <= 0 ? 0f : 1f - SimMath.Clamp01(settlement.Water / half);
    }
}
