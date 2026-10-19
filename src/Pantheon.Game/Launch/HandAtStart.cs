namespace Pantheon.Game.Launch;

/// <summary>What the player's hand lifts at the start, for captures (<c>--hand</c>, ADR-030).</summary>
public enum HandAtStart
{
    /// <summary>Villagers of the focused settlement.</summary>
    Villagers,

    /// <summary>The player's own Creature.</summary>
    Creature,

    /// <summary>An offering from the focused settlement's stores (ADR-039).</summary>
    Offering,
}
