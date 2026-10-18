using System;

namespace Pantheon.Game;

/// <summary>
/// Fixed-step clock that turns variable render-frame time into whole simulation ticks. The
/// simulation therefore advances identically at any frame rate. A per-update cap stops a long
/// frame from triggering a catch-up spiral; ticks beyond the cap are dropped and counted.
/// </summary>
public sealed class SimulationScheduler
{
    /// <summary>Simulation rate at time scale 1.</summary>
    public const double DefaultTicksPerSecond = Pantheon.Domain.World.SimulationTime.TicksPerSecond;

    /// <summary>Tick cap per update. It covers time scale 16 down to about 5 frames per second.</summary>
    public const int DefaultMaxTicksPerUpdate = 32;

    /// <summary>
    /// Tolerance, in simulated seconds, for deciding that a tick is due. Without it, binary
    /// rounding of frame times (0.25 + 0.05 is just below 0.3) would push some ticks a frame late.
    /// </summary>
    private const double DueTolerance = 1e-9;

    private double _timeScale = 1.0;
    private double _accumulator;

    /// <summary>Creates a scheduler.</summary>
    /// <param name="ticksPerSecond">Ticks per second of simulated time; must be positive.</param>
    /// <param name="maxTicksPerUpdate">Maximum ticks run by one <see cref="Advance"/> call; must be positive.</param>
    public SimulationScheduler(double ticksPerSecond = DefaultTicksPerSecond, int maxTicksPerUpdate = DefaultMaxTicksPerUpdate)
    {
        if (!(ticksPerSecond > 0.0) || double.IsInfinity(ticksPerSecond))
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Tick rate must be positive and finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTicksPerUpdate);
        TicksPerSecond = ticksPerSecond;
        TickDuration = 1.0 / ticksPerSecond;
        MaxTicksPerUpdate = maxTicksPerUpdate;
    }

    /// <summary>Ticks per second of simulated time.</summary>
    public double TicksPerSecond { get; }

    /// <summary>Simulated seconds per tick.</summary>
    public double TickDuration { get; }

    /// <summary>Maximum ticks run by one <see cref="Advance"/> call.</summary>
    public int MaxTicksPerUpdate { get; }

    /// <summary>When paused, <see cref="Advance"/> runs no ticks and accumulates no time.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Multiplier from real time to simulated time. Zero is allowed and behaves like a pause.</summary>
    public double TimeScale
    {
        get => _timeScale;
        set
        {
            if (!(value >= 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Time scale must be finite and non-negative.");
            }

            _timeScale = value;
        }
    }

    /// <summary>Total ticks dropped because a single update hit <see cref="MaxTicksPerUpdate"/>.</summary>
    public long DroppedTicks { get; private set; }

    /// <summary>Progress towards the next tick (0..1), for render interpolation.</summary>
    public double Alpha => Math.Clamp(_accumulator / TickDuration, 0.0, 1.0);

    /// <summary>Adds real elapsed time and runs every whole tick that is due.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous call. Negative or non-finite values are ignored.</param>
    /// <param name="step">Callback that advances the simulation by one tick.</param>
    /// <returns>Number of ticks run.</returns>
    public int Advance(double realDeltaSeconds, Action step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (IsPaused || !(realDeltaSeconds > 0.0) || double.IsInfinity(realDeltaSeconds))
        {
            return 0;
        }

        _accumulator += realDeltaSeconds * _timeScale;
        var ticks = 0;
        while (_accumulator + DueTolerance >= TickDuration && ticks < MaxTicksPerUpdate)
        {
            step();
            _accumulator -= TickDuration;
            ticks++;
        }

        if (_accumulator + DueTolerance >= TickDuration)
        {
            // Over the cap: drop whole ticks but keep the fractional remainder.
            var dropped = Math.Floor((_accumulator + DueTolerance) / TickDuration);
            DroppedTicks += (long)dropped;
            _accumulator -= dropped * TickDuration;
        }

        return ticks;
    }

    /// <summary>Discards accumulated time, for example after a world is replaced.</summary>
    public void Reset() => _accumulator = 0.0;
}
