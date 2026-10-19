using System;
using System.Collections.Generic;
using Pantheon.Domain.Society;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Society;

/// <summary>Validates data-driven <see cref="SocietyRules"/>.</summary>
public static class SocietyRulesValidator
{
    /// <summary>Throws when any rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(SocietyRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        errors.RequireAtLeast(rules.StepIntervalTicks, 1, nameof(rules.StepIntervalTicks));
        errors.RequireShare(rules.DriftPerSecond, nameof(rules.DriftPerSecond));
        errors.RequireNonNegative(rules.WarUnsafety, nameof(rules.WarUnsafety));
        errors.RequireShare(rules.DefenceSafety, nameof(rules.DefenceSafety));
        foreach (var (name, weight) in new[]
        {
            ("Happiness.Food", rules.Happiness.Food), ("Happiness.Health", rules.Happiness.Health), ("Happiness.Water", rules.Happiness.Water),
            ("Happiness.Safety", rules.Happiness.Safety), ("Happiness.Love", rules.Happiness.Love), ("Happiness.Fear", rules.Happiness.Fear),
            ("Happiness.Homeless", rules.Happiness.Homeless), ("Stability.Happiness", rules.Stability.Happiness), ("Stability.Unity", rules.Stability.Unity),
            ("Stability.Peace", rules.Stability.Peace), ("Stability.Schism", rules.Stability.Schism),
        })
        {
            errors.RequireNonNegative(weight, name);
        }

        errors.RequireNonNegative(rules.SurplusWealth, nameof(rules.SurplusWealth));
        errors.RequireShare(rules.FullStores, nameof(rules.FullStores));
        errors.RequireNonNegative(rules.TradePerNeighbour, nameof(rules.TradePerNeighbour));
        errors.RequireAtLeast(rules.TradeRange, 0, nameof(rules.TradeRange));
        errors.RequireShare(rules.WealthUsePerSecond, nameof(rules.WealthUsePerSecond));
        errors.RequireShare(rules.EducationPerSecond, nameof(rules.EducationPerSecond));
        errors.RequirePositive(rules.LearnedWealth, nameof(rules.LearnedWealth));
        errors.RequireShare(rules.EducationLossPerSecond, nameof(rules.EducationLossPerSecond));
        errors.RequireNonNegative(rules.TechnologyPerSecond, nameof(rules.TechnologyPerSecond));
        errors.RequireNonNegative(rules.FocusFactor, nameof(rules.FocusFactor));
        errors.RequireShare(rules.DiffusionPerSecond, nameof(rules.DiffusionPerSecond));
        errors.RequirePositive(rules.MaxTechnology, nameof(rules.MaxTechnology));
        errors.RequireNonNegative(rules.ProjectWorkPerWorker, nameof(rules.ProjectWorkPerWorker));
        errors.RequireShare(rules.PriorityShare, nameof(rules.PriorityShare));
        errors.RequireNonNegative(rules.PriorityCooldownSeconds, nameof(rules.PriorityCooldownSeconds));
        var kinds = new HashSet<BuildingKind>();
        foreach (var building in rules.Buildings ?? [])
        {
            errors.RequireDefined(building.Kind, "Buildings.Kind");
            errors.RequireDefined(building.Priority, "Buildings.Priority");
            errors.RequireDefined(building.Field, "Buildings.Field");
            errors.RequirePositive(building.Work, $"Buildings.{building.Kind}.Work");
            errors.RequireNonNegative(building.Wealth, $"Buildings.{building.Kind}.Wealth");
            if (!kinds.Add(building.Kind))
            {
                errors.Add($"Building {building.Kind} is listed twice.");
            }
        }

        var expansion = rules.Expansion;
        errors.RequireAtLeast(expansion.MinPopulation, 2, "Expansion.MinPopulation");
        errors.RequireAtLeast(expansion.Settlers, 1, "Expansion.Settlers");
        if (expansion.Settlers >= expansion.MinPopulation)
        {
            errors.Add("Expansion.Settlers must be fewer than Expansion.MinPopulation.");
        }

        errors.RequireNonNegative(expansion.FoodPerSettler, "Expansion.FoodPerSettler");
        errors.RequireAtLeast(expansion.Range, 1, "Expansion.Range");
        errors.RequireAtLeast(expansion.Spacing, 1, "Expansion.Spacing");
        errors.RequireNonNegative(expansion.CooldownSeconds, "Expansion.CooldownSeconds");
        errors.ThrowIfAny("society rules", nameof(rules));
    }
}
