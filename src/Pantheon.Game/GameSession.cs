using System;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Terrain;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>
/// Engine-agnostic application core. It owns the authoritative <see cref="WorldState"/>, its
/// derived <see cref="TerrainField"/> and the fixed-step clock. The Godot shell drives it and
/// rebuilds visuals when <see cref="WorldReplaced"/> fires.
/// </summary>
public sealed class GameSession
{
    private readonly SimulationEngine _engine;
    private readonly Action _step;

    /// <summary>Creates a session around an existing world.</summary>
    private GameSession(GameContent content, WorldState world, TerrainField terrain)
    {
        Content = content;
        _engine = new SimulationEngine(content.SettlementRules);
        _step = StepWorld;
        World = world;
        Terrain = terrain;
        Scheduler = new SimulationScheduler();
    }

    /// <summary>Raised after <see cref="World"/> and <see cref="Terrain"/> are replaced by a new or loaded world.</summary>
    public event Action? WorldReplaced;

    /// <summary>Content the session was created with.</summary>
    public GameContent Content { get; }

    /// <summary>Authoritative world state.</summary>
    public WorldState World { get; private set; }

    /// <summary>Terrain derived from <see cref="World"/>.</summary>
    public TerrainField Terrain { get; private set; }

    /// <summary>Fixed-step simulation clock.</summary>
    public SimulationScheduler Scheduler { get; }

    /// <summary>Generates a new world.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="seed">World seed.</param>
    /// <returns>The new session.</returns>
    public static GameSession CreateNew(GameContent content, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(content);
        var generated = WorldGenerator.Generate(seed, content.WorldGen);
        return new GameSession(content, generated.State, generated.Terrain);
    }

    /// <summary>Loads a saved world.</summary>
    /// <param name="content">Validated content (the settlement rules apply to the loaded world).</param>
    /// <param name="path">Save file path.</param>
    /// <returns>The loaded session.</returns>
    /// <exception cref="SaveFormatException">The save is invalid.</exception>
    public static GameSession Load(GameContent content, string path)
    {
        ArgumentNullException.ThrowIfNull(content);
        var (world, terrain) = LoadWorld(path);
        return new GameSession(content, world, terrain);
    }

    /// <summary>Advances the simulation by real elapsed time.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous frame.</param>
    /// <returns>Number of ticks run.</returns>
    public int Update(double realDeltaSeconds) => Scheduler.Advance(realDeltaSeconds, _step);

    /// <summary>
    /// Runs exactly one tick now, through the same engine call as <see cref="Update"/>. The
    /// clock's accumulated time is left alone, so N steps change the world exactly as N
    /// scheduled ticks do.
    /// </summary>
    public void StepOneTick() => _step();

    /// <summary>Replaces the current world with a newly generated one.</summary>
    /// <param name="seed">Seed of the new world.</param>
    public void StartNewWorld(WorldSeed seed)
    {
        var generated = WorldGenerator.Generate(seed, Content.WorldGen);
        Replace(generated.State, generated.Terrain);
    }

    /// <summary>Saves the current world.</summary>
    /// <param name="path">Destination file path.</param>
    public void SaveTo(string path) => WorldSaveSerializer.SaveToFile(World, path);

    /// <summary>Replaces the current world with a saved one. On failure the current world is kept.</summary>
    /// <param name="path">Save file path.</param>
    /// <exception cref="SaveFormatException">The save is invalid.</exception>
    public void LoadFrom(string path)
    {
        var (world, terrain) = LoadWorld(path);
        Replace(world, terrain);
    }

    /// <summary>Reads a save and regenerates its terrain from the saved seed and generation settings.</summary>
    private static (WorldState World, TerrainField Terrain) LoadWorld(string path)
    {
        var world = WorldSaveSerializer.LoadFromFile(path);
        try
        {
            // The saved settings, not the current content files, define the terrain, so a save
            // always reproduces the terrain it was played on.
            return (world, TerrainGenerator.Generate(world.Seed, world.Generation));
        }
        catch (ArgumentException exception)
        {
            throw new SaveFormatException($"The save's generation settings are invalid: {exception.Message}", exception);
        }
    }

    /// <summary>Swaps in a new world and notifies listeners.</summary>
    private void Replace(WorldState world, TerrainField terrain)
    {
        World = world;
        Terrain = terrain;
        Scheduler.Reset();
        WorldReplaced?.Invoke();
    }

    /// <summary>Runs one simulation tick on the current world.</summary>
    private void StepWorld() => _engine.Step(World);
}
