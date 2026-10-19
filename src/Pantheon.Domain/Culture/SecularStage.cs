namespace Pantheon.Domain.Culture;

/// <summary>How far a settlement has turned from the gods (spec §35, ADR-024).</summary>
public enum SecularStage
{
    /// <summary>It believes, or is open to belief.</summary>
    Devout,

    /// <summary>It doubts that gods matter.</summary>
    Agnostic,

    /// <summary>It believes in no god.</summary>
    Atheist,

    /// <summary>It turns its people against every god, and its reason spreads.</summary>
    AntiDivine,
}
