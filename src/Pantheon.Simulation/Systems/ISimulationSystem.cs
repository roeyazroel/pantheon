namespace Pantheon.Simulation.Systems;

/// <summary>
/// One deterministic simulation system. The engine runs systems in a fixed order once per tick.
/// Systems may only read and write state through the <see cref="TickContext"/>.
/// </summary>
public interface ISimulationSystem
{
    /// <summary>Advances this system by exactly one tick.</summary>
    /// <param name="context">World state and random stream for the tick.</param>
    void Update(TickContext context);
}
