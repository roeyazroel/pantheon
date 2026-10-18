using System;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
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

    /// <summary>Generates terrain, regions and the starting settlements, without gods (for focused tests).</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Data-driven generation settings; they are stored in the state.</param>
    /// <returns>The new world state and its terrain.</returns>
    public static GeneratedWorld Generate(WorldSeed seed, WorldGenSettings settings) => Generate(seed, settings, pantheon: null);

    /// <summary>Generates terrain, regions, the starting settlements, the gods and their Creatures.</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="settings">Data-driven generation settings; they are stored in the state.</param>
    /// <param name="pantheon">The gods to create, each followed in its own home settlement; <c>null</c> for none.</param>
    /// <param name="creatureRules">Creature rules, so each god's Creature is born at its home; <c>null</c> for none.</param>
    /// <returns>The new world state and its terrain.</returns>
    public static GeneratedWorld Generate(WorldSeed seed, WorldGenSettings settings, PantheonSetup? pantheon, CreatureRules? creatureRules = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var terrain = TerrainGenerator.Generate(seed, settings);
        var regions = RegionBuilder.Build(terrain, settings);
        var settlements = SettlementFounder.FoundAll(regions, terrain, seed, settings.StartingSettlement);
        var (gods, creatures) = pantheon is null ? ([], []) : PantheonFounder.Found(pantheon, settlements, creatureRules, seed, regions);

        var state = new WorldState
        {
            Seed = seed,
            Generation = settings,
            Tick = 0,
            Random = new SimulationRandom(SeedMixer.Derive(seed, SimulationStream)).GetState(),
            Regions = regions,
            Settlements = settlements,
            Gods = gods,
            Creatures = creatures,
        };

        return new GeneratedWorld(state, terrain);
    }
}
