namespace Pantheon.Domain.Disasters;

/// <summary>
/// An earthquake under way (spec §19–20, ADR-014): it shakes the ground around its epicentre for a
/// few seconds, strongest at the centre and fading to nothing at its radius.
/// </summary>
public sealed class Earthquake
{
    /// <summary>Unique id, never reused within a world.</summary>
    public required int Id { get; init; }

    /// <summary>East-west position of the epicentre in metres.</summary>
    public required float X { get; init; }

    /// <summary>North-south position of the epicentre in metres.</summary>
    public required float Z { get; init; }

    /// <summary>Magnitude, for the player (for example 6.4).</summary>
    public required float Magnitude { get; init; }

    /// <summary>Shaking at the epicentre (0..1).</summary>
    public required float Intensity { get; init; }

    /// <summary>Distance in metres at which the shaking has faded to nothing.</summary>
    public required float Radius { get; init; }

    /// <summary>Tick at which the shaking stops.</summary>
    public required long EndsAtTick { get; init; }

    /// <summary>The god whose miracle caused it, or <c>null</c> for a natural earthquake.</summary>
    public required int? CausedByGodId { get; init; }
}
