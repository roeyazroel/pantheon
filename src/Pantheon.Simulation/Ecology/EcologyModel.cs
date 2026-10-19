using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Ecology;

/// <summary>
/// The rules of the land's life as pure functions (spec §18, ADR-028): how much wildlife land can
/// hold, how harm heals and scars, and how healthy and varied the land is. <see cref="EcologySystem"/>
/// applies them; readouts and overlays use them too.
/// </summary>
public static class EcologyModel
{
    /// <summary>A gap this small is closed at once, so a settled land stops changing.</summary>
    public const float Settled = 0.001f;

    /// <summary>Wildlife the land can hold: its cover, unburned, unharmed and unpolluted.</summary>
    /// <param name="region">The region.</param>
    /// <returns>Capacity (0..1); none at sea.</returns>
    public static float WildlifeCapacity(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Biome == Biome.Sea ? 0f
            : SimMath.Clamp01(region.Vegetation) * (1f - SimMath.Clamp01(region.Scorch)) * Health(region);
    }

    /// <summary>How well the land's life fares: unharmed and unpolluted.</summary>
    /// <param name="region">The region.</param>
    /// <returns>Health (0..1).</returns>
    public static float Health(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return (1f - SimMath.Clamp01(region.Damage)) * (1f - SimMath.Clamp01(region.Pollution));
    }

    /// <summary>How varied the land's life is: its health, with its cover and its animals.</summary>
    /// <param name="region">The region.</param>
    /// <returns>Biodiversity (0..1); none at sea.</returns>
    public static float Biodiversity(RegionState region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return region.Biome == Biome.Sea ? 0f
            : Health(region) * ((0.5f * SimMath.Clamp01(region.Vegetation)) + (0.5f * SimMath.Clamp01(region.Wildlife)));
    }

    /// <summary>
    /// What a village hunts in some seconds from the animals of its region and the regions beside
    /// it: they lose them in proportion, and the village gains their food.
    /// </summary>
    /// <param name="rules">Ecology rules.</param>
    /// <param name="grid">Region grid.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="seconds">Simulated seconds.</param>
    /// <returns>Food gained.</returns>
    public static float Hunt(EcologyRules rules, RegionGrid grid, Domain.Settlements.SettlementState settlement, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(settlement);
        var wanted = rules.HuntPerHundredPerSecond * Math.Max(0, settlement.Population) / 100f * seconds;
        var available = 0f;
        foreach (var region in Around(grid, settlement.RegionId, 1))
        {
            available += region.Wildlife;
        }

        if (wanted <= 0f || available <= 0f)
        {
            return 0f;
        }

        var share = Math.Min(1f, wanted / available);
        foreach (var region in Around(grid, settlement.RegionId, 1))
        {
            region.Wildlife -= region.Wildlife * share;
        }

        return Math.Min(wanted, available) * rules.FoodPerWildlife;
    }

    /// <summary>The regions within a radius of a region (Chebyshev distance), clipped at the grid's edge.</summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="regionId">The centre.</param>
    /// <param name="radius">Radius in regions.</param>
    /// <returns>The regions.</returns>
    public static System.Collections.Generic.IEnumerable<RegionState> Around(RegionGrid grid, int regionId, int radius)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var (column, row) = grid.GetCoordinates(regionId);
        for (var r = Math.Max(0, row - radius); r <= Math.Min(grid.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(grid.Columns - 1, column + radius); c++)
            {
                yield return grid.Cells[(r * grid.Columns) + c];
            }
        }
    }

    /// <summary>
    /// Damage and scar after some seconds: damage above the scar threshold slowly becomes scar, and
    /// the rest heals towards the scar, faster where a god of nature holds the land; a god of nature
    /// also heals the scar.
    /// </summary>
    /// <param name="rules">Ecology rules.</param>
    /// <param name="damage">Damage now (0..1).</param>
    /// <param name="scar">Scar now (0..1), at most the damage.</param>
    /// <param name="seconds">Simulated seconds.</param>
    /// <param name="nature">Whether a god of nature holds the land.</param>
    /// <returns>The damage and scar after.</returns>
    public static (float Damage, float Scar) Heal(EcologyRules rules, float damage, float scar, float seconds, bool nature)
    {
        ArgumentNullException.ThrowIfNull(rules);
        damage = SimMath.Clamp01(damage);
        scar = Math.Min(SimMath.Clamp01(scar), damage);
        if (damage > rules.ScarThreshold)
        {
            scar = Math.Min(damage, scar + ((damage - rules.ScarThreshold) * rules.ScarPerSecond * seconds));
        }

        if (nature)
        {
            scar = Math.Max(0f, scar - (rules.NatureScarHealPerSecond * seconds));
        }

        var heal = rules.HealPerSecond * (nature ? rules.NatureHealFactor : 1f) * seconds;
        damage = Math.Max(scar, damage - ((damage - scar) * Math.Min(1f, heal)));
        return (damage - scar < Settled ? scar : damage, scar);
    }
}
