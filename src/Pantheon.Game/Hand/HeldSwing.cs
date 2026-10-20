using System;
using System.Numerics;

namespace Pantheon.Game.Hand;

/// <summary>
/// How what the Divine Hand holds swings as the hand moves (spec §55: "physics interactions should
/// feel tactile", ADR-057): a damped pendulum hung from the hand and driven by the hand's
/// acceleration. A quick move leaves the thing lagging behind; it swings past and settles. Measured in
/// hand sizes, so it swings alike at any zoom. Pure: the client steps it each frame and tilts the held
/// thing by it.
/// </summary>
public sealed class HeldSwing
{
    /// <summary>The pendulum's own frequency, in radians a second.</summary>
    public const float Frequency = 5.5f;

    /// <summary>How fast a swing dies away, a second.</summary>
    public const float Damping = 2.2f;

    /// <summary>The pendulum's length, in hand sizes: a longer one swings less for the same move.</summary>
    public const float Length = 1.6f;

    /// <summary>The most it tilts, in radians.</summary>
    public const float MostTilt = 1f;

    // The hand's acceleration is taken no further than this, in hand sizes a second squared: a jump of
    // the cursor is not a throw.
    private const float MostAcceleration = 400f;

    // The longest step the pendulum takes at once, in seconds, so a slow frame stays stable.
    private const float LongestStep = 1f / 120f;

    private Vector2? _last;
    private Vector2 _velocity;
    private Vector2 _rate;

    /// <summary>How far the held thing has swung from straight below the hand, in radians towards +X and +Z.</summary>
    public Vector2 Tilt { get; private set; }

    /// <summary>Moves the pendulum on as the hand moves.</summary>
    /// <param name="hand">The hand's place on the ground plane, in hand sizes.</param>
    /// <param name="seconds">Seconds since the last step.</param>
    public void Step(Vector2 hand, float seconds)
    {
        if (!(seconds > 0f))
        {
            return;
        }

        if (_last is not { } last)
        {
            _last = hand;
            return;
        }

        var velocity = (hand - last) / seconds;
        var acceleration = (velocity - _velocity) / seconds;
        if (acceleration.Length() > MostAcceleration)
        {
            acceleration = Vector2.Normalize(acceleration) * MostAcceleration;
        }

        (_last, _velocity) = (hand, velocity);
        var steps = (int)MathF.Ceiling(seconds / LongestStep);
        var h = seconds / steps;
        for (var i = 0; i < steps; i++)
        {
            // The pivot's acceleration pushes the bob the other way.
            var pull = (-Frequency * Frequency * Tilt) - (Damping * _rate) - (acceleration / Length);
            _rate += pull * h;
            Tilt += _rate * h;
            if (Tilt.Length() > MostTilt)
            {
                Tilt = Vector2.Normalize(Tilt) * MostTilt;
                _rate *= 0.5f;
            }
        }
    }

    /// <summary>Stops the swing, as when the hand takes something new.</summary>
    public void Reset() => (_last, _velocity, _rate, Tilt) = (null, Vector2.Zero, Vector2.Zero, Vector2.Zero);
}
