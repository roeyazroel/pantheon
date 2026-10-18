using System;
using System.Collections.Generic;
using Pantheon.Domain.Miracles;
using Pantheon.Simulation.Validation;

namespace Pantheon.Simulation.Miracles;

/// <summary>The validated miracles of <c>miracles.json</c>, in the order the player sees them, with lookup by id.</summary>
public sealed class MiracleBook
{
    private readonly Dictionary<string, MiracleDefinition> _byId;

    /// <summary>Validates a catalog and indexes it.</summary>
    /// <param name="catalog">Parsed catalog.</param>
    /// <exception cref="ArgumentException">A miracle is invalid; the message lists every problem.</exception>
    public MiracleBook(MiracleCatalog catalog)
    {
        Validate(catalog);
        Miracles = catalog.Miracles;
        _byId = new Dictionary<string, MiracleDefinition>(StringComparer.Ordinal);
        foreach (var miracle in catalog.Miracles)
        {
            _byId[miracle.Id] = miracle;
        }
    }

    /// <summary>The miracles, in catalog order.</summary>
    public IReadOnlyList<MiracleDefinition> Miracles { get; }

    /// <summary>Finds a miracle by id.</summary>
    /// <param name="id">Miracle id.</param>
    /// <returns>The miracle, or <c>null</c>.</returns>
    public MiracleDefinition? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Throws when the catalog is empty or a miracle is out of range.</summary>
    /// <param name="catalog">Catalog to check.</param>
    /// <exception cref="ArgumentException">The catalog is invalid; the message lists every problem.</exception>
    public static void Validate(MiracleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new RuleErrors();
        if (catalog.Miracles.Count == 0)
        {
            errors.Add("At least one miracle is required.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var miracle in catalog.Miracles)
        {
            if (string.IsNullOrWhiteSpace(miracle.Id) || !ids.Add(miracle.Id))
            {
                errors.Add($"Miracle id '{miracle.Id}' is empty or used more than once.");
            }

            ValidateMiracle(miracle, errors);
        }

        errors.ThrowIfAny("miracles", nameof(catalog));
    }

    /// <summary>Checks one miracle's numbers and effects.</summary>
    private static void ValidateMiracle(MiracleDefinition miracle, RuleErrors errors)
    {
        var prefix = miracle.Id + ".";
        if (string.IsNullOrWhiteSpace(miracle.Name))
        {
            errors.Add(prefix + "Name must not be empty.");
        }

        errors.RequireDefined(miracle.Domain, prefix + nameof(miracle.Domain));
        errors.RequireDefined(miracle.Scale, prefix + nameof(miracle.Scale));
        errors.RequireDefined(miracle.MinimumRank, prefix + nameof(miracle.MinimumRank));
        errors.RequireDefined(miracle.Gratitude, prefix + nameof(miracle.Gratitude));
        errors.RequireNonNegative(miracle.PowerCost, prefix + nameof(miracle.PowerCost));
        errors.RequireAtLeast(miracle.CooldownTicks, 0, prefix + nameof(miracle.CooldownTicks));
        errors.RequirePositive(miracle.Radius, prefix + nameof(miracle.Radius));
        errors.RequireShare(miracle.InfluenceRequirement, prefix + nameof(miracle.InfluenceRequirement));
        errors.RequireSignedShare(miracle.Witness.Faith, prefix + "Witness.Faith");
        errors.RequireSignedShare(miracle.Witness.Love, prefix + "Witness.Love");
        errors.RequireSignedShare(miracle.Witness.Fear, prefix + "Witness.Fear");
        errors.RequireSignedShare(miracle.Witness.Doubt, prefix + "Witness.Doubt");
        foreach (var effect in miracle.Effects)
        {
            errors.RequireDefined(effect.Kind, prefix + "Effects.Kind");
            if (effect.Kind is MiracleEffectKind.Heal or MiracleEffectKind.Bless or MiracleEffectKind.Rain or MiracleEffectKind.Ignite)
            {
                errors.RequireShare(effect.Amount, $"{prefix}Effects.{effect.Kind}.Amount");
            }
            else
            {
                errors.RequireNonNegative(effect.Amount, $"{prefix}Effects.{effect.Kind}.Amount");
            }
        }
    }
}
