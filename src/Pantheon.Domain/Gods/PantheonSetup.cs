using System.Collections.Generic;
using Pantheon.Domain.Belief;

namespace Pantheon.Domain.Gods;

/// <summary>
/// The gods of a new world, loaded from <c>game/Data/gods.json</c>. Each god is founded in its own
/// home settlement, in the order listed; the first is the best site.
/// </summary>
public sealed record PantheonSetup
{
    /// <summary>Divine Power each god starts with.</summary>
    public required float StartingDivinePower { get; init; }

    /// <summary>Belief of a god's home settlement at the start.</summary>
    public required BeliefLevels HomeBelief { get; init; }

    /// <summary>The gods, in order. At least one.</summary>
    public required IReadOnlyList<GodSlot> Gods { get; init; }

    /// <summary>Gods that join in larger worlds, in order (ADR-027); none in the default world.</summary>
    public IReadOnlyList<GodSlot> ExtraGods { get; init; } = [];
}

/// <summary>One god of a new world.</summary>
public sealed record GodSlot
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Player or AI.</summary>
    public required GodController Controller { get; init; }

    /// <summary>Name of the god's Creature.</summary>
    public required string CreatureName { get; init; }

    /// <summary>Species of the god's Creature, one of <c>creatures.json</c>'s.</summary>
    public required string CreatureSpecies { get; init; }

    /// <summary>The AI's personality; <see cref="GodPersonality.None"/> for a player.</summary>
    public required GodPersonality Personality { get; init; }
}
