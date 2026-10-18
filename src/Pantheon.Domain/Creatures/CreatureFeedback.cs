namespace Pantheon.Domain.Creatures;

/// <summary>What the Creature's god made of one of its actions.</summary>
public enum CreatureFeedback
{
    /// <summary>The god said nothing.</summary>
    None,

    /// <summary>The god rewarded it.</summary>
    Rewarded,

    /// <summary>The god punished it.</summary>
    Punished,
}
