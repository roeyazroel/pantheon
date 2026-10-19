using Pantheon.Domain.World;

namespace Pantheon.Domain.Hand;

/// <summary>What a god does with its Divine Hand (spec §55, ADR-030).</summary>
public enum HandAction
{
    /// <summary>Picks up what lies at the point: villagers, the god's Creature, a tree or a rock.</summary>
    PickUp,

    /// <summary>Sets down what the hand holds, gently, at the point.</summary>
    SetDown,

    /// <summary>Throws what the hand holds at the point.</summary>
    Throw,
}

/// <summary>Why the hand could not do what was asked (spec §73).</summary>
public enum HandRejection
{
    /// <summary>It was done.</summary>
    None,

    /// <summary>The god does not exist.</summary>
    UnknownGod,

    /// <summary>The god's influence does not reach the point.</summary>
    NoInfluence,

    /// <summary>The hand already holds something.</summary>
    HandFull,

    /// <summary>The hand holds nothing.</summary>
    HandEmpty,

    /// <summary>Nothing the hand can lift lies at the point.</summary>
    NothingThere,

    /// <summary>The point is off the island.</summary>
    OffTheWorld,

    /// <summary>Gods have no hands in this world's rules.</summary>
    NoHand,

    /// <summary>The Creature cannot be set down in the sea.</summary>
    NotOnLand,

    /// <summary>The Creature is in the middle of a fight.</summary>
    CreatureFighting,
}

/// <summary>A god's use of its Divine Hand at a point.</summary>
/// <param name="GodId">The god.</param>
/// <param name="Action">What it does.</param>
/// <param name="Target">Where.</param>
public sealed record HandCommand(int GodId, HandAction Action, GroundPosition Target);
