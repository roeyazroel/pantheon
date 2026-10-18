namespace Pantheon.Domain.Events;

/// <summary>Most of a settlement's people came to follow a god, or stopped following it.</summary>
public sealed record FollowingChanged : WorldEvent
{
    /// <summary>The settlement.</summary>
    public required int SettlementId { get; init; }

    /// <summary>The god.</summary>
    public required int GodId { get; init; }

    /// <summary><c>true</c> when the god gained the majority, <c>false</c> when it lost it.</summary>
    public required bool Gained { get; init; }
}
