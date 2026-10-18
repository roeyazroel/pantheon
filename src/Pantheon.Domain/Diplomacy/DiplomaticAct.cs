namespace Pantheon.Domain.Diplomacy;

/// <summary>Something one god did that another god remembers (spec §40–41, §113: "AI remembers betrayal").</summary>
public enum DiplomaticAct
{
    /// <summary>It helped the remembering god's followers in need.</summary>
    Helped,

    /// <summary>It courted the remembering god's followers with miracles they did not need.</summary>
    Courted,

    /// <summary>It struck the remembering god's followers.</summary>
    Harmed,

    /// <summary>It won over a settlement that followed the remembering god.</summary>
    Converted,

    /// <summary>Its Creature attacked the remembering god's followers.</summary>
    CreatureAttacked,

    /// <summary>It broke an alliance with the remembering god.</summary>
    Betrayed,

    /// <summary>It declared war on the remembering god.</summary>
    DeclaredWar,

    /// <summary>It made an alliance with the remembering god.</summary>
    Allied,

    /// <summary>It made peace with the remembering god.</summary>
    MadePeace,
}
