namespace Pantheon.Domain.Events;

/// <summary>A god held the victory share of the world's belief long enough to win (spec §53, §115).</summary>
public sealed record VictoryAchieved : WorldEvent
{
    /// <summary>The winning god.</summary>
    public required int GodId { get; init; }
}
