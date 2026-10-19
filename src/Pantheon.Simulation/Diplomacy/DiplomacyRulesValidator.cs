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

        ValidatePolitics(rules.Politics, errors);
        errors.ThrowIfAny("diplomacy rules", nameof(rules));
    }

    /// <summary>Checks treaties, favours and the council (ADR-025).</summary>
    private static void ValidatePolitics(Domain.Politics.PoliticsRules? politics, RuleErrors errors)
    {
        if (politics is null)
        {
            errors.Add("Politics is required.");
            return;
        }

        foreach (var (value, name) in new[]
        {
            (politics.MaxTreatySeconds, nameof(politics.MaxTreatySeconds)),
            (politics.MaxTribute, nameof(politics.MaxTribute)),
            (politics.MaxPowerAsk, nameof(politics.MaxPowerAsk)),
            (politics.MaxFavours, nameof(politics.MaxFavours)),
            (politics.AssistSeconds, nameof(politics.AssistSeconds)),
            (politics.FirstCouncilSeconds, nameof(politics.FirstCouncilSeconds)),
            (politics.CouncilIntervalSeconds, nameof(politics.CouncilIntervalSeconds)),
            (politics.CouncilCooldownSeconds, nameof(politics.CouncilCooldownSeconds)),
            (politics.VoteSeconds, nameof(politics.VoteSeconds)),
            (politics.DecreeSeconds, nameof(politics.DecreeSeconds)),
            (politics.StruckSeconds, nameof(politics.StruckSeconds)),
        })
        {
            errors.RequirePositive(value, $"Politics.{name}");
        }

        foreach (var (value, name) in new[]
        {
            (politics.SecretLeakPerSecond, nameof(politics.SecretLeakPerSecond)),
            (politics.TreatyBetrayalShare, nameof(politics.TreatyBetrayalShare)),
            (politics.BrokenPromiseTrustLoss, nameof(politics.BrokenPromiseTrustLoss)),
            (politics.CondemnTrustLoss, nameof(politics.CondemnTrustLoss)),
            (politics.CondemnDoubt, nameof(politics.CondemnDoubt)),
            (politics.DefianceTrustLoss, nameof(politics.DefianceTrustLoss)),
            (politics.CoordinatedCostFactor, nameof(politics.CoordinatedCostFactor)),
            (politics.AbsorbShare, nameof(politics.AbsorbShare)),
        })
        {
            errors.RequireShare(value, $"Politics.{name}");
        }
    }
}
