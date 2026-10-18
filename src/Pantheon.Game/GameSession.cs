using System;
using System.Collections.Generic;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Terrain;
using Pantheon.Simulation.WorldGen;

namespace Pantheon.Game;

/// <summary>
/// Engine-agnostic application core. It owns the authoritative <see cref="WorldState"/>, its
/// derived <see cref="TerrainField"/> and the fixed-step clock, and it is the authority that checks
/// and applies the local player's commands. The Godot shell drives it and rebuilds visuals when
/// <see cref="WorldReplaced"/> fires.
/// </summary>
public sealed class GameSession
{
    // Events nobody drains (headless runs) are dropped oldest first beyond this many.
    private const int MaxPendingEvents = 512;

    private readonly SimulationEngine _engine;
    private readonly MiracleCaster _caster;
    private readonly Action _step;
    private readonly List<WorldEvent> _events = [];

    /// <summary>Creates a session around an existing world.</summary>
    private GameSession(GameContent content, WorldState world, TerrainField terrain)
    {
        Content = content;
        _engine = new SimulationEngine(content.SettlementRules, content.DivineRules);
        _caster = new MiracleCaster(content.DivineRules, content.SettlementRules, content.Miracles);
        _step = StepWorld;
        World = world;
        Terrain = terrain;
        Scheduler = new SimulationScheduler();
        LocalGodId = FindLocalGod(world);
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

    /// <summary>The god the local player controls: the first god with a human controller, if any.</summary>
    public int? LocalGodId { get; private set; }

    /// <summary>The god the local player controls, if any.</summary>
    public GodState? LocalGod => LocalGodId is { } id ? World.Gods.Find(god => god.Id == id) : null;

    /// <summary>
    /// Simulated time in seconds, including the progress towards the next tick, for smooth
    /// presentation such as walking citizens. It stands still while the clock is paused.
    /// </summary>
    public double SimulationSeconds => (World.Tick + Scheduler.Alpha) * Scheduler.TickDuration;

    /// <summary>Generates a new world.</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="seed">World seed.</param>
    /// <returns>The new session.</returns>
    public static GameSession CreateNew(GameContent content, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(content);
        var generated = WorldGenerator.Generate(seed, content.WorldGen, content.Pantheon);
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

    /// <summary>Runs several ticks now, as <see cref="StepOneTick"/> does, for example to start a capture from a grown settlement.</summary>
    /// <param name="ticks">Number of ticks; must not be negative.</param>
    public void RunTicks(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        for (var i = 0; i < ticks; i++)
        {
            _step();
        }
    }

    /// <summary>Runs ticks up to and including the next settlement step.</summary>
    /// <returns>Number of ticks run, from 1 to <see cref="Pantheon.Domain.Settlements.SettlementRules.StepIntervalTicks"/>.</returns>
    public int StepToNextSettlementStep()
    {
        var ticks = IntervalSystem.TicksUntilDue(World.Tick, Content.SettlementRules.StepIntervalTicks);
        RunTicks(ticks);
        return ticks;
    }

    /// <summary>Replaces the current world with a newly generated one.</summary>
    /// <param name="seed">Seed of the new world.</param>
    public void StartNewWorld(WorldSeed seed)
    {
        var generated = WorldGenerator.Generate(seed, Content.WorldGen, Content.Pantheon);
        Replace(generated.State, generated.Terrain);
    }

    /// <summary>Checks whether the local player's god may cast a miracle at a target, without casting it.</summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where it would be cast.</param>
    /// <returns>The verdict, with the influence and cost at the target.</returns>
    public CastCheck CheckCast(string miracleId, GroundPosition target) =>
        _caster.Check(World, new CastMiracleRequest(LocalGodId ?? 0, miracleId, target), LocalGodId ?? 0);

    /// <summary>The settlements a miracle cast at a target would reach.</summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where it would be cast.</param>
    /// <returns>The settlements; none for an unknown miracle.</returns>
    public IReadOnlyList<Domain.Settlements.SettlementState> FindReached(string miracleId, GroundPosition target) =>
        Content.Miracles.Find(miracleId) is { } miracle ? _caster.FindReached(World, miracle, target) : [];

    /// <summary>
    /// Casts a miracle for the local player's god (spec §73): the request is checked like any other
    /// issuer's, and the world changes only when it is accepted.
    /// </summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where to cast it.</param>
    /// <returns>The verdict.</returns>
    public CastCheck CastMiracle(string miracleId, GroundPosition target) =>
        _caster.Cast(World, new CastMiracleRequest(LocalGodId ?? 0, miracleId, target), LocalGodId ?? 0, _events);

    /// <summary>The latest miracle cast still waiting to be drained, if any.</summary>
    /// <returns>The event, or <c>null</c>.</returns>
    public MiracleCast? PeekLastCast() => _events.FindLast(e => e is MiracleCast) as MiracleCast;

    /// <summary>Returns the events raised since the last call, oldest first, and forgets them.</summary>
    /// <returns>The events.</returns>
    public IReadOnlyList<WorldEvent> DrainEvents()
    {
        var events = _events.ToArray();
        _events.Clear();
        return events;
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
        LocalGodId = FindLocalGod(world);
        _events.Clear();
        Scheduler.Reset();
        WorldReplaced?.Invoke();
    }

    /// <summary>Runs one simulation tick on the current world.</summary>
    private void StepWorld()
    {
        _engine.Step(World, _events);
        if (_events.Count > MaxPendingEvents)
        {
            _events.RemoveRange(0, _events.Count - MaxPendingEvents);
        }
    }

    /// <summary>The first god with a human controller.</summary>
    private static int? FindLocalGod(WorldState world) =>
        world.Gods.Find(god => god.Controller == GodController.Human)?.Id;
}
