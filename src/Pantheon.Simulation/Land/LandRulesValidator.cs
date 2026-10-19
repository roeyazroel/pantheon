using Pantheon.Domain.Land;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Land;

/// <summary>Checks <see cref="LandRules"/> (ADR-045) into the divine rules' errors.</summary>
internal static class LandRulesValidator
{
    /// <summary>Adds an error for every invalid land rule; rules that reshape nothing are not checked.</summary>
    /// <param name="errors">Errors so far.</param>
    /// <param name="rules">Land rules.</param>
    public static void Check(RuleErrors errors, LandRules rules)
    {
        if (!rules.Enabled)
        {
            return;
        }

        foreach (var (value, name) in new[]
        {
            (rules.IslandRadius, "Land.IslandRadius"), (rules.IslandPeak, "Land.IslandPeak"), (rules.GorgeLength, "Land.GorgeLength"),
            (rules.GorgeWidth, "Land.GorgeWidth"), (rules.MountainHeight, "Land.MountainHeight"), (rules.ChannelWidth, "Land.ChannelWidth"),
            (rules.DivertReach, "Land.DivertReach"), (rules.DamOffset, "Land.DamOffset"),
        })
        {
            errors.RequirePositive(value, name);
        }

        foreach (var (value, name) in new[]
        {
            (rules.PassDrop, "Land.PassDrop"), (rules.ChannelDepth, "Land.ChannelDepth"), (rules.DamHeight, "Land.DamHeight"),
            (rules.MinFall, "Land.MinFall"), (rules.MinChannel, "Land.MinChannel"),
        })
        {
            errors.RequireNonNegative(value, name);
        }
    }
}
