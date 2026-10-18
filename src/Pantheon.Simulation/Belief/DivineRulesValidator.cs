using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Belief;

/// <summary>Validates data-driven <see cref="DivineRules"/>.</summary>
public static class DivineRulesValidator
{
    /// <summary>Throws when any rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(DivineRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        foreach (var (value, name) in new[]
        {
            (rules.PowerPerWorshipperPerStep, nameof(rules.PowerPerWorshipperPerStep)),
            (rules.BaseWorshipIntensity, nameof(rules.BaseWorshipIntensity)),
            (rules.LoveWorshipIntensity, nameof(rules.LoveWorshipIntensity)),
            (rules.FearWorshipIntensity, nameof(rules.FearWorshipIntensity)),
            (rules.ShrineInfrastructure, nameof(rules.ShrineInfrastructure)),
            (rules.BaseConversion, nameof(rules.BaseConversion)),
            (rules.LoveConversion, nameof(rules.LoveConversion)),
            (rules.FearConversion, nameof(rules.FearConversion)),
            (rules.BaseApostasy, nameof(rules.BaseApostasy)),
            (rules.FearFragility, nameof(rules.FearFragility)),
            (rules.InfluenceBaseRadius, nameof(rules.InfluenceBaseRadius)),
            (rules.InfluenceRadiusPerSqrtFollower, nameof(rules.InfluenceRadiusPerSqrtFollower)),
            (rules.InfluenceStrength, nameof(rules.InfluenceStrength)),
            (rules.WitnessRadius, nameof(rules.WitnessRadius)),
            (rules.CreatureInfluenceReach, nameof(rules.CreatureInfluenceReach)),
            (rules.ApostateFreedom, nameof(rules.ApostateFreedom)),
            (rules.ContestedCostFactor, nameof(rules.ContestedCostFactor)),
            (rules.VictorySeconds, nameof(rules.VictorySeconds)),
            (rules.SettlementRadius, nameof(rules.SettlementRadius)),
            (rules.WeakInfluenceCostFactor, nameof(rules.WeakInfluenceCostFactor)),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        errors.RequirePositive(rules.ComfortFoodPerPerson, nameof(rules.ComfortFoodPerPerson));
        errors.RequireShare(rules.DroughtWorry, nameof(rules.DroughtWorry));
        errors.RequireShare(rules.DangerWorry, nameof(rules.DangerWorry));
        errors.RequireShare(rules.DisasterFear, nameof(rules.DisasterFear));
        errors.RequireShare(rules.DisasterDoubt, nameof(rules.DisasterDoubt));
        foreach (var (value, name) in new[]
        {
            (rules.FaithFromWorship, nameof(rules.FaithFromWorship)),
            (rules.FaithErosionFromDoubt, nameof(rules.FaithErosionFromDoubt)),
            (rules.FaithForgettingPerStep, nameof(rules.FaithForgettingPerStep)),
            (rules.LoveFromComfort, nameof(rules.LoveFromComfort)),
            (rules.LoveDecayPerStep, nameof(rules.LoveDecayPerStep)),
            (rules.LoveLossFromHardship, nameof(rules.LoveLossFromHardship)),
            (rules.FearDecayPerStep, nameof(rules.FearDecayPerStep)),
            (rules.SilenceDoubt, nameof(rules.SilenceDoubt)),
            (rules.DoubtDriftPerStep, nameof(rules.DoubtDriftPerStep)),
            (rules.DoubtFromHardship, nameof(rules.DoubtFromHardship)),
            (rules.NewBelieverDoubt, nameof(rules.NewBelieverDoubt)),
            (rules.ConversionPerStep, nameof(rules.ConversionPerStep)),
            (rules.ApostasyPerStep, nameof(rules.ApostasyPerStep)),
            (rules.LoveLoyalty, nameof(rules.LoveLoyalty)),
            (rules.ForgottenFaith, nameof(rules.ForgottenFaith)),
            (rules.GratitudeFloor, nameof(rules.GratitudeFloor)),
            (rules.CreatureInfluenceStrength, nameof(rules.CreatureInfluenceStrength)),
            (rules.RivalMiracleDoubt, nameof(rules.RivalMiracleDoubt)),
            (rules.VictoryShare, nameof(rules.VictoryShare)),
            (rules.BlessingDecayPerStep, nameof(rules.BlessingDecayPerStep)),
        })
        {
            errors.RequireShare(value, name);
        }

        ValidateRanks(rules.Ranks, errors);
        errors.ThrowIfAny("divine rules", nameof(rules));
    }

    /// <summary>Ranks above the first, each once, needing more followers the higher they are.</summary>
    private static void ValidateRanks(IReadOnlyList<RankThreshold> ranks, RuleErrors errors)
    {
        var seen = new HashSet<DivineRank>();
        RankThreshold? previous = null;
        foreach (var threshold in ranks)
        {
            errors.RequireDefined(threshold.Rank, nameof(DivineRules.Ranks));
            if (threshold.Rank == DivineRank.MinorSpirit || !seen.Add(threshold.Rank))
            {
                errors.Add($"Ranks must list each rank above {DivineRank.MinorSpirit} at most once; {threshold.Rank} is not allowed.");
            }

            if (previous is not null && (threshold.Rank <= previous.Rank || threshold.Followers <= previous.Followers))
            {
                errors.Add("Ranks must be in rising order of rank and followers.");
            }

            errors.RequireAtLeast(threshold.Followers, 1, $"Followers of {threshold.Rank}");
            previous = threshold;
        }
    }
}
