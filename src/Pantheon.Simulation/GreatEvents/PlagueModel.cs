using System;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>
/// The plague as pure functions and state changes (spec §52, ADR-034): how likely a village is to
/// break out, how the sickness grows and ends within it, how much a hospital and medicine shield it,
/// and what a healing miracle cures.
/// </summary>
public static class PlagueModel
{
    /// <summary>The sickness a village starts with when the plague reaches it.</summary>
    public const float Onset = 0.02f;

    /// <summary>Sickness below which a village is well again.</summary>
    public const float Gone = 0.002f;

    /// <summary>How ripe a village is for an outbreak (0..1): crowded beyond the rules' crowding, its health poor, its land polluted, its people not immune.</summary>
    /// <param name="rules">Plague rules.</param>
    /// <param name="world">World state, for the land's pollution.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The ripeness.</returns>
    public static float Ripeness(PlagueRules rules, WorldState world, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settlement);
        if (settlement.Population <= 0 || settlement.Plague > 0f)
        {
            return 0f;
        }

        var crowding = (float)settlement.Population / Math.Max(1, settlement.Housing);
        var crowded = SimMath.Clamp01((crowding - rules.Crowding) / 0.3f);
        var sick = 0.3f + (0.7f * (1f - SimMath.Clamp01(settlement.Health)));
        var pollution = 1f + SimMath.Clamp01(world.Regions.Cells[settlement.RegionId].Pollution);
        return SimMath.Clamp01(crowded * sick * pollution * (1f - SimMath.Clamp01(settlement.Immunity)));
    }

    /// <summary>The share of the spread a village's hospital and medicine stop.</summary>
    /// <param name="rules">Plague rules.</param>
    /// <param name="settlement">The village.</param>
    /// <returns>The shield (0..1).</returns>
    public static float Shield(PlagueRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var hospital = settlement.Society.Buildings.Exists(b => b.Kind == BuildingKind.Hospital) ? rules.HospitalShield : 0f;
        return SimMath.Clamp01(hospital + (rules.MedicinePerLevel * settlement.Society.Technology.Medicine));
    }

    /// <summary>
    /// The sickness's course within a village for some seconds: the sick infect the healthy who are
    /// not immune, the sick recover immune, immunity fades, and the sick lose health.
    /// </summary>
    /// <param name="rules">Plague rules.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="seconds">Seconds passed.</param>
    /// <returns>Whether the village was sick and is now well.</returns>
    public static bool Advance(PlagueRules rules, SettlementState settlement, float seconds)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var immunity = SimMath.Clamp01(settlement.Immunity);
        settlement.Immunity = immunity - (immunity * Math.Min(1f, rules.ImmunityLossPerSecond * seconds));
        var sick = SimMath.Clamp01(settlement.Plague);
        if (sick <= 0f)
        {
            return false;
        }

        var infected = rules.Transmission * (1f - Shield(rules, settlement)) * sick * Math.Max(0f, 1f - sick - settlement.Immunity) * seconds;
        var recovered = Math.Min(sick, rules.RecoveryPerSecond * sick * seconds);
        settlement.Immunity = SimMath.Clamp01(settlement.Immunity + recovered);
        settlement.Health = SimMath.Clamp01(settlement.Health - (rules.HealthLossPerSecond * sick * seconds));
        settlement.Plague = SimMath.Clamp01(sick + infected - recovered);
        if (settlement.Plague >= Gone)
        {
            return false;
        }

        settlement.Plague = 0f;
        return true;
    }

    /// <summary>The chance per second that a village catches the plague from a sick one, by contact or along a road.</summary>
    /// <param name="rules">Plague rules.</param>
    /// <param name="sick">The sick village.</param>
    /// <param name="healthy">The village that may catch it.</param>
    /// <param name="byRoad">Whether an open road joins them.</param>
    /// <returns>The chance per second.</returns>
    public static float CatchPerSecond(PlagueRules rules, SettlementState sick, SettlementState healthy, bool byRoad)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(sick);
        ArgumentNullException.ThrowIfNull(healthy);
        if (healthy.Population <= 0 || healthy.Plague > 0f)
        {
            return 0f;
        }

        var near = byRoad ? rules.RoadPerSecond : Distance(sick.Position, healthy.Position) <= rules.ContactRange ? rules.ContactPerSecond : 0f;
        return near * SimMath.Clamp01(sick.Plague) * (1f - Shield(rules, healthy)) * (1f - SimMath.Clamp01(healthy.Immunity));
    }

    /// <summary>A healing miracle's cure: it takes a share of the sickness by its healing.</summary>
    /// <param name="rules">Plague rules.</param>
    /// <param name="settlement">The village.</param>
    /// <param name="healing">The miracle's healing.</param>
    public static void Cure(PlagueRules rules, SettlementState settlement, float healing)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(settlement);
        var sick = settlement.Plague * (1f - SimMath.Clamp01(rules.HealCure * healing));
        settlement.Plague = sick < Gone ? 0f : sick;
    }

    /// <summary>The distance between two points.</summary>
    private static float Distance(GroundPosition a, GroundPosition b)
    {
        var (dx, dz) = (a.X - b.X, a.Z - b.Z);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}
