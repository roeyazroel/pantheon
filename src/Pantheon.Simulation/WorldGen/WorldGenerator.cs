using System;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>
/// Creates a complete new world from a seed. The same seed and settings always give the same
/// world.
/// </summary>
public static class WorldGenerator
{
    /// <summary>Name of the RNG stream that drives the tick systems.</summary>
    public const string SimulationStream = "simulation";

    /// <summary>Generates terrain, regions and the starting settlement.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Data-driven generation settings; they are stored in the state.</param>
    /// <returns>The new world state and its terrain.</returns>
    public static GeneratedWorld Generate(WorldSeed seed, WorldGenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var terrain = TerrainGenerator.Generate(seed, settings);
        var regions = RegionBuilder.Build(terrain, settings);
        var settlement = SettlementFounder.Found(regions, terrain, seed, settings.StartingSettlement);

        var state = new WorldState
        {
            Seed = seed,
            Generation = settings,
            Tick = 0,
            Random = new SimulationRandom(SeedMixer.Derive(seed, SimulationStream)).GetState(),
            Regions = regions,
            Settlements = [settlement],
        };

        return new GeneratedWorld(state, terrain);
    }
}
