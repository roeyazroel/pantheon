namespace Pantheon.Domain.Creatures;

/// <summary>
/// A Creature's basic personality (spec §26, §111), each trait from 0 to 1. Traits start from the
/// world seed and drift with the god's rewards and punishments.
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
}
