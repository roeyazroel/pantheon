namespace Pantheon.Domain.Events;

/// <summary>A god began to take a holy site that another keeps (spec §51, ADR-021).</summary>
public sealed record HolySiteContested : WorldEvent
{
    /// <summary>The site.</summary>
    public required int SiteId { get; init; }

    /// <summary>The god taking it.</summary>
    public required int GodId { get; init; }

    /// <summary>The god that keeps it.</summary>
    public required int KeeperGodId { get; init; }
}
