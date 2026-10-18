using Pantheon.Domain.World;

namespace Pantheon.Domain.Creatures;

/// <summary>What a god can tell its Creature.</summary>
public enum CreatureCommandKind
{
    /// <summary>Reward its latest action.</summary>
    Reward,

    /// <summary>Punish its latest action.</summary>
    Punish,

    /// <summary>Go to a place and wait there.</summary>
    GoTo,
}

/// <summary>A god's intent towards its Creature, checked by the authority like a miracle (spec §73).</summary>
/// <param name="GodId">The god.</param>
/// <param name="CreatureId">The Creature.</param>
/// <param name="Kind">Reward, punish or go to.</param>
/// <param name="Target">Where to go, for <see cref="CreatureCommandKind.GoTo"/>.</param>
public sealed record CreatureCommand(int GodId, int CreatureId, CreatureCommandKind Kind, GroundPosition Target = default);
