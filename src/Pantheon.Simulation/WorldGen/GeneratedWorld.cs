using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Result of world generation: the authoritative state plus its derived terrain.</summary>
/// <param name="State">New world state (tick 0).</param>
/// <param name="Terrain">Derived heightfield, which is regenerated rather than saved.</param>
public sealed record GeneratedWorld(WorldState State, TerrainField Terrain);
