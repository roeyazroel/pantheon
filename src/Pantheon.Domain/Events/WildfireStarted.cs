using Pantheon.Domain.Disasters;

namespace Pantheon.Domain.Events;

/// <summary>A region caught fire by itself: lightning, a hearth, an earthquake or a miracle, not spreading flames.</summary>
public sealed record WildfireStarted : WorldEvent
{
    /// <summary>The region.</summary>
    public required int RegionId { get; init; }

    /// <summary>What lit it.</summary>
    public required FireCause Cause { get; init; }
}
