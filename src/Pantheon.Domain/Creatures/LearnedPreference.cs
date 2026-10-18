namespace Pantheon.Domain.Creatures;

/// <summary>
/// How much a Creature likes an action in a situation (spec §25), from 0 to 1. Rewards raise it
/// and punishments lower it, so the Creature comes to act as its god taught it.
/// </summary>
public sealed class LearnedPreference
{
    /// <summary>The situation.</summary>
    public required CreatureSituation Situation { get; init; }

    /// <summary>The action.</summary>
    public required CreatureAction Action { get; init; }

    /// <summary>The preference (0..1).</summary>
    public required float Weight { get; set; }
}
