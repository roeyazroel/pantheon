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
        if (!(rules.RainDrought is >= 0f and < 1f))
        {
            errors.Add($"{nameof(rules.RainDrought)} must be at least 0 and below 1.");
        }

        errors.RequireNonNegative(rules.CreatureSendDistance, nameof(rules.CreatureSendDistance));
        if (!(rules.TemperReach is >= 0f and <= 0.5f))
        {
            errors.Add($"{nameof(rules.TemperReach)} must lie from 0 to 0.5.");
        }

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
            errors.RequireShare(profile.Cooperation, prefix + nameof(profile.Cooperation));
            errors.RequireShare(profile.Aggression, prefix + nameof(profile.Aggression));
            errors.RequireShare(profile.Treachery, prefix + nameof(profile.Treachery));
            errors.RequireShare(profile.Grudge, prefix + nameof(profile.Grudge));
            errors.RequireDefined(profile.RefugeePolicy, prefix + nameof(profile.RefugeePolicy));
            errors.RequireShare(profile.HolySites, prefix + nameof(profile.HolySites));
            var domains = new HashSet<GodDomain>();
            foreach (var domain in profile.Domains ?? [])
            {
                errors.RequireDefined(domain, prefix + nameof(profile.Domains));
                if (!domains.Add(domain))
                {
                    errors.Add($"{prefix}Domains lists {domain} more than once.");
                }
            }
        }

        foreach (var personality in Enum.GetValues<GodPersonality>())
        {
            if (personality != GodPersonality.None && !seen.Contains(personality))
            {
                errors.Add($"Profiles has no profile for {personality}.");
            }
        }

        ValidatePlanning(rules.Planning, errors);
        var watch = rules.Watch;
        if (watch.Radius < 0f || watch.IntervalTicks < 1 || watch.CooldownSeconds < 0f || !(watch.RepelConfidence is >= 0f and <= 1f)
            || !(watch.WelcomeHostility is >= 0f and <= 1f) || !(watch.RepelHostility is >= 0f and <= 1f) || watch.WelcomeHostility >= watch.RepelHostility)
        {
            errors.Add("Watch needs a radius of at least 0, an interval of at least 1, a cooldown of at least 0, a confidence from 0 to 1, and a welcome hostility below the repel hostility, both from 0 to 1.");
        }

        errors.ThrowIfAny("god AI rules", nameof(rules));
    }

    /// <summary>Checks the major planning (ADR-036).</summary>
    private static void ValidatePlanning(PlanningRules? planning, RuleErrors errors)
    {
        if (planning is null)
        {
            errors.Add("Planning must not be null.");
            return;
        }

        errors.RequireNonNegative(planning.PlanSeconds, "Planning.PlanSeconds");
        errors.RequireAtLeast(planning.MaxGoals, 0, "Planning.MaxGoals");
        errors.RequireNonNegative(planning.MinGoalSeconds, "Planning.MinGoalSeconds");
        errors.RequireShare(planning.Threshold, "Planning.Threshold");
        if (!(planning.Boost >= 1f))
        {
            errors.Add("Planning.Boost must be at least 1.");
        }

        errors.RequireNonNegative(planning.DiplomacyBias, "Planning.DiplomacyBias");
        errors.RequireShare(planning.HomeDanger, "Planning.HomeDanger");
        errors.RequireNonNegative(planning.ConvertReach, "Planning.ConvertReach");
        errors.RequireShare(planning.Doubt, "Planning.Doubt");
        errors.RequireShare(planning.Dryness, "Planning.Dryness");
    }
}
