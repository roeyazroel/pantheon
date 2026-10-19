using System.Collections.Generic;
using Pantheon.Domain.Belief;

namespace Pantheon.Domain.Hand;

/// <summary>What a god's Divine Hand can hold (spec §55, ADR-030).</summary>
public enum HeldKind
{
    /// <summary>Villagers lifted from a settlement.</summary>
    Villagers,

    /// <summary>A rock torn from the land.</summary>
    Rock,

    /// <summary>A tree pulled from a wood.</summary>
    Tree,

    /// <summary>The god's own Creature.</summary>
    Creature,

    /// <summary>An offering from a village's stores: food, wood and stone (ADR-039).</summary>
    Offering,
}

/// <summary>What a god's Divine Hand holds now (spec §55, ADR-030).</summary>
public sealed class HeldObject
{
    /// <summary>What it is.</summary>
    public required HeldKind Kind { get; init; }

    /// <summary>How many people, for villagers; 1 for anything else.</summary>
    public required int Amount { get; init; }

    /// <summary>The settlement villagers or an offering were taken from; <c>null</c> for anything else.</summary>
    public int? SettlementId { get; init; }

    /// <summary>Whether villagers were lifted out of danger (a fire, a flood, shaking ground, a burning mountain).</summary>
    public bool FromDanger { get; init; }

    /// <summary>The villagers' health (0..1).</summary>
    public float Health { get; init; } = 1f;

    /// <summary>The villagers' beliefs, as their settlement held them when they were lifted.</summary>
    public List<GodBelief> Beliefs { get; init; } = [];

    /// <summary>The tick it was picked up.</summary>
    public required long Tick { get; init; }

    /// <summary>The food an offering holds (ADR-039).</summary>
    public float Food { get; init; }

    /// <summary>The wood an offering holds (ADR-039).</summary>
    public float Wood { get; init; }

    /// <summary>The stone an offering holds (ADR-039).</summary>
    public float Stone { get; init; }
}
