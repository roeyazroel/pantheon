namespace Pantheon.Domain.Gods;

/// <summary>A god's standing (spec §13). Higher ranks unlock more powerful miracles; a god never loses a rank.</summary>
public enum DivineRank
{
    /// <summary>The starting rank.</summary>
    MinorSpirit,

    /// <summary>Second rank.</summary>
    LesserGod,

    /// <summary>Third rank.</summary>
    God,

    /// <summary>Fourth rank.</summary>
    GreaterGod,

    /// <summary>Fifth rank.</summary>
    HighGod,

    /// <summary>The highest rank.</summary>
    Ascendant,
}
