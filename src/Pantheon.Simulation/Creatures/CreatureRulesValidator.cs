using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Creatures;

/// <summary>Validates data-driven <see cref="CreatureRules"/>.</summary>
public static class CreatureRulesValidator
{
    /// <summary>Throws when any rule is out of range or an action is missing.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(CreatureRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.DecisionIntervalTicks, 1, nameof(rules.DecisionIntervalTicks));
        if (string.IsNullOrWhiteSpace(rules.Species))
        {
            errors.Add("Species must not be empty.");
        }

        foreach (var (value, name) in new[]
        {
            (rules.BaseSpeed, nameof(rules.BaseSpeed)),
            (rules.BaseStrength, nameof(rules.BaseStrength)),
            (rules.PerceptionRadius, nameof(rules.PerceptionRadius)),
            (rules.DistanceHalfUtility, nameof(rules.DistanceHalfUtility)),
        })
        {
            errors.RequirePositive(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (rules.SpeedPerLevel, nameof(rules.SpeedPerLevel)),
            (rules.StrengthPerLevel, nameof(rules.StrengthPerLevel)),
            (rules.DecisionJitter, nameof(rules.DecisionJitter)),
            (rules.HungerPerSecond, nameof(rules.HungerPerSecond)),
            (rules.FatiguePerSecond, nameof(rules.FatiguePerSecond)),
            (rules.BoredomPerSecond, nameof(rules.BoredomPerSecond)),
            (rules.StarvationHealthLossPerSecond, nameof(rules.StarvationHealthLossPerSecond)),
            (rules.RestHealthPerSecond, nameof(rules.RestHealthPerSecond)),
            (rules.BanishedSeconds, nameof(rules.BanishedSeconds)),
            (rules.FeedbackWindowSeconds, nameof(rules.FeedbackWindowSeconds)),
            (rules.CommandStaySeconds, nameof(rules.CommandStaySeconds)),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (rules.HungryThreshold, nameof(rules.HungryThreshold)),
            (rules.TiredThreshold, nameof(rules.TiredThreshold)),
            (rules.BoredThreshold, nameof(rules.BoredThreshold)),
            (rules.VillageNeedThreshold, nameof(rules.VillageNeedThreshold)),
            (rules.RewardLearningRate, nameof(rules.RewardLearningRate)),
            (rules.PunishmentLearningRate, nameof(rules.PunishmentLearningRate)),
            (rules.PersonalityNudge, nameof(rules.PersonalityNudge)),
            (rules.StartingLoyalty, nameof(rules.StartingLoyalty)),
            (rules.LoyaltyPerReward, nameof(rules.LoyaltyPerReward)),
            (rules.LoyaltyPerPunishment, nameof(rules.LoyaltyPerPunishment)),
            (rules.PersonalitySpread, nameof(rules.PersonalitySpread)),
        })
        {
            errors.RequireShare(value, name);
        }

        errors.RequireAtLeast(rules.MemoryCapacity, 1, nameof(rules.MemoryCapacity));
        ValidateLevels(rules.LevelExperience, errors);
        ValidateActions(rules, errors);
        foreach (var preference in rules.StartingPreferences)
        {
            errors.RequireDefined(preference.Situation, "StartingPreferences.Situation");
            errors.RequireDefined(preference.Action, "StartingPreferences.Action");
            errors.RequireShare(preference.Weight, $"StartingPreferences {preference.Situation}/{preference.Action}");
        }

        errors.ThrowIfAny("creature rules", nameof(rules));
    }

    /// <summary>Rising, positive thresholds, at most one per level above the first.</summary>
    private static void ValidateLevels(IReadOnlyList<float> levels, RuleErrors errors)
    {
        if (levels.Count >= CreatureModel.MaxLevel)
        {
            errors.Add($"LevelExperience lists at most {CreatureModel.MaxLevel - 1} thresholds.");
        }

        var previous = 0f;
        foreach (var needed in levels)
        {
            if (!(needed > previous))
            {
                errors.Add("LevelExperience must rise and start above 0.");
            }

            previous = needed;
        }
    }

    /// <summary>Exactly one rule for each action except GoTo, with valid numbers.</summary>
    private static void ValidateActions(CreatureRules rules, RuleErrors errors)
    {
        var seen = new HashSet<CreatureAction>();
        foreach (var rule in rules.Actions)
        {
            errors.RequireDefined(rule.Action, "Actions.Action");
            if (rule.Action == CreatureAction.GoTo || !seen.Add(rule.Action))
            {
                errors.Add($"Actions lists {rule.Action} more than once or lists GoTo, which has no rule.");
            }

            var prefix = $"Actions.{rule.Action}.";
            errors.RequirePositive(rule.Seconds, prefix + nameof(rule.Seconds));
            errors.RequireNonNegative(rule.Amount, prefix + nameof(rule.Amount));
            errors.RequireNonNegative(rule.Experience, prefix + nameof(rule.Experience));
            errors.RequireSignedShare(rule.HungerRelief, prefix + nameof(rule.HungerRelief));
            errors.RequireSignedShare(rule.FatigueRelief, prefix + nameof(rule.FatigueRelief));
            errors.RequireSignedShare(rule.BoredomRelief, prefix + nameof(rule.BoredomRelief));
            errors.RequireSignedShare(rule.Kindness, prefix + nameof(rule.Kindness));
            errors.RequireSignedShare(rule.Witness.Faith, prefix + "Witness.Faith");
            errors.RequireSignedShare(rule.Witness.Love, prefix + "Witness.Love");
            errors.RequireSignedShare(rule.Witness.Fear, prefix + "Witness.Fear");
            errors.RequireSignedShare(rule.Witness.Doubt, prefix + "Witness.Doubt");
        }

        foreach (var action in Enum.GetValues<CreatureAction>())
        {
            if (action != CreatureAction.GoTo && !seen.Contains(action))
            {
                errors.Add($"Actions has no rule for {action}.");
            }
        }
    }
}
