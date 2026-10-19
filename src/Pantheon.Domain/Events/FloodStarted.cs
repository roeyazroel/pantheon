namespace Pantheon.Domain.Events;

/// <summary>A river overflowed its banks, or a god sent the water over the land (ADR-022).</summary>
public sealed record FloodStarted : WorldEvent
{
    /// <summary>The river that overflowed, if a river did.</summary>
    public required int? RiverId { get; init; }

    /// <summary>The god whose miracle sent the water, if one did.</summary>
    public required int? GodId { get; init; }

    /// <summary>The region where the flood is deepest.</summary>
    public required int RegionId { get; init; }
}
