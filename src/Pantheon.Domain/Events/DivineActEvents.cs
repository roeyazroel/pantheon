using Pantheon.Domain.Creatures;
using Pantheon.Domain.World;

namespace Pantheon.Domain.Events;

/// <summary>Which Divine Act was done (spec §14, ADR-030).</summary>
public enum DivineActKind
{
    /// <summary>The land was hallowed: its blessing never fades.</summary>
    Hallowing,

    /// <summary>The volcano was woken.</summary>
    Awakening,

    /// <summary>Every disaster on the island was stilled.</summary>
    Stillness,
}

/// <summary>A god did a Divine Act, which the world remembers (spec §14, ADR-030).</summary>
public sealed record DivineActDone : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The act.</summary>
    public required DivineActKind Act { get; init; }

    /// <summary>Where it was done: the target, or the volcano's crater.</summary>
    public required GroundPosition Position { get; init; }
}

/// <summary>A Creature took up a calling as it reached a level (spec §24, ADR-030).</summary>
public sealed record CreatureSpecialised : WorldEvent
{
    /// <summary>The Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>Its god.</summary>
    public required int GodId { get; init; }

    /// <summary>The calling it took up.</summary>
    public required CreatureSpecialisation Specialisation { get; init; }
}
