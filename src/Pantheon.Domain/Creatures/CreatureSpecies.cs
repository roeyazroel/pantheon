namespace Pantheon.Domain.Creatures;

/// <summary>
/// A kind of Creature (spec §130's "more Creatures", ADR-021), part of <c>game/Data/creatures.json</c>.
/// Its factors scale the shared rules; 1 is the plain Cow.
/// </summary>
public sealed record CreatureSpecies
{
    /// <summary>Its name, which is also its id in saves.</summary>
    public required string Name { get; init; }

    /// <summary>What sets it apart, for the player.</summary>
    public required string Description { get; init; }

    /// <summary>How much harm it takes before it falls: damage and starvation are divided by this.</summary>
    public required float Toughness { get; init; }

    /// <summary>Factor on its strength.</summary>
    public required float Strength { get; init; }

    /// <summary>Factor on its walking speed.</summary>
    public required float Speed { get; init; }

    /// <summary>Factor on what it learns from its god's rewards and punishments.</summary>
    public required float Learning { get; init; }

    /// <summary>Added to the kindness it is born with (-1..1).</summary>
    public required float Kindness { get; init; }

    /// <summary>Added to the aggression it is born with (-1..1).</summary>
    public required float Aggression { get; init; }

    /// <summary>Added to the courage it is born with (-1..1, ADR-037).</summary>
    public float Courage { get; init; }

    /// <summary>Added to the protectiveness it is born with (-1..1, ADR-037).</summary>
    public float Protectiveness { get; init; }

    /// <summary>Added to the jealousy it is born with (-1..1, ADR-037).</summary>
    public float Jealousy { get; init; }

    /// <summary>Added to the playfulness it is born with (-1..1, ADR-037).</summary>
    public float Playfulness { get; init; }
}
