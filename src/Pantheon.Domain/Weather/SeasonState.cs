namespace Pantheon.Domain.Weather;

/// <summary>
/// A world's seasons (spec §17, ADR-028), present only when the world was made with them. The
/// season follows from the tick: the year began at <see cref="OriginTick"/> with spring.
/// </summary>
public sealed class SeasonState
{
    /// <summary>The tick at which a year began with spring; earlier than the world's start to begin in another season.</summary>
    public required long OriginTick { get; set; }
}
