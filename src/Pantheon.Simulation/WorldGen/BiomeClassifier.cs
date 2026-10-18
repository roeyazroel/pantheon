using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World.Generation;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Classifies samples into biomes with the ordered, data-driven <see cref="BiomeRule"/> list:
/// the first matching rule wins.
/// </summary>
public sealed class BiomeClassifier
{
    private readonly BiomeRule[] _rules;

    /// <summary>Validates and captures the rule list.</summary>
    /// <param name="rules">Ordered rules. The last rule must be a catch-all, and each biome may appear only once.</param>
    /// <exception cref="ArgumentException">The rule list is empty, lacks a final catch-all, or repeats a biome.</exception>
    public BiomeClassifier(IReadOnlyList<BiomeRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Count == 0)
        {
            throw new ArgumentException("At least one biome rule is required.", nameof(rules));
        }

        if (!rules[^1].IsCatchAll())
        {
            throw new ArgumentException("The last biome rule must have no bounds so that every sample is classified.", nameof(rules));
        }

        var seen = new HashSet<Biome>();
        foreach (var rule in rules)
        {
            if (!seen.Add(rule.Biome))
            {
                throw new ArgumentException($"Biome '{rule.Biome}' has more than one rule.", nameof(rules));
            }
        }

        _rules = [.. rules];
    }

    /// <summary>Returns the biome of the first rule matching the sample.</summary>
    /// <param name="elevation">Height in metres (sea level = 0).</param>
    /// <param name="temperature">Normalised temperature.</param>
    /// <param name="moisture">Normalised moisture.</param>
    /// <returns>The classified biome.</returns>
    public Biome Classify(float elevation, float temperature, float moisture)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matches(elevation, temperature, moisture))
            {
                return rule.Biome;
            }
        }

        // Unreachable in practice: the constructor guarantees a final catch-all rule.
        return _rules[^1].Biome;
    }

    /// <summary>Returns the rule, and therefore the base traits, of a biome.</summary>
    /// <param name="biome">Biome to look up.</param>
    /// <returns>The biome's rule.</returns>
    /// <exception cref="KeyNotFoundException">No rule exists for the biome.</exception>
    public BiomeRule GetRule(Biome biome)
    {
        foreach (var rule in _rules)
        {
            if (rule.Biome == biome)
            {
                return rule;
            }
        }

        throw new KeyNotFoundException($"No biome rule defines '{biome}'.");
    }
}
