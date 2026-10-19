using System;
using System.Collections.Generic;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Diplomacy;
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
    private readonly Authorities _authorities;
    private readonly Action _step;
    private readonly List<WorldEvent> _events = [];

    /// <summary>Creates a session around an existing world.</summary>
    private GameSession(GameContent content, WorldState world, TerrainField terrain)
    {
        Content = content;
        _engine = content.CreateEngine();
        _authorities = _engine.Authorities!;
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

    /// <summary>The local player's god's Creature, if it has one.</summary>
    public CreatureState? LocalCreature => LocalGod?.CreatureId is { } id ? World.Creatures.Find(creature => creature.Id == id) : null;

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
        var generated = WorldGenerator.Generate(seed, content.WorldGen, content.Pantheon, content.Creatures);
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
        var generated = WorldGenerator.Generate(seed, Content.WorldGen, Content.Pantheon, Content.Creatures);
        Replace(generated.State, generated.Terrain);
    }

    /// <summary>Checks whether the local player's god may cast a miracle at a target, without casting it.</summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where it would be cast.</param>
    /// <returns>The verdict, with the influence and cost at the target.</returns>
    public CastCheck CheckCast(string miracleId, GroundPosition target) =>
        _authorities.Miracles.Check(World, new CastMiracleRequest(LocalGodId ?? 0, miracleId, target), LocalGodId ?? 0);

    /// <summary>The settlements a miracle cast at a target would reach.</summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where it would be cast.</param>
    /// <returns>The settlements; none for an unknown miracle.</returns>
    public IReadOnlyList<Domain.Settlements.SettlementState> FindReached(string miracleId, GroundPosition target) =>
        Content.Miracles.Find(miracleId) is { } miracle ? _authorities.Miracles.FindReached(World, miracle, target) : [];

    /// <summary>
    /// Casts a miracle for the local player's god (spec §73): the request is checked like any other
    /// issuer's, and the world changes only when it is accepted.
    /// </summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where to cast it.</param>
    /// <returns>The verdict.</returns>
    public CastCheck CastMiracle(string miracleId, GroundPosition target) =>
        _authorities.Miracles.Cast(World, new CastMiracleRequest(LocalGodId ?? 0, miracleId, target), LocalGodId ?? 0, _events);

    /// <summary>
    /// Sends a command to the local player's Creature (spec §73, ADR-010): reward or punish what it is
    /// doing or just did, or send it somewhere. The authority checks it first.
    /// </summary>
    /// <param name="kind">Reward, punish or go to.</param>
    /// <param name="target">Where to go, for <see cref="CreatureCommandKind.GoTo"/>.</param>
    /// <returns>The outcome.</returns>
    public CreatureCommandResult CommandCreature(CreatureCommandKind kind, GroundPosition target = default)
    {
        var god = LocalGodId ?? 0;
        var creature = LocalGod?.CreatureId ?? 0;
        return _authorities.Creatures.Execute(World, new CreatureCommand(god, creature, kind, target), god, _events);
    }

    /// <summary>What a reward or punishment of the local Creature would judge now, if anything.</summary>
    /// <returns>The situation, the action, and the settlement or other Creature it concerned, or <c>null</c>.</returns>
    public (CreatureSituation Situation, CreatureAction Action, int? SettlementId, int? OtherCreatureId)? FindJudgedAction()
    {
        if (LocalCreature is not { } creature || _authorities.Creatures.FindJudged(World, creature) is not { } judged)
        {
            return null;
        }

        return judged.Memory is { } memory
            ? (judged.Situation, judged.Action, memory.SettlementId, memory.OtherCreatureId)
            : (judged.Situation, judged.Action, creature.Activity.TargetSettlementId, creature.Activity.TargetCreatureId);
    }

    /// <summary>The local Creature's options now, best first, for the debug view.</summary>
    /// <returns>The options; none without a Creature.</returns>
    public IReadOnlyList<CreatureOption> PreviewCreatureOptions() =>
        LocalCreature is { } creature ? _engine.Creatures!.Brain.ListOptions(World, creature) : [];

    /// <summary>The Creature nearest the local Creature that is not banished, if any (ADR-017).</summary>
    /// <returns>The Creature, or <c>null</c>.</returns>
    public CreatureState? NearestOtherCreature()
    {
        if (LocalCreature is not { } mine)
        {
            return null;
        }

        CreatureState? nearest = null;
        var best = float.MaxValue;
        foreach (var other in World.Creatures)
        {
            var dx = other.Position.X - mine.Position.X;
            var dz = other.Position.Z - mine.Position.Z;
            var distance = (dx * dx) + (dz * dz);
            if (other.Id != mine.Id && other.Activity.Phase != CreaturePhase.Banished && distance < best)
            {
                (nearest, best) = (other, distance);
            }
        }

        return nearest;
    }

    /// <summary>How the local Creature would answer the nearest other Creature if they met now, best first, for the debug view.</summary>
    /// <returns>The answers; none without two Creatures.</returns>
    public IReadOnlyList<CreatureOption> PreviewCreatureResponses() =>
        LocalCreature is { } mine && NearestOtherCreature() is { } other ? _engine.Creatures!.Brain.ListResponses(World, mine, other) : [];

    /// <summary>
    /// Debug (ADR-017): brings the nearest other Creature beside the local Creature, free to meet it
    /// at once, as the M key brings migrants. The meeting itself happens in the simulation.
    /// </summary>
    /// <param name="name">The Creature to bring, by name (any case); the nearest when <c>null</c> or empty.</param>
    /// <returns>The Creature brought, or <c>null</c> without such a Creature or room beside the local one.</returns>
    public CreatureState? BringOtherCreature(string? name = null)
    {
        var other = string.IsNullOrEmpty(name)
            ? NearestOtherCreature()
            : World.Creatures.Find(c => c.Id != LocalCreature?.Id && c.Activity.Phase != CreaturePhase.Banished && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (LocalCreature is not { Activity.Phase: not CreaturePhase.Banished } mine || other is null)
        {
            return null;
        }

        if (CreatureFounder.Beside(World.Regions, mine.Position) is not { } spot)
        {
            return null;
        }

        other.Position = spot;
        other.Activity.Path.Clear();
        other.Activity.Phase = CreaturePhase.Deciding;
        other.Activity.TicksLeft = 0;
        foreach (var view in new[] { CreatureBonds.Find(mine, other.Id), CreatureBonds.Find(other, mine.Id) })
        {
            if (view is not null)
            {
                view.LastMetTick = -1;
            }
        }

        return other;
    }

    /// <summary>
    /// Sends a diplomatic command from the local player's god to another god (spec §113, ADR-012); the
    /// authority checks it first.
    /// </summary>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Why it was refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection CommandDiplomacy(int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind) =>
        _authorities.Diplomacy.Execute(World, new Domain.Diplomacy.DiplomaticCommand(LocalGodId ?? 0, otherGodId, kind), LocalGodId ?? 0, _events);

    /// <summary>Whether a diplomatic command from the local player's god would be accepted now.</summary>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Why it would be refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection CheckDiplomacy(int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind) =>
        _authorities.Diplomacy.Check(World, new Domain.Diplomacy.DiplomaticCommand(LocalGodId ?? 0, otherGodId, kind), LocalGodId ?? 0);

    /// <summary>The diplomatic moves an AI god is weighing now, best first, for the debug view.</summary>
    /// <param name="god">The god.</param>
    /// <returns>Its candidates; none for a player's god.</returns>
    public IReadOnlyList<Simulation.Gods.DiplomacyCandidate> PreviewGodDiplomacy(GodState god) =>
        god.Controller == GodController.Ai ? _engine.Gods!.Ai.EvaluateDiplomacy(World, god) : [];

    /// <summary>What an AI god is weighing now, best first, for the debug view (spec §106).</summary>
    /// <param name="god">The god.</param>
    /// <returns>Its candidate actions; none for a god without an AI personality.</returns>
    public IReadOnlyList<Simulation.Gods.AiCandidate> PreviewGodAi(GodState god) =>
        god.Controller == GodController.Ai ? _engine.Gods!.Ai.Evaluate(World, god) : [];

    /// <summary>Sets the local player's god's refugee policy (spec §31, ADR-016); the authority checks it first.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>Why it was refused, or <see cref="Simulation.Migration.RefugeePolicyRejection.None"/>.</returns>
    public Simulation.Migration.RefugeePolicyRejection SetRefugeePolicy(Domain.Migration.RefugeePolicy policy) =>
        _authorities.Migration.SetPolicy(World, LocalGodId ?? 0, policy, LocalGodId ?? 0, _events);

    /// <summary>The pressure on a settlement's people to leave now (ADR-016).</summary>
    /// <param name="settlement">The settlement.</param>
    /// <returns>The pressure and its main cause.</returns>
    public Simulation.Migration.MigrationPressure MigrationPressureOn(Domain.Settlements.SettlementState settlement) =>
        _engine.Migration!.PressureOn(World, settlement);

    /// <summary>Sends a group out of a settlement now (the debug tools' forced migration, ADR-016).</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="people">How many leave.</param>
    /// <returns>The group, or <c>null</c> when nobody could leave or there is nowhere to go.</returns>
    public Domain.Migration.MigrationGroup? SendMigrants(Domain.Settlements.SettlementState settlement, int people) =>
        _engine.Migration!.SendOut(World, settlement, people, _events);

    /// <summary>The refugee policy an AI god wants now and why, for the debug view.</summary>
    /// <param name="god">The god.</param>
    /// <returns>The policy and reason; <c>null</c> for a player's god.</returns>
    public (Domain.Migration.RefugeePolicy Policy, string Reason)? PreviewRefugeePolicy(GodState god) =>
        god.Controller == GodController.Ai && _engine.Gods!.Ai.ProfileOf(god.Personality) is { } profile && _engine.Gods.Refugees is { } advisor
            ? advisor.Choose(World, god, profile)
            : null;

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
