using System;
using System.Collections.Generic;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Gods;

/// <summary>Validates data-driven <see cref="GodAiRules"/>.</summary>
public static class GodAiRulesValidator
{
    /// <summary>Throws when any rule is out of range or a personality is missing.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(GodAiRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.EvaluationIntervalTicks, 1, nameof(rules.EvaluationIntervalTicks));
        errors.RequireNonNegative(rules.Patience, nameof(rules.Patience));
        errors.RequireNonNegative(rules.DecisionJitter, nameof(rules.DecisionJitter));
        errors.RequirePositive(rules.ReferencePopulation, nameof(rules.ReferencePopulation));
        errors.RequireNonNegative(rules.CreatureSendDistance, nameof(rules.CreatureSendDistance));
        var seen = new HashSet<GodPersonality>();
        foreach (var profile in rules.Profiles)
        {
            errors.RequireDefined(profile.Personality, "Profiles.Personality");
            if (profile.Personality == GodPersonality.None || !seen.Add(profile.Personality))
            {
                errors.Add($"Profiles lists {profile.Personality} more than once or lists None.");
            }

            var prefix = $"Profiles.{profile.Personality}.";
            errors.RequireShare(profile.Protect, prefix + nameof(profile.Protect));
            errors.RequireShare(profile.Convert, prefix + nameof(profile.Convert));
            errors.RequireShare(profile.Intimidate, prefix + nameof(profile.Intimidate));
            errors.RequireShare(profile.Bless, prefix + nameof(profile.Bless));
            errors.RequireShare(profile.Opportunism, prefix + nameof(profile.Opportunism));
            errors.RequireShare(profile.Thrift, prefix + nameof(profile.Thrift));
            errors.RequireShare(profile.SendCreature, prefix + nameof(profile.SendCreature));
            errors.RequireSignedShare(profile.CreatureKindness, prefix + nameof(profile.CreatureKindness));
        }

        foreach (var personality in Enum.GetValues<GodPersonality>())
        {
            if (personality != GodPersonality.None && !seen.Contains(personality))
            {
                errors.Add($"Profiles has no profile for {personality}.");
            }
        }

        errors.ThrowIfAny("god AI rules", nameof(rules));
    }
}
