using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Settlements;

namespace Pantheon.Simulation.Belief;

/// <summary>What the land threatens a settlement with, beyond its stores and health (ADR-013, ADR-014).</summary>
/// <param name="Drought">How dry its farmland is (0..1).</param>
/// <param name="Danger">Fire on its farmland or the ground shaking under it (0..1).</param>
public readonly record struct Hazards(float Drought, float Danger)
{
    /// <summary>No drought and no danger.</summary>
    public static Hazards None { get; } = new(0f, 0f);

    /// <summary>The hazards a settlement faces now.</summary>
    /// <param name="rules">Settlement rules, for the farmland.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Its drought and danger.</returns>
    public static Hazards Of(SettlementRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new Hazards(SettlementEconomy.DroughtOf(rules, world.Regions, settlement), DisasterModel.DangerTo(rules, world, settlement));
    }
}
