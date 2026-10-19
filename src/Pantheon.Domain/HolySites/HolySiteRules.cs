using System.Collections.Generic;

namespace Pantheon.Domain.HolySites;

/// <summary>
/// Data-driven tuning of holy sites (spec §51, ADR-021), loaded from <c>game/Data/holy_sites.json</c>.
/// The step runs every <see cref="StepIntervalTicks"/> ticks; rates are per step.
/// </summary>
public sealed record HolySiteRules
{
    /// <summary>Ticks between holy site steps.</summary>
    public required int StepIntervalTicks { get; init; }

    /// <summary>Influence (0..1) a god needs at a site to claim it.</summary>
    public required float ClaimInfluence { get; init; }

    /// <summary>How far (0..1) a claimant's influence must exceed every other god's there.</summary>
    public required float ClaimLead { get; init; }

    /// <summary>Simulated seconds a claimant must hold the site to take it.</summary>
    public required float ClaimSeconds { get; init; }

    /// <summary>Regions between any two sites, at least, where the land has room.</summary>
    public required int MinimumSpacing { get; init; }

    /// <summary>The kinds of site the world has, one site each, in the order they are placed.</summary>
    public required IReadOnlyList<HolySiteDefinition> Sites { get; init; }
}

/// <summary>One kind of holy site and what keeping it gives.</summary>
public sealed record HolySiteDefinition
{
    /// <summary>The kind.</summary>
    public required HolySiteKind Kind { get; init; }

    /// <summary>Its name before the nearest settlement's, such as "The Sacred Peak".</summary>
    public required string Name { get; init; }

    /// <summary>What it gives, for the player.</summary>
    public required string Description { get; init; }

    /// <summary>Divine Power its keeper gains per step.</summary>
    public required float PowerPerStep { get; init; }

    /// <summary>What else it gives.</summary>
    public required HolySiteBoon Boon { get; init; }

    /// <summary>How much; the unit depends on <see cref="Boon"/>.</summary>
    public required float BoonAmount { get; init; }

    /// <summary>Distance in metres the boon reaches, where it concerns places near the site.</summary>
    public required float BoonReach { get; init; }
}
