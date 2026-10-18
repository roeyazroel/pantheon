using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;

namespace Pantheon.Simulation.Systems;

/// <summary>Everything a system may touch during one tick.</summary>
/// <param name="World">World state being advanced; systems mutate it in place.</param>
/// <param name="Random">Core random stream for this tick. It is restored from and written back to <see cref="WorldState.Random"/>.</param>
/// <param name="Events">Events raised during the tick, in the order they happened.</param>
public sealed record TickContext(WorldState World, SimulationRandom Random, List<WorldEvent> Events);
