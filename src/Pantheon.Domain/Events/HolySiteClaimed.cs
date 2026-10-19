namespace Pantheon.Domain.Events;

/// <summary>A god took a holy site (spec §51, ADR-021).</summary>
public sealed record HolySiteClaimed : WorldEvent
{
    /// <summary>The site.</summary>
    public required int SiteId { get; init; }

    /// <summary>The god that keeps it now.</summary>
    public required int GodId { get; init; }

    /// <summary>The god that kept it before, if any.</summary>
    public required int? PreviousGodId { get; init; }
}
