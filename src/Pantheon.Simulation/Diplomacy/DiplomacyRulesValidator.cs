using System;
using Pantheon.Domain.Diplomacy;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Diplomacy;

/// <summary>Validates data-driven <see cref="DiplomacyRules"/>.</summary>
public static class DiplomacyRulesValidator
{
    /// <summary>Throws when any rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(DiplomacyRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireAtLeast(rules.MemoryCapacity, 1, nameof(rules.MemoryCapacity));
        errors.RequireNonNegative(rules.ProposalSeconds, nameof(rules.ProposalSeconds));
        errors.RequireNonNegative(rules.StatusCooldownSeconds, nameof(rules.StatusCooldownSeconds));
        errors.RequireNonNegative(rules.ProposalCooldownSeconds, nameof(rules.ProposalCooldownSeconds));
        errors.RequireNonNegative(rules.WarCreatureAggression, nameof(rules.WarCreatureAggression));
        foreach (var (value, name) in new[]
        {
            (rules.StartingTrust, nameof(rules.StartingTrust)),
            (rules.StartingRivalry, nameof(rules.StartingRivalry)),
            (rules.StartingSuspicion, nameof(rules.StartingSuspicion)),
            (rules.DriftPerStep, nameof(rules.DriftPerStep)),
            (rules.MemoryFadePerStep, nameof(rules.MemoryFadePerStep)),
            (rules.BetrayalFadePerStep, nameof(rules.BetrayalFadePerStep)),
            (rules.HelpGratitude, nameof(rules.HelpGratitude)),
            (rules.CourtingRivalry, nameof(rules.CourtingRivalry)),
            (rules.HarmTrustLoss, nameof(rules.HarmTrustLoss)),
            (rules.HarmRivalry, nameof(rules.HarmRivalry)),
            (rules.BetrayalTrustLoss, nameof(rules.BetrayalTrustLoss)),
            (rules.BetrayalReputationLoss, nameof(rules.BetrayalReputationLoss)),
            (rules.WarResistance, nameof(rules.WarResistance)),
            (rules.HostilityThreshold, nameof(rules.HostilityThreshold)),
        })
        {
            errors.RequireShare(value, name);
        }

        errors.ThrowIfAny("diplomacy rules", nameof(rules));
    }
}
