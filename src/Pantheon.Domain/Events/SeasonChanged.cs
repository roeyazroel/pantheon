namespace Pantheon.Domain.Events;

/// <summary>A new season began (spec §17, ADR-028).</summary>
public sealed record SeasonChanged : WorldEvent
{
    /// <summary>The season that began.</summary>
    public required Weather.Season Season { get; init; }

    /// <summary>The year it belongs to, counting from 1.</summary>
    public required int Year { get; init; }
}
