using System;
using System.Collections.Generic;
using Pantheon.Domain.HolySites;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.HolySites;

/// <summary>Validates data-driven <see cref="HolySiteRules"/> (ADR-021).</summary>
public static class HolySiteRulesValidator
{
    /// <summary>Throws when a rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(HolySiteRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireShare(rules.ClaimInfluence, nameof(rules.ClaimInfluence));
        errors.RequireShare(rules.ClaimLead, nameof(rules.ClaimLead));
        errors.RequirePositive(rules.ClaimSeconds, nameof(rules.ClaimSeconds));
        errors.RequireAtLeast(rules.MinimumSpacing, 0, nameof(rules.MinimumSpacing));
        var kinds = new HashSet<HolySiteKind>();
        foreach (var site in rules.Sites ?? [])
        {
            var prefix = site.Kind + ".";
            errors.RequireDefined(site.Kind, "Sites.Kind");
            errors.RequireDefined(site.Boon, prefix + nameof(site.Boon));
            if (!kinds.Add(site.Kind) || string.IsNullOrWhiteSpace(site.Name) || string.IsNullOrWhiteSpace(site.Description))
            {
                errors.Add($"Holy site {site.Kind} is listed twice or lacks a name or description.");
            }

            errors.RequireNonNegative(site.PowerPerStep, prefix + nameof(site.PowerPerStep));
            errors.RequireNonNegative(site.BoonReach, prefix + nameof(site.BoonReach));
            if (site.Boon == HolySiteBoon.Wisdom)
            {
                errors.RequireNonNegative(site.BoonAmount, prefix + nameof(site.BoonAmount));
            }
            else
            {
                errors.RequireShare(site.BoonAmount, prefix + nameof(site.BoonAmount));
            }

            if (site.Boon != HolySiteBoon.CheaperMiracles)
            {
                errors.RequirePositive(site.BoonReach, prefix + nameof(site.BoonReach));
            }
        }

        if (kinds.Count == 0)
        {
            errors.Add("At least one holy site is required.");
        }

        errors.ThrowIfAny("holy site rules", nameof(rules));
    }
}
