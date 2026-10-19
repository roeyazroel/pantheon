namespace Pantheon.Domain.Culture;

/// <summary>How the world comes to see a god, from what it does rather than what it chose (spec §11, ADR-024).</summary>
public enum GodTrait
{
    /// <summary>It helps people in need far more than it harms.</summary>
    Compassionate,

    /// <summary>It harms people, its own among them.</summary>
    Cruel,

    /// <summary>It strikes those who wronged it.</summary>
    Vengeful,

    /// <summary>It makes peace and keeps faith.</summary>
    Merciful,

    /// <summary>It gives freely.</summary>
    Generous,

    /// <summary>It breaks its word and sows discord.</summary>
    Deceptive,

    /// <summary>It keeps its word.</summary>
    Honest,

    /// <summary>It sets the land shaking and burning.</summary>
    Chaotic,

    /// <summary>It goes to war.</summary>
    Ambitious,
}
