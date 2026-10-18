using System;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Disasters;

/// <summary>
/// The rules of wildfires and earthquakes as pure functions (spec §19–20, ADR-014).
/// <see cref="DisasterSystem"/> applies them once per disaster step; miracles, the god AI and the
/// readouts use them too.
/// </summary>
public static class DisasterModel
{
    /// <summary>Weight of a region's fire on a settlement, by its distance in regions (Chebyshev): its own, the next ring, the one after.</summary>
    private static readonly float[] FireThreatByRing = [1f, 0.6f, 0.3f];

    /// <summary>A new earthquake: the stronger, the wider and fiercer its shaking.</summary>
    /// <param name="rules">Disaster rules.</param>
    /// <param name="disasters">Disaster state, for the id.</param>
    /// <param name="epicentre">Where it strikes.</param>
    /// <param name="magnitude">Its magnitude.</param>
    /// <param name="tick">The tick it starts.</param>
    /// <param name="godId">The god whose miracle caused it, or <c>null</c>.</param>
    /// <returns>The earthquake, already added to the disasters under way.</returns>
    public static Earthquake StartQuake(DisasterRules rules, DisasterState disasters, GroundPosition epicentre, float magnitude, long tick, int? godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(disasters);
        var excess = Math.Max(0f, magnitude - rules.HarmlessMagnitude);
        var quake = new Earthquake
        {
            Id = disasters.NextEarthquakeId++,
            X = epicentre.X,
            Z = epicentre.Z,
            Magnitude = magnitude,
            Intensity = SimMath.Clamp01(excess / (rules.FullMagnitude - rules.HarmlessMagnitude)),
            Radius = Math.Max(1f, rules.RadiusPerMagnitude * excess),
            EndsAtTick = tick + Math.Max(1L, (long)MathF.Round(rules.ShakingSeconds * SimulationTime.TicksPerSecond)),
            CausedByGodId = godId,
        };
        disasters.Earthquakes.Add(quake);
        return quake;
    }

    /// <summary>How hard an earthquake shakes a point: its intensity, fading with the square of the distance to nothing at its radius.</summary>
    /// <param name="quake">The earthquake.</param>
    /// <param name="point">The point.</param>
    /// <returns>Shaking (0..1).</returns>
    public static float Shaking(Earthquake quake, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(quake);
        var dx = point.X - quake.X;
        var dz = point.Z - quake.Z;
        var reach = 1f - (MathF.Sqrt((dx * dx) + (dz * dz)) / quake.Radius);
        return reach <= 0f ? 0f : quake.Intensity * reach * reach;
    }

    /// <summary>The strongest shaking at a point now.</summary>
    /// <param name="disasters">Disasters under way.</param>
    /// <param name="point">The point.</param>
    /// <returns>Shaking (0..1).</returns>
    public static float ShakingAt(DisasterState disasters, GroundPosition point)
    {
        ArgumentNullException.ThrowIfNull(disasters);
        var shaking = 0f;
        foreach (var quake in disasters.Earthquakes)
        {
            shaking = Math.Max(shaking, Shaking(quake, point));
        }

        return shaking;
    }

    /// <summary>
    /// How readily a region burns: its unburnt vegetation, times the square of how dry its soil is,
    /// so damp land hardly burns and parched land burns readily. The sea and soil at or above
    /// <see cref="DisasterRules.WetSoil"/> do not burn.
    /// </summary>
    /// <param name="rules">Disaster rules.</param>
    /// <param name="region">The region.</param>
    /// <returns>Flammability (0..1).</returns>
    public static float Flammability(DisasterRules rules, RegionState region)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(region);
        if (region.Biome == Biome.Sea)
        {
            return 0f;
        }

        var dryness = 1f - Math.Min(1f, SimMath.Clamp01(region.SoilMoisture) / rules.WetSoil);
        return SimMath.Clamp01(region.Vegetation) * (1f - SimMath.Clamp01(region.Scorch)) * dryness * dryness;
    }

    /// <summary>Sets a region alight, when it can burn; a burning region only grows fiercer.</summary>
    /// <param name="rules">Disaster rules.</param>
    /// <param name="region">The region.</param>
    /// <param name="intensity">The fire's intensity (0..1).</param>
    /// <returns><c>true</c> when the region was not burning and now is.</returns>
    public static bool Ignite(DisasterRules rules, RegionState region, float intensity)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (Flammability(rules, region) < rules.MinFlammability)
        {
            return false;
        }

        var lit = region.Fire <= 0f;
        region.Fire = Math.Max(region.Fire, SimMath.Clamp01(intensity));
        return lit;
    }

    /// <summary>How close fire is to a settlement: the fiercest fire on its farmland, nearer rings counting more.</summary>
    /// <param name="rules">Settlement rules, for the farmland radius.</param>
    /// <param name="regions">Region grid.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Fire threat (0..1).</returns>
    public static float FireThreat(SettlementRules rules, RegionGrid regions, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(settlement);
        var (column, row) = regions.GetCoordinates(settlement.RegionId);
        var radius = Math.Min(rules.FarmlandRadius, FireThreatByRing.Length - 1);
        var threat = 0f;
        for (var r = Math.Max(0, row - radius); r <= Math.Min(regions.Rows - 1, row + radius); r++)
        {
            for (var c = Math.Max(0, column - radius); c <= Math.Min(regions.Columns - 1, column + radius); c++)
            {
                var ring = Math.Max(Math.Abs(r - row), Math.Abs(c - column));
                threat = Math.Max(threat, regions.Cells[(r * regions.Columns) + c].Fire * FireThreatByRing[ring]);
            }
        }

        return threat;
    }

    /// <summary>The danger a settlement is in: fire on its farmland or the ground shaking under it.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>Danger (0..1).</returns>
    public static float DangerTo(SettlementRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Math.Max(FireThreat(rules, world.Regions, settlement), ShakingAt(world.Disasters, settlement.Position));
    }

    /// <summary>Whether a Shield miracle protects a settlement now.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="tick">The current tick.</param>
    /// <returns><c>true</c> while shielded.</returns>
    public static bool IsShielded(SettlementState settlement, long tick)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.ShieldedUntilTick > tick;
    }
}
