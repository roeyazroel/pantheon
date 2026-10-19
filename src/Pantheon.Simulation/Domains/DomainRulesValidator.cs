using System;
using System.Collections.Generic;
using Pantheon.Domain.Gods;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Domains;

/// <summary>Validates data-driven <see cref="DomainRules"/> (ADR-021).</summary>
public static class DomainRulesValidator
{
    /// <summary>Throws when a rule is out of range.</summary>
    /// <param name="rules">Rules to check.</param>
    /// <exception cref="ArgumentException">One or more rules are invalid; the message lists them all.</exception>
    public static void Validate(DomainRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var errors = new RuleErrors();
        if (!(rules.DomainCostFactor is > 0f and <= 1f))
        {
            errors.Add($"DomainCostFactor must lie in (0, 1]; it is {rules.DomainCostFactor}.");
        }

        ValidateSlots(rules.Slots, errors);
        var domains = new HashSet<GodDomain>();
        foreach (var definition in rules.Domains ?? [])
        {
            errors.RequireDefined(definition.Domain, "Domains.Domain");
            if (!domains.Add(definition.Domain) || string.IsNullOrWhiteSpace(definition.Description) || string.IsNullOrWhiteSpace(definition.Boon))
            {
                errors.Add($"Domain {definition.Domain} is listed twice or lacks a description or boon.");
            }
        }

        if (domains.Count == 0)
        {
            errors.Add("At least one domain is required.");
        }

        var boons = rules.Boons;
        errors.RequireShare(boons.NatureBlessing, nameof(boons.NatureBlessing));
        errors.RequireShare(boons.LifeHealthPerStep, nameof(boons.LifeHealthPerStep));
        errors.RequireShare(boons.StormAwePerStep, nameof(boons.StormAwePerStep));
        errors.RequireShare(boons.FireQuenchPerStep, nameof(boons.FireQuenchPerStep));
        errors.RequireNonNegative(boons.WarCreatureDamage, nameof(boons.WarCreatureDamage));
        errors.RequireNonNegative(boons.DeathPowerPerDeath, nameof(boons.DeathPowerPerDeath));
        errors.RequireNonNegative(boons.ProsperityPower, nameof(boons.ProsperityPower));
        errors.RequireShare(boons.ProtectionResilience, nameof(boons.ProtectionResilience));
        errors.RequireNonNegative(boons.KnowledgeLearning, nameof(boons.KnowledgeLearning));
        errors.RequireNonNegative(boons.TrickeryDoubt, nameof(boons.TrickeryDoubt));
        errors.RequireNonNegative(boons.ChaosMagnitude, nameof(boons.ChaosMagnitude));
        errors.ThrowIfAny("domain rules", nameof(rules));
    }

    /// <summary>Slots start at the lowest rank, rise in rank and never offer fewer domains than before.</summary>
    private static void ValidateSlots(IReadOnlyList<DomainSlot>? slots, RuleErrors errors)
    {
        if (slots is null || slots.Count == 0 || slots[0].Rank != DivineRank.MinorSpirit)
        {
            errors.Add("Slots must start at MinorSpirit.");
            return;
        }

        for (var i = 0; i < slots.Count; i++)
        {
            errors.RequireDefined(slots[i].Rank, "Slots.Rank");
            errors.RequireAtLeast(slots[i].Domains, 1, "Slots.Domains");
            if (i > 0 && (slots[i].Rank <= slots[i - 1].Rank || slots[i].Domains < slots[i - 1].Domains))
            {
                errors.Add("Slots must rise in rank and never offer fewer domains.");
            }
        }
    }
}
