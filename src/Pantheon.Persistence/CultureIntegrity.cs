using System;
using System.Collections.Generic;
using Pantheon.Domain.Culture;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;

namespace Pantheon.Persistence;

/// <summary>Structural checks on culture, conduct and the deeper feelings of belief (part of <see cref="SaveIntegrity"/>, ADR-024).</summary>
internal static class CultureIntegrity
{
    /// <summary>Records every inconsistency of a settlement's culture and its beliefs' dependency and resentment.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(SettlementState settlement, List<string> errors)
    {
        if (settlement.Culture is not { } culture)
        {
            errors.Add($"Settlement {settlement.Id} has no culture.");
            return;
        }

        foreach (var doctrine in Enum.GetValues<Doctrine>())
        {
            if (!IsShare(culture.Get(doctrine)))
            {
                errors.Add($"Settlement {settlement.Id} holds {doctrine} at {culture.Get(doctrine)}; it must lie between 0 and 1.");
            }
        }

        if (!IsShare(culture.Secularism))
        {
            errors.Add($"Settlement {settlement.Id} has secularism {culture.Secularism}; it must lie between 0 and 1.");
        }

        foreach (var belief in settlement.Beliefs)
        {
            if (belief is not null && (!IsShare(belief.Dependency) || !IsShare(belief.Resentment)))
            {
                errors.Add($"Settlement {settlement.Id} has a belief in god {belief.GodId} with dependency or resentment outside 0..1.");
            }
        }
    }

    /// <summary>Records every inconsistency of a god's conduct and traits.</summary>
    /// <param name="god">The god.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(GodState god, List<string> errors)
    {
        if (god.Conduct is not { } conduct || god.Traits is null)
        {
            errors.Add($"God {god.Id} has no conduct or traits.");
            return;
        }

        foreach (var weight in new[] { conduct.Help, conduct.Gifts, conduct.Harm, conduct.Cruelty, conduct.Vengeance, conduct.Upheaval, conduct.Deceit, conduct.Faithfulness, conduct.Aggression })
        {
            if (!float.IsFinite(weight) || weight < 0f)
            {
                errors.Add($"God {god.Id} has a conduct weight that is negative or invalid.");
                break;
            }
        }

        var seen = new HashSet<GodTrait>();
        foreach (var trait in god.Traits)
        {
            if (!Enum.IsDefined(trait) || !seen.Add(trait))
            {
                errors.Add($"God {god.Id} has an unknown trait or a trait twice.");
                break;
            }
        }
    }

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool IsShare(float value) => value is >= 0f and <= 1f;
}
