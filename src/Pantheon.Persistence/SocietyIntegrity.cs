using System;
using System.Collections.Generic;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;

namespace Pantheon.Persistence;

/// <summary>Structural checks on a deserialized settlement's society (part of <see cref="SaveIntegrity"/>, ADR-023).</summary>
internal static class SocietyIntegrity
{
    /// <summary>Records every inconsistency of a settlement's society.</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    public static void Validate(SettlementState settlement, HashSet<int> godIds, List<string> errors)
    {
        var society = settlement.Society;
        if (society is null || society.Technology is null || society.Buildings is null)
        {
            errors.Add($"Settlement {settlement.Id} has no society, technology or buildings.");
            return;
        }

        if (!IsShare(society.Safety) || !IsShare(society.Happiness) || !IsShare(society.Education) || !IsShare(society.Stability)
            || !float.IsFinite(society.Wealth) || society.Wealth < 0f || !float.IsFinite(society.ProjectProgress) || society.ProjectProgress < 0f)
        {
            errors.Add($"Settlement {settlement.Id} has a society value out of range: safety, happiness, education and stability lie in 0..1, wealth and work are not negative.");
        }

        foreach (var field in Enum.GetValues<TechnologyField>())
        {
            var level = society.Technology.Get(field);
            if (!float.IsFinite(level) || level < 0f)
            {
                errors.Add($"Settlement {settlement.Id} has {field} technology {level}; it must not be negative.");
            }
        }

        var kinds = new HashSet<(BuildingKind, int?)>();
        foreach (var building in society.Buildings)
        {
            if (building is null || !Enum.IsDefined(building.Kind) || !kinds.Add((building.Kind, building.GodId))
                || (building.Kind == BuildingKind.Temple) != (building.GodId is not null) || (building.GodId is { } god && !godIds.Contains(god)))
            {
                errors.Add($"Settlement {settlement.Id} has a building that is unknown, built twice, or a temple without a known god.");
            }
        }

        if (!Enum.IsDefined(society.Priority) || (society.PriorityGodId is { } setter && !godIds.Contains(setter)) || society.PrioritySinceTick < 0
            || society.LastFoundingTick < 0 || (society.Project is { } project && !Enum.IsDefined(project)))
        {
            errors.Add($"Settlement {settlement.Id} has an unknown priority or project, a priority set by a missing god, or a negative tick.");
        }
    }

    /// <summary>Whether a value lies in 0..1.</summary>
    private static bool IsShare(float value) => value is >= 0f and <= 1f;
}
