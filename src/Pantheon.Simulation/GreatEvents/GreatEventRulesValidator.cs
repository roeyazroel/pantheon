using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.GreatEvents;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.GreatEvents;

/// <summary>Checks <see cref="GreatEventRules"/> (ADR-034) into the disaster check's errors.</summary>
internal static class GreatEventRulesValidator
{
    /// <summary>Adds an error for every invalid great event rule.</summary>
    /// <param name="errors">Errors so far.</param>
    /// <param name="rules">Great event rules.</param>
    public static void Check(RuleErrors errors, GreatEventRules rules)
    {
        var plague = rules.Plague;
        foreach (var (value, name) in new[]
        {
            (plague.Transmission, "GreatEvents.Plague.Transmission"), (plague.RecoveryPerSecond, "GreatEvents.Plague.RecoveryPerSecond"),
            (plague.ImmunityLossPerSecond, "GreatEvents.Plague.ImmunityLossPerSecond"), (plague.HealthLossPerSecond, "GreatEvents.Plague.HealthLossPerSecond"),
            (plague.ContactPerSecond, "GreatEvents.Plague.ContactPerSecond"), (plague.RoadPerSecond, "GreatEvents.Plague.RoadPerSecond"),
            (plague.HospitalShield, "GreatEvents.Plague.HospitalShield"), (plague.MedicinePerLevel, "GreatEvents.Plague.MedicinePerLevel"),
            (rules.WarFearPerSecond, "GreatEvents.WarFearPerSecond"),
        })
        {
            errors.RequireShare(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (plague.OutbreakPerHour, "GreatEvents.Plague.OutbreakPerHour"), (plague.Crowding, "GreatEvents.Plague.Crowding"),
            (plague.ContactRange, "GreatEvents.Plague.ContactRange"), (plague.HealCure, "GreatEvents.Plague.HealCure"),
            (rules.Eclipse.FirstMinSeconds, "GreatEvents.Eclipse.FirstMinSeconds"), (rules.Eclipse.Seconds, "GreatEvents.Eclipse.Seconds"),
            (rules.Eclipse.IntervalMinSeconds, "GreatEvents.Eclipse.IntervalMinSeconds"), (rules.Eclipse.Awe, "GreatEvents.Eclipse.Awe"),
            (rules.Reformation.CooldownSeconds, "GreatEvents.Reformation.CooldownSeconds"), (rules.DroughtSeconds, "GreatEvents.DroughtSeconds"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        errors.RequireAtLeast(plague.EventVillages, 1, "GreatEvents.Plague.EventVillages");
        if (rules.Eclipse.FirstMaxSeconds < rules.Eclipse.FirstMinSeconds || rules.Eclipse.IntervalMaxSeconds < rules.Eclipse.IntervalMinSeconds)
        {
            errors.Add("GreatEvents.Eclipse's longest waits must be at least its shortest.");
        }

        Impulse(errors, rules.Eclipse.Dread, "GreatEvents.Eclipse.Dread");
        Beast(errors, rules.Beast);
        var anomaly = rules.Anomaly;
        foreach (var (value, name) in new[]
        {
            (anomaly.FirstMinSeconds, "GreatEvents.Anomaly.FirstMinSeconds"), (anomaly.IntervalMinSeconds, "GreatEvents.Anomaly.IntervalMinSeconds"),
            (anomaly.Seconds, "GreatEvents.Anomaly.Seconds"), (anomaly.Radius, "GreatEvents.Anomaly.Radius"), (anomaly.PowerPerSecond, "GreatEvents.Anomaly.PowerPerSecond"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        errors.RequireShare(anomaly.CostFactor, "GreatEvents.Anomaly.CostFactor");
        if (anomaly.FirstMaxSeconds < anomaly.FirstMinSeconds || anomaly.IntervalMaxSeconds < anomaly.IntervalMinSeconds)
        {
            errors.Add("GreatEvents.Anomaly's longest waits must be at least its shortest.");
        }
        if (!rules.Equals(GreatEventRules.None))
        {
            Active(errors, rules);
        }

        var kinds = new HashSet<GreatEventKind>();
        foreach (var names in rules.Names)
        {
            if (names is null || !System.Enum.IsDefined(names.Kind) || !kinds.Add(names.Kind) || names.Names is null || names.Names.Count == 0 || System.Linq.Enumerable.Any(names.Names, string.IsNullOrWhiteSpace))
            {
                errors.Add("GreatEvents.Names must list known kinds, each once, with names that are not blank.");
            }
        }
    }

    /// <summary>The checks that only hold for rules that make great events.</summary>
    private static void Active(RuleErrors errors, GreatEventRules rules)
    {
        errors.RequireAtLeast(rules.WorldWarMinGods, 2, "GreatEvents.WorldWarMinGods");
        errors.RequireShare(rules.WorldWarShare, "GreatEvents.WorldWarShare");
        errors.RequireShare(rules.MigrationShare, "GreatEvents.MigrationShare");
        errors.RequireShare(rules.DroughtSoil, "GreatEvents.DroughtSoil");
        errors.RequireShare(rules.Reformation.Doubt, "GreatEvents.Reformation.Doubt");
        errors.RequireShare(rules.Reformation.Learning, "GreatEvents.Reformation.Learning");
        errors.RequireShare(rules.Reformation.DoubtKept, "GreatEvents.Reformation.DoubtKept");
        errors.RequireShare(rules.Reformation.Faith, "GreatEvents.Reformation.Faith");
        errors.RequireShare(rules.Reformation.Leaving, "GreatEvents.Reformation.Leaving");
        errors.RequireAtLeast(rules.Reformation.MinVillages, 1, "GreatEvents.Reformation.MinVillages");
    }

    /// <summary>Checks the beast's rules.</summary>
    private static void Beast(RuleErrors errors, BeastRules beast)
    {
        if (System.Linq.Enumerable.Any(beast.Names, string.IsNullOrWhiteSpace))
        {
            errors.Add("GreatEvents.Beast.Names must not be blank.");
        }

        foreach (var (value, name) in new[]
        {
            (beast.FirstSleepSeconds, "GreatEvents.Beast.FirstSleepSeconds"), (beast.WakePerHour, "GreatEvents.Beast.WakePerHour"),
            (beast.WakeRange, "GreatEvents.Beast.WakeRange"), (beast.RegenPerSecond, "GreatEvents.Beast.RegenPerSecond"), (beast.Speed, "GreatEvents.Beast.Speed"),
            (beast.AwakeSeconds, "GreatEvents.Beast.AwakeSeconds"), (beast.RavageSeconds, "GreatEvents.Beast.RavageSeconds"),
            (beast.FightRange, "GreatEvents.Beast.FightRange"), (beast.CreatureHarm, "GreatEvents.Beast.CreatureHarm"), (beast.SmiteHarm, "GreatEvents.Beast.SmiteHarm"),
            (beast.FireHarmPerSecond, "GreatEvents.Beast.FireHarmPerSecond"), (beast.CallRange, "GreatEvents.Beast.CallRange"),
        })
        {
            errors.RequireNonNegative(value, name);
        }

        errors.RequirePositive(beast.Health, "GreatEvents.Beast.Health");
        errors.RequirePositive(beast.StrikeSeconds, "GreatEvents.Beast.StrikeSeconds");
        errors.RequireShare(beast.StrikeHousing, "GreatEvents.Beast.StrikeHousing");
        errors.RequireShare(beast.StrikeDeaths, "GreatEvents.Beast.StrikeDeaths");
        errors.RequireShare(beast.Bite, "GreatEvents.Beast.Bite");
        errors.RequireShare(beast.FleeHealth, "GreatEvents.Beast.FleeHealth");
        Impulse(errors, beast.Renown, "GreatEvents.Beast.Renown");
    }

    /// <summary>Checks a feeling's change.</summary>
    private static void Impulse(RuleErrors errors, BeliefImpulse impulse, string name)
    {
        errors.RequireSignedShare(impulse.Faith, name + ".Faith");
        errors.RequireSignedShare(impulse.Love, name + ".Love");
        errors.RequireSignedShare(impulse.Fear, name + ".Fear");
        errors.RequireSignedShare(impulse.Doubt, name + ".Doubt");
    }
}
