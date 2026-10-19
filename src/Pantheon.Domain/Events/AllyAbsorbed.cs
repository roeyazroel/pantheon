namespace Pantheon.Domain.Events;

/// <summary>A god broke its alliance and took a share of its ally's followers (spec §54, ADR-025).</summary>
public sealed record AllyAbsorbed : WorldEvent
{
    /// <summary>The god that absorbed.</summary>
    public required int GodId { get; init; }

    /// <summary>The ally absorbed.</summary>
    public required int AllyGodId { get; init; }

    /// <summary>People who turned.</summary>
    public required int People { get; init; }
}
