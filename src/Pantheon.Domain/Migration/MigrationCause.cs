namespace Pantheon.Domain.Migration;

/// <summary>Why people leave a settlement (spec §31, ADR-016).</summary>
public enum MigrationCause
{
    /// <summary>Their stores run short.</summary>
    Famine,

    /// <summary>They have no roof: ruins after a disaster, or too many people for the houses.</summary>
    Homelessness,

    /// <summary>Fire on their land or the ground shaking under them.</summary>
    Danger,

    /// <summary>Their village is contested by gods at war.</summary>
    War,

    /// <summary>They fear the god they follow.</summary>
    Fear,

    /// <summary>They have lost confidence in the god they follow.</summary>
    Doubt,

    /// <summary>They are unhappy (ADR-023).</summary>
    Unhappiness,

    /// <summary>Their settlement's order is breaking down (ADR-023).</summary>
    Unrest,

    /// <summary>A god's hand carried them away and set them down (ADR-030).</summary>
    Carried,
}
