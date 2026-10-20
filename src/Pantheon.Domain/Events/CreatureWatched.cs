namespace Pantheon.Domain.Events;

/// <summary>How a god answered another god's Creature in the lands it leads (ADR-070).</summary>
public enum WatchAnswer
{
    /// <summary>A friend's: its Creature comes to greet the visitor.</summary>
    Welcome,

    /// <summary>A stranger's: its Creature comes to watch the visitor, and the god holds the trespass against its god.</summary>
    Watch,

    /// <summary>A foe's: its Creature is set on the visitor to drive it out.</summary>
    Repel,

    /// <summary>A player's god does not answer by itself: it is told, and chooses.</summary>
    Unanswered,
}

/// <summary>A god noticed another god's Creature near a village it leads, and answered (ADR-070).</summary>
public sealed record CreatureWatched : WorldEvent
{
    /// <summary>The god whose land it is.</summary>
    public required int GodId { get; init; }

    /// <summary>The visitor's god.</summary>
    public required int VisitorGodId { get; init; }

    /// <summary>The visiting Creature.</summary>
    public required int CreatureId { get; init; }

    /// <summary>The village it came to.</summary>
    public required int SettlementId { get; init; }

    /// <summary>How the god answered.</summary>
    public required WatchAnswer Answer { get; init; }

    /// <summary>The god's own Creature, when it sent it; <c>null</c> when it had none fit to send.</summary>
    public int? GuardId { get; init; }
}
