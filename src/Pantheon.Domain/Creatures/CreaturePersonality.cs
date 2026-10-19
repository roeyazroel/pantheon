namespace Pantheon.Domain.Creatures;

/// <summary>
/// A Creature's personality (spec §26, §111), each trait from 0 to 1. Traits start from the world
/// seed and the species, and evolve by the god's rewards and punishments, by victories and defeats,
/// and by the Creatures it plays with and envies (ADR-037). Loyalty, its loyal or independent side,
/// is kept on the Creature.
/// </summary>
public sealed class CreaturePersonality
{
    /// <summary>How much it cares for others.</summary>
    public required float Kindness { get; set; }

    /// <summary>How readily it hurts others.</summary>
    public required float Aggression { get; set; }

    /// <summary>How much it would rather rest.</summary>
    public required float Laziness { get; set; }

    /// <summary>How much it likes to explore.</summary>
    public required float Curiosity { get; set; }

    /// <summary>How bravely it faces danger: high is brave, low cowardly (ADR-037).</summary>
    public float Courage { get; set; } = 0.5f;

    /// <summary>How much it guards its god's people (ADR-037).</summary>
    public float Protectiveness { get; set; } = 0.5f;

    /// <summary>How much it envies Creatures stronger than itself (ADR-037).</summary>
    public float Jealousy { get; set; } = 0.5f;

    /// <summary>How much it loves to play (ADR-037).</summary>
    public float Playfulness { get; set; } = 0.5f;
}
