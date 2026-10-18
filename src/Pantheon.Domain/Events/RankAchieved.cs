using Pantheon.Domain.Gods;

namespace Pantheon.Domain.Events;

/// <summary>A god reached a new rank.</summary>
public sealed record RankAchieved : WorldEvent
{
    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary>The new rank.</summary>
    public required DivineRank Rank { get; init; }
}
