using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Gods;

namespace Pantheon.Domain.Miracles;

/// <summary>A miracle, loaded from <c>game/Data/miracles.json</c> (spec §107).</summary>
public sealed record MiracleDefinition
{
    /// <summary>Unique id, used in saves and requests.</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>One-line description for the player.</summary>
    public required string Description { get; init; }

    /// <summary>Domain the miracle belongs to.</summary>
    public required GodDomain Domain { get; init; }

    /// <summary>Scale of the miracle.</summary>
    public required MiracleScale Scale { get; init; }

    /// <summary>Divine Power the miracle costs where the god has full influence.</summary>
    public required float PowerCost { get; init; }

    /// <summary>Ticks before the god can cast it again.</summary>
    public required int CooldownTicks { get; init; }

    /// <summary>Radius of the effect around the target, in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>Influence (0..1) the god needs at the target.</summary>
    public required float InfluenceRequirement { get; init; }

    /// <summary>Lowest rank that can cast it.</summary>
    public required DivineRank MinimumRank { get; init; }

    /// <summary>What it does.</summary>
    public required IReadOnlyList<MiracleEffect> Effects { get; init; }

    /// <summary>How it changes the belief of the people who see it, at the target.</summary>
    public required BeliefImpulse Witness { get; init; }

    /// <summary>The need it answers, which scales the Love it earns.</summary>
    public required MiracleNeed Gratitude { get; init; }
}

/// <summary>Contents of <c>miracles.json</c>.</summary>
public sealed record MiracleCatalog
{
    /// <summary>The miracles, in the order the player sees them.</summary>
    public required IReadOnlyList<MiracleDefinition> Miracles { get; init; }
}
