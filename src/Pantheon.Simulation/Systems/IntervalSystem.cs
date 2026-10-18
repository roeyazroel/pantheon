using System;

namespace Pantheon.Simulation.Systems;

/// <summary>
/// Runs an inner system once every <see cref="IntervalTicks"/> ticks, on the last tick of each
/// interval (ticks <c>I - 1</c>, <c>2I - 1</c>, ...), so that <c>n * IntervalTicks</c> ticks from
/// tick 0 run it exactly <c>n</c> times (ADR-008).
/// </summary>
public sealed class IntervalSystem : ISimulationSystem
{
    private readonly ISimulationSystem _inner;

    /// <summary>Wraps a system.</summary>
    /// <param name="inner">System to run on the interval.</param>
    /// <param name="intervalTicks">Ticks per run; at least 1.</param>
    public IntervalSystem(ISimulationSystem inner, int intervalTicks)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        _inner = inner;
        IntervalTicks = intervalTicks;
    }

    /// <summary>Ticks between runs of the inner system.</summary>
    public int IntervalTicks { get; }

    /// <summary>Whether a system on the given interval runs during the tick numbered <paramref name="tick"/>.</summary>
    /// <param name="tick">The tick being processed (<see cref="Pantheon.Domain.World.WorldState.Tick"/> before it is incremented).</param>
    /// <param name="intervalTicks">Ticks per run; at least 1.</param>
    /// <returns><c>true</c> on the last tick of each interval.</returns>
    public static bool IsDue(long tick, int intervalTicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        return (tick + 1) % intervalTicks == 0;
    }

    /// <summary>Ticks to run from <paramref name="tick"/> so that the system runs exactly once, on the last of them.</summary>
    /// <param name="tick">The next tick to be processed.</param>
    /// <param name="intervalTicks">Ticks per run; at least 1.</param>
    /// <returns>A count from 1 to <paramref name="intervalTicks"/>.</returns>
    public static int TicksUntilDue(long tick, int intervalTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalTicks, 1);
        return intervalTicks - (int)(tick % intervalTicks);
    }

    /// <inheritdoc />
    public void Update(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsDue(context.World.Tick, IntervalTicks))
        {
            _inner.Update(context);
        }
    }
}
