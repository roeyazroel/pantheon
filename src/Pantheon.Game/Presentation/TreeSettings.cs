using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Ecology;

namespace Pantheon.Game.Presentation;

/// <summary>A species' colours.</summary>
public sealed record TreeColors
{
    /// <summary>Its trunk.</summary>
    public required RgbColor Trunk { get; init; }

    /// <summary>Its leaves, before each tree's own shade.</summary>
    public required RgbColor Leaves { get; init; }
}

/// <summary>The trees (spec §61, §93, ADR-053), from <c>settlement_view.json</c>: the species each biome grows, their colours and the wind. Presentation only.</summary>
public sealed record TreeSettings
{
    /// <summary>For each biome, its species and their shares.</summary>
    public required IReadOnlyDictionary<Biome, IReadOnlyDictionary<TreeKind, float>> Species { get; init; }

    /// <summary>Each species' colours.</summary>
    public required IReadOnlyDictionary<TreeKind, TreeColors> Colors { get; init; }

    /// <summary>How far a tree's top sways in a light breeze, in metres.</summary>
    public required float Sway { get; init; }

    /// <summary>How much more it sways in the strongest wind.</summary>
    public required float StormSway { get; init; }

    /// <summary>The species a tree on a biome is, by a unit value.</summary>
    /// <param name="biome">The biome.</param>
    /// <param name="unit">A value in 0..1.</param>
    /// <returns>The species.</returns>
    public TreeKind Pick(Biome biome, float unit)
    {
        var shares = Species.TryGetValue(biome, out var found) ? found : Species[Biome.Grassland];
        var total = shares.Values.Sum();
        var at = unit * total;
        foreach (var (kind, share) in shares.OrderBy(pair => pair.Key))
        {
            if ((at -= share) < 0f)
            {
                return kind;
            }
        }

        return shares.Keys.Max();
    }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!Species.ContainsKey(Biome.Grassland) || Species.Values.Any(shares => shares.Count == 0 || shares.Values.Any(share => !(share >= 0f)) || !(shares.Values.Sum() > 0f)))
        {
            throw new ArgumentException("Trees.Species needs Grassland, and every biome listed needs species with shares of at least 0 that add up to more than 0.");
        }

        if (Enum.GetValues<TreeKind>().Any(kind => !Colors.ContainsKey(kind)) || !(Sway >= 0f) || !(StormSway >= 0f))
        {
            throw new ArgumentException("Trees.Colors needs every species, and Sway and StormSway must not be negative.");
        }
    }
}
