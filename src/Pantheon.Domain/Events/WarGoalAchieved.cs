using Pantheon.Domain.Conflict;

namespace Pantheon.Domain.Events;

/// <summary>A god at war achieved its goal and may dictate peace (spec §44, ADR-026).</summary>
public sealed record WarGoalAchieved : WorldEvent
{
    /// <summary>The god that achieved it.</summary>
    public required int GodId { get; init; }

    /// <summary>Its enemy.</summary>
    public required int EnemyGodId { get; init; }

    /// <summary>The goal.</summary>
    public required WarGoal Goal { get; init; }
}
