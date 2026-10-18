namespace Pantheon.Domain.World;

/// <summary>The simulation's clock: a fixed number of ticks per simulated second (ADR-006).</summary>
public static class SimulationTime
{
    /// <summary>Ticks per simulated second.</summary>
    public const int TicksPerSecond = 10;

    /// <summary>Simulated seconds per tick.</summary>
    public const float SecondsPerTick = 1f / TicksPerSecond;
}
