using System;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;

namespace Pantheon.Simulation.Settlements;

/// <summary>
/// What a settlement's technology, buildings and priority do for its life (spec §21, §29, §36,
/// ADR-023), as factors the settlement model applies. Pure functions of the rules and the settlement.
/// </summary>
public static class CivicModel
{
    // Walls and arms never turn away more than this share of an attack.
    private const float MaxDefence = 0.9f;

    /// <summary>Whether a settlement has raised a building.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="kind">The building.</param>
    /// <returns><c>true</c> when it has.</returns>
    public static bool Has(SettlementState settlement, BuildingKind kind)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.Society.Buildings.Exists(b => b.Kind == kind);
    }

    /// <summary>Whether a settlement has raised a temple to a god.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="godId">The god.</param>
    /// <returns><c>true</c> when it has.</returns>
    public static bool HasTemple(SettlementState settlement, int godId)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        return settlement.Society.Buildings.Exists(b => b.Kind == BuildingKind.Temple && b.GodId == godId);
    }

    /// <summary>Whether the settlement puts a priority first.</summary>
    public static bool Puts(SettlementState settlement, SettlementPriority priority) => settlement.Society.Priority == priority;

    /// <summary>Factor on the harvest: agriculture, and a Food priority.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The factor (1 and up).</returns>
    public static float YieldFactor(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var civics = rules.Civics;
        var priority = Puts(settlement, SettlementPriority.Food) ? 1f + civics.FoodPriorityYield : 1f;
        return (1f + (civics.AgriculturePerLevel * settlement.Society.Technology.Agriculture)) * priority;
    }

    /// <summary>Health a step adds beyond the fed's recovery: medicine, a hospital and a Health priority, closing that share of the gap to full health.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The share (0..1).</returns>
    public static float HealingShare(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var civics = rules.Civics;
        var healing = civics.MedicinePerLevel * settlement.Society.Technology.Medicine;
        healing += Has(settlement, BuildingKind.Hospital) ? civics.HospitalRecovery : 0f;
        healing += Puts(settlement, SettlementPriority.Health) ? civics.HealthPriorityRecovery : 0f;
        return SimMath.Clamp01(healing);
    }

    /// <summary>Factor on the deaths from ill health: medicine and a hospital prevent some.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The factor (0..1).</returns>
    public static float IllnessDeathFactor(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var civics = rules.Civics;
        var saved = (civics.MedicineDeathsPerLevel * settlement.Society.Technology.Medicine) + (Has(settlement, BuildingKind.Hospital) ? civics.HospitalDeaths : 0f);
        return 1f - Math.Clamp(saved, 0f, MaxDefence);
    }

    /// <summary>Factor on births: a Growth priority.</summary>
    public static float BirthFactor(SettlementRules rules, SettlementState settlement) =>
        Puts(settlement, SettlementPriority.Growth) ? 1f + rules.Civics.GrowthPriorityBirths : 1f;

    /// <summary>Factor on the share of the day spent worshipping: a Worship priority.</summary>
    public static float WorshipFactor(SettlementRules rules, SettlementState settlement) =>
        Puts(settlement, SettlementPriority.Worship) ? rules.Civics.WorshipPriorityFactor : 1f;

    /// <summary>Factor on a house's work: architecture.</summary>
    public static float HouseWorkFactor(SettlementRules rules, SettlementState settlement) =>
        1f - Math.Clamp(rules.Civics.ArchitecturePerLevel * settlement.Society.Technology.Architecture, 0f, MaxDefence);

    /// <summary>Factor on food storage: a granary.</summary>
    public static float StorageFactor(SettlementRules rules, SettlementState settlement) =>
        Has(settlement, BuildingKind.Granary) ? rules.Civics.GranaryStorage : 1f;

    /// <summary>Factor on water storage: a cistern.</summary>
    public static float WaterStorageFactor(SettlementRules rules, SettlementState settlement) =>
        Has(settlement, BuildingKind.Cistern) ? rules.Civics.CisternStorage : 1f;

    /// <summary>Share of an attack's harm walls and arms turn away.</summary>
    /// <param name="rules">Settlement rules.</param>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The share (0..0.9).</returns>
    public static float Defence(SettlementRules rules, SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var civics = rules.Civics;
        var defence = (Has(settlement, BuildingKind.Walls) ? civics.WallsDefence : 0f) + (civics.WarfarePerLevel * settlement.Society.Technology.Warfare);
        return Math.Clamp(defence, 0f, MaxDefence);
    }
}
