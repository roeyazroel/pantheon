using Pantheon.Domain.Belief;
using Pantheon.Domain.Hand;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Hand;

/// <summary>Checks <see cref="HandRules"/> (ADR-030) into the divine check's errors.</summary>
internal static class HandRulesValidator
{
    /// <summary>Adds every problem with the hand's rules.</summary>
    /// <param name="errors">The divine check's errors.</param>
    /// <param name="rules">The hand's rules.</param>
    public static void Check(RuleErrors errors, HandRules rules)
    {
        errors.RequireAtLeast(rules.VillagersPerGrab, 1, "Hand.VillagersPerGrab");
        foreach (var (value, name) in new[]
        {
            (rules.MinInfluence, "Hand.MinInfluence"),
            (rules.MaxShare, "Hand.MaxShare"),
            (rules.TreeVegetation, "Hand.TreeVegetation"),
            (rules.TreeCover, "Hand.TreeCover"),
            (rules.ThrowHousingLoss, "Hand.ThrowHousingLoss"),
            (rules.ThrowDeaths, "Hand.ThrowDeaths"),
            (rules.TreeThrowFactor, "Hand.TreeThrowFactor"),
            (rules.ThrowLandHarm, "Hand.ThrowLandHarm"),
            (rules.ThrownCreatureHealth, "Hand.ThrownCreatureHealth"),
        })
        {
            errors.RequireShare(value, name);
        }

        errors.RequirePositive(rules.Reach, "Hand.Reach");
        errors.RequirePositive(rules.ThrowRadius, "Hand.ThrowRadius");
        errors.RequireNonNegative(rules.WitnessRadius, "Hand.WitnessRadius");
        Impulse(errors, rules.Rescue, "Hand.Rescue");
        Impulse(errors, rules.Abduction, "Hand.Abduction");
        Impulse(errors, rules.Smiting, "Hand.Smiting");
        Impulse(errors, rules.Planting, "Hand.Planting");
    }

    /// <summary>An impulse whose changes each lie between -1 and 1.</summary>
    private static void Impulse(RuleErrors errors, BeliefImpulse impulse, string name)
    {
        errors.RequireSignedShare(impulse.Faith, name + ".Faith");
        errors.RequireSignedShare(impulse.Love, name + ".Love");
        errors.RequireSignedShare(impulse.Fear, name + ".Fear");
        errors.RequireSignedShare(impulse.Doubt, name + ".Doubt");
    }
}
