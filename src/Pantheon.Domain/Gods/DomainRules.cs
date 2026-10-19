using System.Collections.Generic;

namespace Pantheon.Domain.Gods;

/// <summary>
/// Data-driven tuning of god domains (spec §12, ADR-021), loaded from <c>game/Data/domains.json</c>.
/// A god takes up domains as it rises: each one makes its miracles cheaper, opens its signature
/// miracle and grants a boon that changes how the god plays.
/// </summary>
public sealed record DomainRules
{
    /// <summary>Share of the cost a god pays for miracles of its own domains (0..1].</summary>
    public required float DomainCostFactor { get; init; }

    /// <summary>How many domains a god may hold from each rank, in rank order; the first is <see cref="DivineRank.MinorSpirit"/>'s.</summary>
    public required IReadOnlyList<DomainSlot> Slots { get; init; }

    /// <summary>The domains a god may choose, in the order the player sees them.</summary>
    public required IReadOnlyList<DomainDefinition> Domains { get; init; }

    /// <summary>The numbers behind each domain's boon.</summary>
    public required DomainBoons Boons { get; init; }
}

/// <summary>Domains a god may hold from a rank on.</summary>
public sealed record DomainSlot
{
    /// <summary>The rank.</summary>
    public required DivineRank Rank { get; init; }

    /// <summary>Domains a god of this rank or higher may hold.</summary>
    public required int Domains { get; init; }
}

/// <summary>One domain a god may choose.</summary>
public sealed record DomainDefinition
{
    /// <summary>The domain.</summary>
    public required GodDomain Domain { get; init; }

    /// <summary>What the domain stands for, for the player.</summary>
    public required string Description { get; init; }

    /// <summary>What its boon does, for the player.</summary>
    public required string Boon { get; init; }
}

/// <summary>
/// The numbers of the domains' boons (ADR-021). Each changes one rule of the world for the gods that
/// hold the domain, and for the settlements where such a god holds the majority.
/// </summary>
public sealed record DomainBoons
{
    /// <summary>Nature: the farmland of the god's settlements never has less blessing than this (0..1).</summary>
    public required float NatureBlessing { get; init; }

    /// <summary>Life: health gained per belief step by the god's settlements, closing that share of the gap to 1.</summary>
    public required float LifeHealthPerStep { get; init; }

    /// <summary>Storm: Faith and Fear the god's followers gain per belief step while a storm is over them.</summary>
    public required float StormAwePerStep { get; init; }

    /// <summary>Fire: share of a fire's strength put out per belief step on the farmland of the god's settlements.</summary>
    public required float FireQuenchPerStep { get; init; }

    /// <summary>War: extra share of damage the god's Creature deals in a fight.</summary>
    public required float WarCreatureDamage { get; init; }

    /// <summary>Death: Divine Power the god gains for each person its miracles kill.</summary>
    public required float DeathPowerPerDeath { get; init; }

    /// <summary>Prosperity: extra share of Divine Power the god's worshippers raise.</summary>
    public required float ProsperityPower { get; init; }

    /// <summary>Protection: share of the houses a disaster would destroy in the god's settlements that stand.</summary>
    public required float ProtectionResilience { get; init; }

    /// <summary>Knowledge: extra share of what the god's Creature learns from each reward and punishment.</summary>
    public required float KnowledgeLearning { get; init; }

    /// <summary>Trickery: extra share of the doubt the god's miracles plant about other gods.</summary>
    public required float TrickeryDoubt { get; init; }

    /// <summary>Chaos: extra magnitude of the earthquakes the god's miracles start.</summary>
    public required float ChaosMagnitude { get; init; }
}
