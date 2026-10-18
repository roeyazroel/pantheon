using Pantheon.Domain.World;

namespace Pantheon.Domain.Events;

/// <summary>An earthquake began.</summary>
public sealed record EarthquakeStruck : WorldEvent
{
    /// <summary>The earthquake.</summary>
    public required int QuakeId { get; init; }

    /// <summary>Its epicentre.</summary>
    public required GroundPosition Epicentre { get; init; }

    /// <summary>Its magnitude.</summary>
    public required float Magnitude { get; init; }

    /// <summary>The god whose miracle caused it, or <c>null</c> for a natural one.</summary>
    public required int? GodId { get; init; }
}
