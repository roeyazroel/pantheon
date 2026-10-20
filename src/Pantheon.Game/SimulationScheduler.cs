using System;

namespace Pantheon.Game;

/// <summary>
/// Fixed-step clock that turns variable render-frame time into whole simulation ticks. The
/// simulation therefore advances identically at any frame rate. A per-update cap stops a long
/// frame from triggering a catch-up spiral; ticks beyond the cap are dropped and counted.
/// </summary>
/// <remarks>
/// The simulation also keeps within a CPU budget (spec §105, ADR-056): once an update has spent
/// <see cref="BudgetSeconds"/>, the ticks still due wait for the next update, up to
/// <see cref="MaxBacklogTicks"/>, so a heavy world runs a little slower than asked rather than
/// stealing the frame. The caller measures the time, as the core never reads the clock.
/// </remarks>
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
    private double? _budgetSeconds;

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
        MaxBacklogTicks = 2 * maxTicksPerUpdate;
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

    /// <summary>Total ticks dropped because a single update hit <see cref="MaxTicksPerUpdate"/>, or the backlog <see cref="MaxBacklogTicks"/>.</summary>
    public long DroppedTicks { get; private set; }

    /// <summary>
    /// The most real seconds the ticks of one update may take before the rest wait for the next
    /// (spec §105); <c>null</c> for no budget. At least one due tick always runs.
    /// </summary>
    public double? BudgetSeconds
    {
        get => _budgetSeconds;
        set
        {
            if (value is { } seconds && (!(seconds > 0.0) || double.IsInfinity(seconds)))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A budget must be positive and finite.");
            }

            _budgetSeconds = value;
        }
    }

    /// <summary>The most due ticks that may wait for a later update when the budget is spent; more are dropped.</summary>
    public int MaxBacklogTicks { get; }

    /// <summary>Updates the budget cut short, leaving due ticks for later.</summary>
    public long BudgetHits { get; private set; }

    /// <summary>Whole ticks due but not yet run: those the budget put off.</summary>
    public int Backlog => (int)Math.Floor((_accumulator + DueTolerance) / TickDuration);

    /// <summary>Progress towards the next tick (0..1), for render interpolation.</summary>
    public double Alpha => Math.Clamp(_accumulator / TickDuration, 0.0, 1.0);

    /// <summary>Adds real elapsed time and runs every whole tick that is due, within the budget.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous call. Negative or non-finite values are ignored.</param>
    /// <param name="step">Callback that advances the simulation by one tick.</param>
    /// <param name="elapsedSeconds">
    /// Real seconds spent since this update began, measured by the caller; without it the budget is
    /// not applied.
    /// </param>
    /// <returns>Number of ticks run.</returns>
    public int Advance(double realDeltaSeconds, Action step, Func<double>? elapsedSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (IsPaused || !(realDeltaSeconds > 0.0) || double.IsInfinity(realDeltaSeconds))
        {
            return 0;
        }

        _accumulator += realDeltaSeconds * _timeScale;
        var ticks = 0;
        var spent = false;
        while (_accumulator + DueTolerance >= TickDuration && ticks < MaxTicksPerUpdate)
        {
            if (ticks > 0 && _budgetSeconds is { } budget && elapsedSeconds is not null && elapsedSeconds() >= budget)
            {
                spent = true;
                break;
            }

            step();
            _accumulator -= TickDuration;
            ticks++;
        }

        if (spent)
        {
            BudgetHits++;
        }

        // Over the cap, drop whole ticks but keep the fractional remainder; when the budget was
        // spent, keep a backlog for the next updates.
        var due = Math.Floor((_accumulator + DueTolerance) / TickDuration);
        var kept = spent ? MaxBacklogTicks : 0;
        if (due > kept)
        {
            var dropped = due - kept;
            DroppedTicks += (long)dropped;
            _accumulator -= dropped * TickDuration;
        }

        return ticks;
    }

    /// <summary>Discards accumulated time, for example after a world is replaced.</summary>
    public void Reset() => _accumulator = 0.0;
}
