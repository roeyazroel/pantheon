namespace Pantheon.Domain.Gods;

/// <summary>An AI god's strategic personality (spec §47). Players have none.</summary>
public enum GodPersonality
{
    /// <summary>No AI personality: a player's god.</summary>
    None,

    /// <summary>Protects its followers and wins people with kindness.</summary>
    Guardian,

    /// <summary>Rules through fear: smites, intimidates and teaches its Creature cruelty.</summary>
    Tyrant,

    /// <summary>Strikes where others are weak or suffering.</summary>
    Opportunist,

    /// <summary>Converts above all else.</summary>
    Zealot,

    /// <summary>Blesses the land and keeps its people fed.</summary>
    NatureKeeper,
}
