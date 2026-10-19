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
            (rules.CreatureAffinityPerLevel, nameof(rules.CreatureAffinityPerLevel)),
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
            (rules.ConquestShare, nameof(rules.ConquestShare)),
            (rules.CulturalShare, nameof(rules.CulturalShare)),
            (rules.BlessingDecayPerStep, nameof(rules.BlessingDecayPerStep)),
        })
        {
            errors.RequireShare(value, name);
        }

        ValidateRanks(rules.Ranks, errors);
        errors.RequireAtLeast(rules.PantheonMaxGods, 2, nameof(rules.PantheonMaxGods));
        errors.RequireShare(rules.HolySiteInfluenceStrength, nameof(rules.HolySiteInfluenceStrength));
        errors.RequireNonNegative(rules.HolySiteInfluenceReach, nameof(rules.HolySiteInfluenceReach));
        ValidateSchism(rules.Schism, errors);
        ValidateCulture(rules.Culture, errors);
        Hand.HandRulesValidator.Check(errors, rules.Hand);
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

    /// <summary>Culture's rates and thresholds are shares, in order where they are stages; factors and weights are not negative (ADR-024).</summary>
    private static void ValidateCulture(Domain.Culture.CultureRules? culture, RuleErrors errors)
    {
        if (culture is null || culture.Secular is null)
        {
            errors.Add("Culture and Culture.Secular are required.");
            return;
        }

        foreach (var (value, name) in new[]
        {
            (culture.ResentmentFromFear, nameof(culture.ResentmentFromFear)), (culture.ResentmentFromLove, nameof(culture.ResentmentFromLove)),
            (culture.ResentmentFromHarm, nameof(culture.ResentmentFromHarm)), (culture.DependencyFromHelp, nameof(culture.DependencyFromHelp)),
            (culture.DependencyFade, nameof(culture.DependencyFade)), (culture.DependencyExpectation, nameof(culture.DependencyExpectation)),
            (culture.SecularPowerLoss, nameof(culture.SecularPowerLoss)), (culture.SecularResistance, nameof(culture.SecularResistance)),
            (culture.MiracleResistance, nameof(culture.MiracleResistance)), (culture.Agnostic, nameof(culture.Agnostic)),
            (culture.Atheist, nameof(culture.Atheist)), (culture.AntiDivine, nameof(culture.AntiDivine)),
            (culture.AntiDivineApostasy, nameof(culture.AntiDivineApostasy)), (culture.SecularSpreadPerSecond, nameof(culture.SecularSpreadPerSecond)),
            (culture.SecularDriftPerSecond, nameof(culture.SecularDriftPerSecond)), (culture.DoctrineDriftPerSecond, nameof(culture.DoctrineDriftPerSecond)),
            (culture.Embraced, nameof(culture.Embraced)), (culture.DoctrineFromMiracle, nameof(culture.DoctrineFromMiracle)),
            (culture.DoctrineEffect, nameof(culture.DoctrineEffect)), (culture.CharityPerSecond, nameof(culture.CharityPerSecond)),
            (culture.MissionPerSecond, nameof(culture.MissionPerSecond)), (culture.RebellionResentment, nameof(culture.RebellionResentment)),
            (culture.RebellionShare, nameof(culture.RebellionShare)), (culture.RebellionVent, nameof(culture.RebellionVent)),
            (culture.ConductFadePerSecond, nameof(culture.ConductFadePerSecond)),
        })
        {
            errors.RequireShare(value, "Culture." + name);
        }

        foreach (var (value, name) in new[]
        {
            (culture.DependencyWorship, nameof(culture.DependencyWorship)), (culture.SacrificeWorship, nameof(culture.SacrificeWorship)),
            (culture.SecularScience, nameof(culture.SecularScience)), (culture.Contradiction, nameof(culture.Contradiction)),
            (culture.Secular.Doubt, "Secular.Doubt"), (culture.Secular.Godless, "Secular.Godless"), (culture.Secular.Schism, "Secular.Schism"),
            (culture.Secular.Resentment, "Secular.Resentment"), (culture.Secular.Science, "Secular.Science"), (culture.Secular.Faith, "Secular.Faith"),
        })
        {
            errors.RequireNonNegative(value, "Culture." + name);
        }

        errors.RequirePositive(culture.TraitThreshold, "Culture.TraitThreshold");
        errors.RequireAtLeast(culture.MissionRange, 0, "Culture.MissionRange");
        if (!(culture.Agnostic < culture.Atheist && culture.Atheist < culture.AntiDivine))
        {
            errors.Add("Culture.Agnostic, Atheist and AntiDivine must rise in that order.");
        }
    }

    /// <summary>Schism thresholds and rates are shares; times and populations are not negative (ADR-021).</summary>
    private static void ValidateSchism(SchismRules? schism, RuleErrors errors)
    {
        if (schism is null)
        {
            errors.Add("Schism is required.");
            return;
        }

        foreach (var (value, name) in new[]
        {
            (schism.Doubt, nameof(schism.Doubt)),
            (schism.MinimumAdherence, nameof(schism.MinimumAdherence)),
            (schism.Share, nameof(schism.Share)),
            (schism.DoubtRelief, nameof(schism.DoubtRelief)),
            (schism.BetrayalDoubt, nameof(schism.BetrayalDoubt)),
            (schism.ReconcileLove, nameof(schism.ReconcileLove)),
            (schism.ReconcileDoubt, nameof(schism.ReconcileDoubt)),
            (schism.ReturnPerStep, nameof(schism.ReturnPerStep)),
            (schism.FadePerStep, nameof(schism.FadePerStep)),
        })
        {
            errors.RequireShare(value, "Schism." + name);
        }

        errors.RequireAtLeast(schism.MinimumPopulation, 0, "Schism.MinimumPopulation");
        errors.RequireNonNegative(schism.CooldownSeconds, "Schism.CooldownSeconds");
    }
}
