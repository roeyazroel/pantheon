using Pantheon.Domain.Villagers;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Villagers;

/// <summary>Checks <see cref="VillagerRules"/> (ADR-043) into the settlement check's errors.</summary>
internal static class VillagerRulesValidator
{
    /// <summary>Adds an error for every invalid villager rule; rules without names follow no one and are not checked.</summary>
    /// <param name="errors">Errors so far.</param>
    /// <param name="rules">Villager rules.</param>
    public static void Check(RuleErrors errors, VillagerRules rules)
    {
        if (!rules.Enabled)
        {
            return;
        }

        if (System.Linq.Enumerable.Any(rules.Names, string.IsNullOrWhiteSpace))
        {
            errors.Add("Villagers.Names must not be blank.");
        }

        if (rules.MinAge < 0f || rules.MaxAge < rules.MinAge || rules.MaxLifeYears < rules.MinLifeYears || rules.MinLifeYears <= 0f)
        {
            errors.Add("Villagers' ages must run from MinAge to MaxAge, and their lives from MinLifeYears to MaxLifeYears.");
        }

        foreach (var (value, name) in new[]
        {
            (rules.SecondsPerYear, "Villagers.SecondsPerYear"), (rules.Speed, "Villagers.Speed"), (rules.VillageRadius, "Villagers.VillageRadius"),
            (rules.SpotRange, "Villagers.SpotRange"), (rules.WorkSeconds, "Villagers.WorkSeconds"), (rules.EatSeconds, "Villagers.EatSeconds"),
            (rules.RestSeconds, "Villagers.RestSeconds"), (rules.WorshipSeconds, "Villagers.WorshipSeconds"), (rules.WorshipGapSeconds, "Villagers.WorshipGapSeconds"),
        })
        {
            errors.RequirePositive(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (rules.HungerPerSecond, "Villagers.HungerPerSecond"), (rules.EatPerSecond, "Villagers.EatPerSecond"), (rules.FatiguePerSecond, "Villagers.FatiguePerSecond"),
            (rules.RestPerSecond, "Villagers.RestPerSecond"), (rules.WorkDrive, "Villagers.WorkDrive"), (rules.WorshipDrive, "Villagers.WorshipDrive"),
            (rules.HardshipWorship, "Villagers.HardshipWorship"), (rules.StarvePerSecond, "Villagers.StarvePerSecond"), (rules.HealPerSecond, "Villagers.HealPerSecond"),
            (rules.PlagueHarmPerSecond, "Villagers.PlagueHarmPerSecond"), (rules.Mortality, "Villagers.Mortality"), (rules.FaithDrift, "Villagers.FaithDrift"),
            (rules.WorshipFaith, "Villagers.WorshipFaith"), (rules.WitnessRadius, "Villagers.WitnessRadius"), (rules.FleeRange, "Villagers.FleeRange"),
            (rules.FleeDistance, "Villagers.FleeDistance"), (rules.HardshipSeconds, "Villagers.HardshipSeconds"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        errors.RequireShare(rules.FleeDanger, "Villagers.FleeDanger");
        errors.RequireShare(rules.WaryFlee, "Villagers.WaryFlee");
        errors.RequireShare(rules.WitnessFaith, "Villagers.WitnessFaith");
        errors.RequireShare(rules.ConvertFaith, "Villagers.ConvertFaith");
        errors.RequireAtLeast(rules.MemoryCapacity, 1, "Villagers.MemoryCapacity");
    }
}
