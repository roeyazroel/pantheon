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
        ValidateSpecies(rules.Species, errors);
        ValidateTemperament(rules.Temperament, errors);

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

        ValidateBonds(rules.Bonds, rules.PerceptionRadius, errors);
        ValidateCombat(rules.Combat, errors);
        CallingRulesValidator.Check(errors, rules.Callings);
        errors.ThrowIfAny("creature rules", nameof(rules));
    }

    /// <summary>Distances within sight, shares in 0..1 and non-negative rates (ADR-017).</summary>
    private static void ValidateBonds(CreatureBondRules bonds, float perception, RuleErrors errors)
    {
        if (bonds is null)
        {
            errors.Add("Bonds is required.");
            return;
        }

        errors.RequirePositive(bonds.MeetRadius, "Bonds.MeetRadius");
        errors.RequireNonNegative(bonds.MeetCooldownSeconds, "Bonds.MeetCooldownSeconds");
        if (!(bonds.SeekRadius >= bonds.MeetRadius && bonds.SeekRadius <= perception) || !(bonds.FleeRadius >= bonds.MeetRadius && bonds.FleeRadius <= bonds.SeekRadius))
        {
            errors.Add("Bonds.SeekRadius must lie between MeetRadius and PerceptionRadius, and FleeRadius between MeetRadius and SeekRadius.");
        }

        foreach (var (value, name) in new[]
        {
            (bonds.StartingTrust, nameof(bonds.StartingTrust)),
            (bonds.WarFightDrive, nameof(bonds.WarFightDrive)),
            (bonds.AllianceBefriendDrive, nameof(bonds.AllianceBefriendDrive)),
            (bonds.FriendThreshold, nameof(bonds.FriendThreshold)),
            (bonds.RivalThreshold, nameof(bonds.RivalThreshold)),
            (bonds.FriendshipPerPlay, nameof(bonds.FriendshipPerPlay)),
            (bonds.TrustPerPlay, nameof(bonds.TrustPerPlay)),
            (bonds.FriendshipPerGreeting, nameof(bonds.FriendshipPerGreeting)),
            (bonds.RivalryPerFight, nameof(bonds.RivalryPerFight)),
            (bonds.FriendshipLossPerFight, nameof(bonds.FriendshipLossPerFight)),
            (bonds.BetrayalTrustLoss, nameof(bonds.BetrayalTrustLoss)),
            (bonds.FearPerDefeat, nameof(bonds.FearPerDefeat)),
            (bonds.RespectPerDefeat, nameof(bonds.RespectPerDefeat)),
            (bonds.RespectPerFight, nameof(bonds.RespectPerFight)),
            (bonds.FearPerFlight, nameof(bonds.FearPerFlight)),
            (bonds.TrustPerSpare, nameof(bonds.TrustPerSpare)),
            (bonds.FriendshipPerSpare, nameof(bonds.FriendshipPerSpare)),
            (bonds.RivalryPerAttack, nameof(bonds.RivalryPerAttack)),
            (bonds.FearFadePerSecond, nameof(bonds.FearFadePerSecond)),
            (bonds.RivalryFadePerSecond, nameof(bonds.RivalryFadePerSecond)),
            (bonds.FriendshipFadePerSecond, nameof(bonds.FriendshipFadePerSecond)),
            (bonds.TrustDriftPerSecond, nameof(bonds.TrustDriftPerSecond)),
        })
        {
            errors.RequireShare(value, "Bonds." + name);
        }

        errors.RequireAtLeast(bonds.EncounterMemory, 1, "Bonds.EncounterMemory");
    }

    /// <summary>Positive damage, shares in 0..1 and impulses in -1..1 (ADR-017).</summary>
    private static void ValidateCombat(CreatureCombatRules combat, RuleErrors errors)
    {
        if (combat is null)
        {
            errors.Add("Combat is required.");
            return;
        }

        errors.RequirePositive(combat.DamagePerSecond, "Combat.DamagePerSecond");
        errors.RequireNonNegative(combat.SkillPower, "Combat.SkillPower");
        errors.RequireNonNegative(combat.WinnerExperience, "Combat.WinnerExperience");
        errors.RequireNonNegative(combat.WitnessRadius, "Combat.WitnessRadius");
        errors.RequireNonNegative(combat.SpareThreshold, "Combat.SpareThreshold");
        errors.RequireNonNegative(combat.RespiteSeconds, "Combat.RespiteSeconds");
        foreach (var (value, name) in new[]
        {
            (combat.Jitter, nameof(combat.Jitter)),
            (combat.YieldHealth, nameof(combat.YieldHealth)),
            (combat.SkillPerFight, nameof(combat.SkillPerFight)),
            (combat.DefeatExperienceLoss, nameof(combat.DefeatExperienceLoss)),
            (combat.LoyaltyPerDefeat, nameof(combat.LoyaltyPerDefeat)),
            (combat.AggressionPerVictory, nameof(combat.AggressionPerVictory)),
            (combat.AggressionPerDefeat, nameof(combat.AggressionPerDefeat)),
            (combat.FightLearningRate, nameof(combat.FightLearningRate)),
        })
        {
            errors.RequireShare(value, "Combat." + name);
        }

        if (!(combat.YieldHealth < 1f))
        {
            errors.Add("Combat.YieldHealth must be below 1, or no fight could start.");
        }

        foreach (var (impulse, name) in new[] { (combat.VictoryWitness, "Combat.VictoryWitness"), (combat.DefeatWitness, "Combat.DefeatWitness") })
        {
            errors.RequireSignedShare(impulse.Faith, name + ".Faith");
            errors.RequireSignedShare(impulse.Love, name + ".Love");
            errors.RequireSignedShare(impulse.Fear, name + ".Fear");
            errors.RequireSignedShare(impulse.Doubt, name + ".Doubt");
        }
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

    /// <summary>At least one species, each named once, with positive factors and temperaments within -1..1 (ADR-021).</summary>
    private static void ValidateTemperament(TemperamentRules? temperament, RuleErrors errors)
    {
        if (temperament is null)
        {
            errors.Add("Temperament must not be null.");
            return;
        }

        errors.RequireNonNegative(temperament.GuardFight, "Temperament.GuardFight");
        errors.RequireNonNegative(temperament.GuardRadius, "Temperament.GuardRadius");
        errors.RequireNonNegative(temperament.DefendDrive, "Temperament.DefendDrive");
        foreach (var (value, name) in new[]
        {
            (temperament.EnvyRivalry, nameof(temperament.EnvyRivalry)),
            (temperament.CouragePerVictory, nameof(temperament.CouragePerVictory)),
            (temperament.CouragePerDefeat, nameof(temperament.CouragePerDefeat)),
            (temperament.CouragePerFlight, nameof(temperament.CouragePerFlight)),
            (temperament.CouragePerWound, nameof(temperament.CouragePerWound)),
            (temperament.JealousyPerDefeat, nameof(temperament.JealousyPerDefeat)),
            (temperament.JealousyPerReward, nameof(temperament.JealousyPerReward)),
            (temperament.PlayfulnessPerPlay, nameof(temperament.PlayfulnessPerPlay)),
        })
        {
            errors.RequireShare(value, "Temperament." + name);
        }
    }

    /// <summary>Checks the species.</summary>
    private static void ValidateSpecies(IReadOnlyList<CreatureSpecies>? species, RuleErrors errors)
    {
        if (species is null || species.Count == 0)
        {
            errors.Add("At least one species is required.");
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kind in species)
        {
            if (string.IsNullOrWhiteSpace(kind.Name) || !names.Add(kind.Name) || string.IsNullOrWhiteSpace(kind.Description))
            {
                errors.Add($"Species '{kind.Name}' is unnamed, named twice or lacks a description.");
            }

            errors.RequirePositive(kind.Toughness, kind.Name + ".Toughness");
            errors.RequirePositive(kind.Strength, kind.Name + ".Strength");
            errors.RequirePositive(kind.Speed, kind.Name + ".Speed");
            errors.RequirePositive(kind.Learning, kind.Name + ".Learning");
            errors.RequireSignedShare(kind.Kindness, kind.Name + ".Kindness");
            errors.RequireSignedShare(kind.Aggression, kind.Name + ".Aggression");
            errors.RequireSignedShare(kind.Courage, kind.Name + ".Courage");
            errors.RequireSignedShare(kind.Protectiveness, kind.Name + ".Protectiveness");
            errors.RequireSignedShare(kind.Jealousy, kind.Name + ".Jealousy");
            errors.RequireSignedShare(kind.Playfulness, kind.Name + ".Playfulness");
        }
    }
}
