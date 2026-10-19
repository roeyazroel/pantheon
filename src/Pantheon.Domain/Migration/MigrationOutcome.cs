namespace Pantheon.Domain.Migration;

/// <summary>How a group's journey ended (ADR-016).</summary>
public enum MigrationOutcome
{
    /// <summary>A settlement took them in as they were.</summary>
    Welcomed,

    /// <summary>A settlement took them in, and its god made many of them convert.</summary>
    Converted,

    /// <summary>Turned away everywhere, they went home.</summary>
    Returned,

    /// <summary>They died on the road.</summary>
    Perished,
}
