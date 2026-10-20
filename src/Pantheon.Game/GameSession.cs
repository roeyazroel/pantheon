using System;
using System.Collections.Generic;
using System.Linq;
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
/// and applies the players' commands. The Godot shell drives it and rebuilds visuals when
/// <see cref="WorldReplaced"/> fires. A player connected to a host holds a replica instead
/// (ADR-018): it runs the simulation only to keep its view moving between the host's snapshots,
/// keeps none of the events it raises, and sends its commands to the host.
/// </summary>
public sealed class GameSession
{
    private readonly SimulationEngine _engine;
    private readonly Authorities _authorities;
    private readonly Action _step;
    private readonly SessionEvents _queue = new();
    private readonly List<WorldEvent> _predicted = [];
    private readonly Dictionary<int, string> _players = [];
    private TerrainField _terrain;

    /// <summary>Creates a session around an existing world.</summary>
    private GameSession(GameContent content, WorldState world, TerrainField terrain, int? localGodId = null)
    {
        Content = content;
        _engine = content.CreateEngine();
        _authorities = _engine.Authorities!;
        _step = StepWorld;
        World = world;
        _terrain = terrain;
        (_authorities.Miracles.Land.Terrain, _authorities.Miracles.Land.Shaped) = (terrain, world.Land.Reshapes.Count);
        Scheduler = new SimulationScheduler();
        LocalGodId = localGodId ?? FindLocalGod(world);
    }

    /// <summary>Raised after <see cref="World"/> and <see cref="Terrain"/> are replaced by a new or loaded world.</summary>
    public event Action? WorldReplaced;

    /// <summary>Raised after a Divine Act reshaped the land and <see cref="Terrain"/> was made again (ADR-045).</summary>
    public event Action? TerrainChanged;

    /// <summary>Content the session was created with.</summary>
    public GameContent Content { get; }

    /// <summary>Authoritative world state.</summary>
    public WorldState World { get; private set; }

    /// <summary>Terrain derived from <see cref="World"/>, with the land's reshapings (ADR-045).</summary>
    public TerrainField Terrain
    {
        get => _terrain;
        private set => _authorities.Miracles.Land.Terrain = _terrain = value;
    }

    /// <summary>Fixed-step simulation clock.</summary>
    public SimulationScheduler Scheduler { get; }

    /// <summary>The time the village crowds live by (ADR-049): presentation only, never saved.</summary>
    public Population.CrowdClock Crowds { get; } = new();

    /// <summary>The god the local player controls: the first god with a human controller, or a joined player's god.</summary>
    public int? LocalGodId { get; private set; }

    /// <summary>The host this session's player is connected to, when the session is a replica (ADR-018).</summary>
    public Multiplayer.MultiplayerClient? Client { get; private set; }

    /// <summary>Whether the session is a replica of a host's world, which only the host may change.</summary>
    public bool IsReplica => Client is not null;

    /// <summary>
    /// Whether events are also kept for the host to send to its players (<see cref="DrainOutbox"/>),
    /// besides the local player's (<see cref="DrainEvents"/>).
    /// </summary>
    public bool KeepsOutbox
    {
        get => _queue.KeepsOutbox;
        set => _queue.KeepsOutbox = value;
    }

    /// <summary>The players' names by the god each plays, when a session is shared (ADR-018).</summary>
    public IReadOnlyDictionary<int, string> Players => _players;

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
    /// <param name="species">The species of the player's Creature; <c>gods.json</c>'s when <c>null</c> (ADR-021).</param>
    /// <returns>The new session.</returns>
    public static GameSession CreateNew(GameContent content, WorldSeed seed, string? species = null) => CreateNew(content, seed, new NewWorldOptions(species));

    /// <summary>Generates a new world at a size, with its victories (ADR-027).</summary>
    /// <param name="content">Validated content.</param>
    /// <param name="seed">World seed.</param>
    /// <param name="options">The Creature's species, the size and the victories.</param>
    /// <returns>The session.</returns>
    public static GameSession CreateNew(GameContent content, WorldSeed seed, NewWorldOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        var generated = WorldMaker.Generate(content, seed, options);
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
        var (world, terrain) = SessionStore.LoadWorld(content, path);
        return new GameSession(content, world, terrain);
    }

    /// <summary>A replica of a host's world for a joined player (ADR-018), its terrain regenerated from the world's seed.</summary>
    /// <param name="content">Validated content, the same as the host's.</param>
    /// <param name="world">The host's world.</param>
    /// <param name="localGodId">The god the player plays.</param>
    /// <param name="client">The connection to the host.</param>
    /// <returns>The replica session.</returns>
    public static GameSession CreateReplica(GameContent content, WorldState world, int localGodId, Multiplayer.MultiplayerClient client)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(client);
        return new GameSession(content, world, TerrainGenerator.Generate(world.Seed, world.Generation, world.Land.Reshapes), localGodId) { Client = client };
    }

    /// <summary>
    /// Takes the host's newer world into a replica (ADR-018): the world is replaced as a whole and
    /// the clock follows the host's. The terrain is kept while the seed and generation match.
    /// </summary>
    /// <param name="world">The host's world.</param>
    /// <param name="timeScale">The host's simulation speed.</param>
    /// <param name="paused">Whether the host's clock is paused.</param>
    public void ApplySnapshot(WorldState world, double timeScale, bool paused)
    {
        ArgumentNullException.ThrowIfNull(world);
        // Generation settings hold lists, so they are compared by what fixes the land: the seed and the grid.
        var (grid, current) = (world.Regions, World.Regions);
        var sameLand = world.Seed == World.Seed && grid.Columns == current.Columns && grid.Rows == current.Rows && grid.RegionSize == current.RegionSize
            && world.Generation.HeightmapResolution == World.Generation.HeightmapResolution && world.Generation.VertexSpacing == World.Generation.VertexSpacing;
        World = world;
        Scheduler.TimeScale = timeScale;
        Scheduler.IsPaused = paused;
        if (!sameLand)
        {
            Terrain = TerrainGenerator.Generate(world.Seed, world.Generation, world.Land.Reshapes);
            _authorities.Miracles.Land.Shaped = world.Land.Reshapes.Count;
            Scheduler.Reset();
            WorldReplaced?.Invoke();
            return;
        }

        SyncLand();
    }

    /// <summary>Makes the terrain again when the world holds reshapings it lacks: a host's Divine Act reached a replica (ADR-045).</summary>
    public void SyncLand()
    {
        if (World.Land.Reshapes.Count != _authorities.Miracles.Land.Shaped)
        {
            Terrain = TerrainGenerator.Reshape(Terrain, World.Seed, World.Generation, World.Land.Reshapes);
            _authorities.Miracles.Land.Shaped = World.Land.Reshapes.Count;
            TerrainChanged?.Invoke();
        }
    }

    /// <summary>Adds events the host's world raised, for the local player's alerts and effects.</summary>
    /// <param name="events">The events, oldest first.</param>
    public void ReceiveEvents(IEnumerable<WorldEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _queue.Add(events);
    }

    /// <summary>Records who plays which god, for the HUD and the panels.</summary>
    /// <param name="players">Player names by god id.</param>
    public void SetPlayers(IEnumerable<KeyValuePair<int, string>> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        _players.Clear();
        foreach (var (godId, name) in players)
        {
            _players[godId] = name;
        }
    }

    /// <summary>Returns the events raised since the last call for the host to send, oldest first, and forgets them.</summary>
    /// <returns>The events; none unless <see cref="KeepsOutbox"/> is set.</returns>
    public IReadOnlyList<WorldEvent> DrainOutbox() => _queue.DrainOutbox();

    /// <summary>Advances the simulation by real elapsed time, within the scheduler's CPU budget.</summary>
    /// <param name="realDeltaSeconds">Real seconds since the previous frame.</param>
    /// <param name="elapsedSeconds">Real seconds spent since this update began, measured by the client, for the budget.</param>
    /// <returns>Number of ticks run.</returns>
    public int Update(double realDeltaSeconds, Func<double>? elapsedSeconds = null) => Scheduler.Advance(realDeltaSeconds, _step, elapsedSeconds);

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
    /// <param name="species">The species of the player's Creature; <c>gods.json</c>'s when <c>null</c> (ADR-021).</param>
    public void StartNewWorld(WorldSeed seed, string? species = null) => StartNewWorld(seed, new NewWorldOptions(species));

    /// <summary>Replaces the world with a new one at a size, with its victories (ADR-027).</summary>
    /// <param name="seed">World seed.</param>
    /// <param name="options">The Creature's species, the size and the victories.</param>
    public void StartNewWorld(WorldSeed seed, NewWorldOptions options)
    {
        var generated = WorldMaker.Generate(Content, seed, options);
        Replace(generated.State, generated.Terrain);
    }

    /// <summary>Takes up a domain for a god, as its player's intent (spec §12, ADR-021); the authority checks it first.</summary>
    /// <param name="godId">The issuing god.</param>
    /// <param name="domain">The domain.</param>
    /// <returns>Why it was refused, or <see cref="Simulation.Domains.DomainRejection.None"/>.</returns>
    public Simulation.Domains.DomainRejection ChooseDomainAs(int godId, GodDomain domain) =>
        Chronicled(() => _authorities.Domains?.Choose(World, godId, domain, godId, _queue.Pending) ?? Simulation.Domains.DomainRejection.UnknownDomain);

    /// <summary>Runs a command through the authorities as the host; its events reach the next frame and the history (ADR-023).</summary>
    /// <typeparam name="T">The command's verdict.</typeparam>
    /// <param name="command">The command, given the authorities and where to raise its events.</param>
    /// <returns>The verdict.</returns>
    public T Command<T>(Func<Simulation.Systems.Authorities, ICollection<Domain.Events.WorldEvent>, T> command) => Chronicled(() => command(_authorities, _queue.Pending));

    /// <summary>Asks the authorities without changing the world, for readouts (ADR-025).</summary>
    /// <typeparam name="T">The answer.</typeparam>
    /// <param name="query">The question, given the authorities.</param>
    /// <returns>The answer.</returns>
    public T Inspect<T>(Func<Simulation.Systems.Authorities, T> query) => query(_authorities);

    /// <summary>The politics system (ADR-025), for the debug tool that summons the council.</summary>
    internal Simulation.Politics.PoliticsSystem? Politics => _engine.Politics;

    /// <summary>The engine, for the debug previews of <see cref="SessionPreviews"/>.</summary>
    internal SimulationEngine Engine => _engine;

    /// <summary>Whether the local player casts in secret (ADR-026): a preference, not game state.</summary>
    public bool Veiled { get; set; }

    /// <summary>Whether the local player's god may take up a domain now.</summary>
    /// <param name="domain">The domain.</param>
    /// <returns>Why it would be refused, or <see cref="Simulation.Domains.DomainRejection.None"/>.</returns>
    public Simulation.Domains.DomainRejection CheckDomain(GodDomain domain) =>
        _authorities.Domains?.Check(World, LocalGodId ?? 0, domain, LocalGodId ?? 0) ?? Simulation.Domains.DomainRejection.UnknownDomain;

    /// <summary>Checks whether the local player's god may cast a miracle at a target, without casting it.</summary>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where it would be cast.</param>
    /// <returns>The verdict, with the influence and cost at the target.</returns>
    public CastCheck CheckCast(string miracleId, GroundPosition target) =>
        _authorities.Miracles.Check(World, new CastMiracleRequest(LocalGodId ?? 0, miracleId, target, Veiled), LocalGodId ?? 0);

    /// <summary>Casts a miracle for a god, as its player's intent (spec §73): checked as any issuer's is.</summary>
    /// <param name="godId">The issuing god: the local player's, or a connected player's on the host.</param>
    /// <param name="miracleId">The miracle.</param>
    /// <param name="target">Where to cast it.</param>
    /// <param name="veiled">Whether it is cast in secret (ADR-026).</param>
    /// <returns>The verdict.</returns>
    public CastCheck CastMiracleAs(int godId, string miracleId, GroundPosition target, bool veiled = false) =>
        Chronicled(() => _authorities.Miracles.Cast(World, new CastMiracleRequest(godId, miracleId, target, veiled), godId, _queue.Pending));

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
    public CastCheck CastMiracle(string miracleId, GroundPosition target) => CastMiracleAs(LocalGodId ?? 0, miracleId, target);

    /// <summary>
    /// Sends a command to the local player's Creature (spec §73, ADR-010): reward or punish what it is
    /// doing or just did, or send it somewhere. The authority checks it first.
    /// </summary>
    /// <param name="kind">Reward, punish or go to.</param>
    /// <param name="target">Where to go, for <see cref="CreatureCommandKind.GoTo"/>.</param>
    /// <returns>The outcome.</returns>
    public CreatureCommandResult CommandCreature(CreatureCommandKind kind, GroundPosition target = default) => CommandCreatureAs(LocalGodId ?? 0, kind, target);

    /// <summary>Sends a command to a god's Creature, as its player's intent; the authority checks it first.</summary>
    /// <param name="godId">The issuing god.</param>
    /// <param name="kind">Reward, punish or go to.</param>
    /// <param name="target">Where to go, for <see cref="CreatureCommandKind.GoTo"/>.</param>
    /// <returns>The outcome.</returns>
    public CreatureCommandResult CommandCreatureAs(int godId, CreatureCommandKind kind, GroundPosition target = default)
    {
        var creature = World.Gods.Find(g => g.Id == godId)?.CreatureId ?? 0;
        return _authorities.Creatures.Execute(World, new CreatureCommand(godId, creature, kind, target), godId, _queue.Pending);
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

    /// <summary>The Creatures' brain, for previews of their options and answers.</summary>
    internal CreatureBrain CreatureBrain => _engine.Creatures!.Brain;

    /// <summary>
    /// Sends a diplomatic command from the local player's god to another god (spec §113, ADR-012); the
    /// authority checks it first.
    /// </summary>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Why it was refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection CommandDiplomacy(int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind) => CommandDiplomacyAs(LocalGodId ?? 0, otherGodId, kind);

    /// <summary>Sends a diplomatic command from a god to another, as its player's intent; the authority checks it first.</summary>
    /// <param name="godId">The issuing god.</param>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Why it was refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection CommandDiplomacyAs(int godId, int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind) =>
        Chronicled(() => _authorities.Diplomacy.Execute(World, new Domain.Diplomacy.DiplomaticCommand(godId, otherGodId, kind), godId, _queue.Pending));

    /// <summary>Whether a diplomatic command from the local player's god would be accepted now.</summary>
    /// <param name="otherGodId">The other god.</param>
    /// <param name="kind">What to do.</param>
    /// <returns>Why it would be refused, or <see cref="DiplomacyRejection.None"/>.</returns>
    public DiplomacyRejection CheckDiplomacy(int otherGodId, Domain.Diplomacy.DiplomaticCommandKind kind) =>
        _authorities.Diplomacy.Check(World, new Domain.Diplomacy.DiplomaticCommand(LocalGodId ?? 0, otherGodId, kind), LocalGodId ?? 0);

    /// <summary>Sets the local player's god's refugee policy (spec §31, ADR-016); the authority checks it first.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>Why it was refused, or <see cref="Simulation.Migration.RefugeePolicyRejection.None"/>.</returns>
    public Simulation.Migration.RefugeePolicyRejection SetRefugeePolicy(Domain.Migration.RefugeePolicy policy) => SetRefugeePolicyAs(LocalGodId ?? 0, policy);

    /// <summary>Sets a god's refugee policy, as its player's intent; the authority checks it first.</summary>
    /// <param name="godId">The issuing god.</param>
    /// <param name="policy">The policy.</param>
    /// <returns>Why it was refused, or <see cref="Simulation.Migration.RefugeePolicyRejection.None"/>.</returns>
    public Simulation.Migration.RefugeePolicyRejection SetRefugeePolicyAs(int godId, Domain.Migration.RefugeePolicy policy) =>
        _authorities.Migration.SetPolicy(World, godId, policy, godId, _queue.Pending);

    /// <summary>Sends a group out of a settlement now (the debug tools' forced migration, ADR-016).</summary>
    /// <param name="settlement">The settlement.</param>
    /// <param name="people">How many leave.</param>
    /// <returns>The group, or <c>null</c> when nobody could leave or there is nowhere to go.</returns>
    public Domain.Migration.MigrationGroup? SendMigrants(Domain.Settlements.SettlementState settlement, int people) =>
        Chronicled(() => _engine.Migration!.SendOut(World, settlement, people, _queue.Pending));

    /// <summary>The latest miracle cast still waiting to be drained, if any.</summary>
    /// <returns>The event, or <c>null</c>.</returns>
    public MiracleCast? PeekLastCast() => _queue.Pending.FindLast(e => e is MiracleCast) as MiracleCast;

    /// <summary>Returns the events raised since the last call, oldest first, and forgets them.</summary>
    /// <returns>The events.</returns>
    public IReadOnlyList<WorldEvent> DrainEvents() => _queue.DrainLocal();

    /// <summary>Saves the current world.</summary>
    /// <param name="path">Destination file path.</param>
    public void SaveTo(string path) => WorldSaveSerializer.SaveToFile(World, path);

    /// <summary>Replaces the current world with a saved one. On failure the current world is kept.</summary>
    /// <param name="path">Save file path.</param>
    /// <exception cref="SaveFormatException">The save is invalid.</exception>
    public void LoadFrom(string path)
    {
        var (world, terrain) = SessionStore.LoadWorld(Content, path);
        Replace(world, terrain);
    }

    /// <summary>Swaps in a new world and notifies listeners.</summary>
    private void Replace(WorldState world, TerrainField terrain)
    {
        World = world;
        Terrain = terrain;
        _authorities.Miracles.Land.Shaped = world.Land.Reshapes.Count;
        LocalGodId = FindLocalGod(world);
        _queue.Clear();
        Scheduler.Reset();
        WorldReplaced?.Invoke();
    }

    /// <summary>Runs a command and adds what it raised to the world's history (ADR-020).</summary>
    private T Chronicled<T>(Func<T> command)
    {
        var first = _queue.Pending.Count;
        var result = command();
        _engine.Chronicle(World, _queue.Pending, first);
        if (_authorities.Miracles.Land.Terrain is { } shaped && !ReferenceEquals(shaped, _terrain))
        {
            // A Divine Act reshaped the land and made the terrain again (ADR-045).
            (_terrain, _authorities.Miracles.Land.Shaped) = (shaped, World.Land.Reshapes.Count);
            TerrainChanged?.Invoke();
        }

        return result;
    }

    /// <summary>Runs one simulation tick on the current world; a replica keeps none of its events, which the host sends instead.</summary>
    private void StepWorld()
    {
        if (IsReplica)
        {
            _engine.Step(World, _predicted);
            _predicted.Clear();
            return;
        }

        _engine.Step(World, _queue.Pending);
        _queue.Trim();
    }

    /// <summary>The first god with a human controller.</summary>
    private static int? FindLocalGod(WorldState world) =>
        world.Gods.Find(god => god.Controller == GodController.Human)?.Id;
}
