using System;

namespace Pantheon.Game.Presentation;

/// <summary>The debris that flies when houses fall or a thrown thing strikes (spec §94, §55, ADR-048), from <c>settlement_view.json</c>. Presentation only.</summary>
public sealed record DebrisSettings
{
    /// <summary>Seconds a piece lasts, its fade included.</summary>
    public required float Lifetime { get; init; }

    /// <summary>Seconds a piece takes to fade at the end.</summary>
    public required float FadeSeconds { get; init; }

    /// <summary>The most pieces at once; the oldest go first.</summary>
    public required int MaxBodies { get; init; }

    /// <summary>Planks from a fallen house.</summary>
    public required int HousePlanks { get; init; }

    /// <summary>Stones from a fallen house.</summary>
    public required int HouseStones { get; init; }

    /// <summary>Thatch shards from a fallen house's roof.</summary>
    public required int HouseShards { get; init; }

    /// <summary>Stones a thrown thing or a falling star throws up where it strikes.</summary>
    public required int StrikeStones { get; init; }

    /// <summary>Outward speed of a piece, in metres per second, at its strongest.</summary>
    public required float Impulse { get; init; }

    /// <summary>Upward speed of a piece, in metres per second, at its strongest.</summary>
    public required float Lift { get; init; }

    /// <summary>Spin of a piece, in radians per second, at its strongest.</summary>
    public required float Spin { get; init; }

    /// <summary>How far from the camera, in metres, debris is thrown; beyond it nobody would see it.</summary>
    public required float Reach { get; init; }

    /// <summary>The side of the patch of ground a burst lands on, in metres.</summary>
    public required float GroundPatch { get; init; }

    /// <summary>Colour of planks.</summary>
    public required RgbColor Wood { get; init; }

    /// <summary>Colour of stones.</summary>
    public required RgbColor Stone { get; init; }

    /// <summary>Colour of thatch.</summary>
    public required RgbColor Thatch { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(Lifetime > 0f) || !(FadeSeconds >= 0f && FadeSeconds <= Lifetime) || MaxBodies < 1 || HousePlanks < 0 || HouseStones < 0 || HouseShards < 0 || StrikeStones < 0
            || !(Impulse >= 0f) || !(Lift >= 0f) || !(Spin >= 0f) || !(Reach > 0f) || !(GroundPatch >= 4f))
        {
            throw new ArgumentException("Debris needs a positive lifetime longer than its fade, at least one body, no negative counts or speeds, a positive reach and a ground patch of at least 4 m.");
        }
    }
}
