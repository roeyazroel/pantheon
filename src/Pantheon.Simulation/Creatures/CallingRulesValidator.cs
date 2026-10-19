using Pantheon.Domain.Creatures;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Creatures;

/// <summary>Checks <see cref="CallingRules"/> (ADR-030) into the Creature check's errors.</summary>
internal static class CallingRulesValidator
{
    /// <summary>Adds every problem with the paths and the attributes' growth.</summary>
    /// <param name="errors">The Creature check's errors.</param>
    /// <param name="rules">The calling rules.</param>
    public static void Check(RuleErrors errors, CallingRules rules)
    {
        var previous = 1;
        foreach (var level in rules.Levels)
        {
            if (level <= previous || level > CreatureModel.MaxLevel)
            {
                errors.Add($"Callings.Levels must rise from above 1 to at most {CreatureModel.MaxLevel}.");
            }

            previous = level;
        }

        foreach (var deed in rules.Deeds)
        {
            errors.RequireDefined(deed.Action, "Callings.Deeds.Action");
            errors.RequireDefined(deed.Specialisation, "Callings.Deeds.Specialisation");
            errors.RequireNonNegative(deed.Weight, "Callings.Deeds.Weight");
        }

        foreach (var (value, name) in new[]
        {
            (rules.RewardCalling, "Callings.RewardCalling"),
            (rules.VictoryCalling, "Callings.VictoryCalling"),
            (rules.MercyCalling, "Callings.MercyCalling"),
            (rules.IntelligencePerLevel, "Callings.IntelligencePerLevel"),
            (rules.ResiliencePerLevel, "Callings.ResiliencePerLevel"),
            (rules.GuardRadius, "Callings.GuardRadius"),
            (rules.WarriorPower, "Callings.WarriorPower"),
            (rules.DestroyerAttack, "Callings.DestroyerAttack"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (rules.GuardianShield, "Callings.GuardianShield"),
            (rules.HealerHealth, "Callings.HealerHealth"),
            (rules.KeeperHealing, "Callings.KeeperHealing"),
            (rules.MissionaryConversion, "Callings.MissionaryConversion"),
            (rules.TricksterDoubt, "Callings.TricksterDoubt"),
            (rules.CommanderDefence, "Callings.CommanderDefence"),
        })
        {
            errors.RequireShare(value, name);
        }
    }
}
