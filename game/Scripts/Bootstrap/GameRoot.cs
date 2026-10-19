using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation;
using Pantheon.Client.Presentation.Creatures;
using Pantheon.Client.Presentation.Disasters;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Client.Presentation.Weather;
using Pantheon.Client.UI;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Composition root of the client (<c>Main.tscn</c>). It loads the content, creates the
/// <see cref="GameSession"/> from the launch options, wires presenters, camera and overlay, drives
/// the session every frame and handles the global hotkeys.
/// </summary>
public partial class GameRoot : Node3D
{
    private const string QuickSavePath = "user://saves/quicksave.json";
    private const float FocusDistance = 220f;

    // C brings the camera this close to the Creature.
    private const float CreatureFocusDistance = 90f;

    // The debug panel details the settlement nearest the cursor within this distance.
    private const float SelectDistance = 250f;

    // F2 seeds stay short enough to read off the overlay and pass back with --seed.
    private const long MaxRandomSeed = 1_000_000_000;

    // The island the fog was tuned for, in metres across.
    private const float FogReferenceExtent = 2048f;

    private GameSession _session = null!;
    private BiomePalette _palette = null!;
    private WorldPresenter _presenter = null!;
    private GodCameraController _camera = null!;
    private DebugOverlay _overlay = null!;
    private DivinePresenter _divinePresenter = null!;
    private CreaturePresenter _creatures = null!;
    private WeatherPresenter _weather = null!;
    private DisasterPresenter _disasters = null!;
    private Pantheon.Client.Presentation.Migration.MigrationPresenter _migrants = null!;
    private NatureDebug _nature = null!;
    private Pantheon.Client.Network.NetworkDriver _network = null!;
    private DiplomacyPanel _diplomacy = null!;
    private DivineController _divine = null!;
    private int _focusIndex = -1;
    private float? _baseFogDensity;
    private CreatureCommandKind? _pendingJudge;
    private IReadOnlyList<(StringName Action, Action Run)> _hotkeys = [];
    private string? _status;
    private RegionDebugLayer? _regionMapLayer;
    private (RegionDebugLayer Layer, int? CursorRegion, long Tick, WorldState World)? _shownRegionMap;

    /// <summary>Absolute path of the quicksave file.</summary>
    private static string GlobalQuickSavePath => ProjectSettings.GlobalizePath(QuickSavePath);

    /// <summary>Builds the session and the world visuals. Quits with exit code 1 when the content is broken.</summary>
    public override void _Ready()
    {
        var arguments = PendingLaunch.Take();
        var options = LaunchOptions.Parse(arguments);
        Startup.ReportLaunchWarnings(options);
        Startup.CheckInputMap();
        var settings = Startup.LoadSettings();
        var loaded = Startup.TryLoadContent(settings);
        if (loaded is null)
        {
            SetProcess(false);
            SetProcessUnhandledInput(false);
            GetTree().Quit(1);
            return;
        }

        var (content, presentation) = loaded.Value;
        _palette = presentation.Palette;
        _camera = GetNode<GodCameraController>("GodCamera");
        _overlay = GetNode<DebugOverlay>("DebugOverlay");
        _presenter = new WorldPresenter(GetNode<Node3D>("World"), _camera.ViewCamera, presentation, content.SettlementRules, options.Terrain);
        var worldNode = GetNode<Node3D>("World");
        _divinePresenter = new DivinePresenter(worldNode.AddLayer("Divine"), presentation.DivineView);
        _creatures = new CreaturePresenter(worldNode.AddLayer("Creatures"), presentation.DivineView);
        _weather = new WeatherPresenter(worldNode.AddLayer("Weather"), presentation.WeatherView, content.Weather, GetNodeOrNull<DirectionalLight3D>("Sun"), GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment);
        _disasters = new DisasterPresenter(worldNode.AddLayer("Disasters"), _camera, content.Disasters.Hazards);
        _migrants = new Pantheon.Client.Presentation.Migration.MigrationPresenter(worldNode.AddLayer("Migrants"), presentation.DivineView);
        var hand = DivineHand.Create();
        worldNode.AddChild(hand);
        _network = new Pantheon.Client.Network.NetworkDriver(SetStatus);
        _session = _network.Join(content, options) ?? CreateSession(content, options);
        _network.Host(_session, options);
        _nature = new NatureDebug(_session, _camera, SetStatus);
        var hud = new Hud { Name = "Hud" };
        AddChild(hud);
        hud.AlertClicked += position => _camera.FocusOn(position, FocusDistance, snap: false);
        hud.CreatureLookClicked += FocusCreature;
        _divine = new DivineController(_session, hud, _divinePresenter, presentation.DivineView, SetStatus, _creatures, hand);
        _diplomacy = new DiplomacyPanel { Name = "Diplomacy" };
        AddChild(_diplomacy);
        _diplomacy.CommandClicked += (god, kind) => SetStatus(SessionCommands.CommandDiplomacy(_session, god, kind));
        _diplomacy.PolicyClicked += policy => SetStatus(Pantheon.Game.Migration.MigrationCommands.SetPolicy(_session, policy));
        CallDeferred(nameof(ShowDiplomacyAtStart), options.DiplomacyPanel);
        var flow = new GameFlow { Name = "GameFlow" };
        AddChild(flow);
        flow.Setup(_session, _network, _divine, _diplomacy, SetStatus, options, settings);
        var audio = new Pantheon.Client.Presentation.Audio.AudioPresenter { Name = "Audio" };
        AddChild(audio);
        audio.Setup(_session, () => new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y), point => _session.Terrain.SampleHeight(point.X, point.Z), settings);
        _divine.EventsHandled += audio.Hear;
        SettingsApplier.Apply(settings, GetViewport(), GetNodeOrNull<DirectionalLight3D>("Sun"), startup: true);
        var faith = new FaithController { Name = "Faith" };
        AddChild(faith);
        faith.Setup(_session, worldNode.AddLayer("HolySites"), presentation.DivineView, _camera, SetStatus, options);
        faith.ActChosen += _divine.Select;
        var book = new BookController { Name = "BookOfTheWorld" };
        AddChild(book);
        book.Setup(_session, options);
        book.MiracleAimed += _divine.Select;
        var politics = new PoliticsController { Name = "Politics" };
        AddChild(politics);
        politics.Setup(_session, _camera, _diplomacy, SetStatus, options);
        var society = new SocietyController { Name = "Society" };
        AddChild(society);
        society.Setup(_session, _camera, SetStatus, options);
        _overlay.PanelVisible = options.DebugPanel ?? false;
        _session.Scheduler.TimeScale = options.Speed ?? _session.Scheduler.TimeScale;
        if (options.AdvanceTicks is { } ticks)
        {
            SetStatus(SessionCommands.AdvanceTicks(_session, ticks));
        }

        _session.WorldReplaced += ShowWorld;
        _hotkeys = CreateHotkeys();

        ShowWorld();
        ApplyDivineOptions(options);
        if (options.FocusCreature && _session.LocalCreature is { } creature)
        {
            _camera.FocusOn(creature.Position, options.CameraDistance ?? CreatureFocusDistance, snap: true);
        }
        else if (options.CameraDistance is { } distance)
        {
            FocusSettlement(distance, snap: true);
        }

        _overlay.SetFallbackBadgeVisible(_presenter.TerrainIsFallback);
        if (options.QuitAfterFrames is { } frames)
        {
            _camera.EdgeScrollEnabled = false;
            _camera.CenterCursorWhenAway = true;
            AddChild(SmokeTestDriver.Create(frames, options.ScreenshotPath, DescribeWorld));
        }
    }

    /// <summary>Ends a shared session on the way out: a host saves its world for later (spec §71).</summary>
    public override void _ExitTree() => _network?.Shutdown(_session, ProjectSettings.GlobalizePath("user://saves/hosted.json"));

    /// <summary>Advances the fixed-step simulation, then refreshes the visuals, the citizens and the overlay.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        _network.Update(delta, new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y));
        _session.Update(delta);
        _presenter.Sync(_session.World, delta);
        _presenter.UpdateCitizens(_session.World, _session.SimulationSeconds, _camera.ViewCamera.GlobalPosition);

        _creatures.Sync(_session.World, (float)delta, !_session.Scheduler.IsPaused, _session.LocalGodId, _divine.HeldCreatureAt(_camera.CursorGroundPoint));
        var focus = new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y);
        _weather.Sync(_session.World, _session.SimulationSeconds, (float)delta, focus);
        _disasters.Sync(_session.World, (float)delta, focus);
        _migrants.Sync(_session.World, (float)delta, !_session.Scheduler.IsPaused);
        LabelDeclutterer.Apply(GetTree(), _camera.ViewCamera);
        if (_pendingJudge is { } judge)
        {
            _pendingJudge = null;
            _divine.JudgeCreature(judge);
        }
        var cursor = _camera.CursorGroundPoint;
        _divine.Process(delta, cursor, _camera.Model.Distance);
        if (_diplomacy.PanelVisible)
        {
            _diplomacy.ShowEntries(Pantheon.Game.Diplomacy.DiplomacyReadout.Build(_session, _diplomacy.Choice), _session.LocalGod?.RefugeePolicy);
        }

        var cursorRegion = RegionAt(cursor);
        _presenter.SetHoveredRegion(cursorRegion);
        UpdateRegionMap(cursorRegion);
        if (_overlay.PanelVisible)
        {
            _overlay.ShowModel(DebugReadout.Build(CreateSnapshot(cursor)));
        }
    }

    /// <summary>Runs the first hotkey that matches a newly pressed action.</summary>
    /// <param name="event">Input event not consumed by the UI or the camera.</param>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.MiracleCast) && _divine.TryCast(_camera.CursorGroundPoint))
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        foreach (var (action, run) in _hotkeys)
        {
            if (@event.IsActionPressed(action))
            {
                run();
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    /// <summary>Hotkey table: action name and what it does.</summary>
    private IReadOnlyList<(StringName Action, Action Run)> CreateHotkeys() =>
    [
        (InputActions.ToggleRegionBoundaries, () => _presenter.RegionBoundariesVisible = !_presenter.RegionBoundariesVisible),
        (InputActions.ToggleDebugOverlay, () => _overlay.PanelVisible = !_overlay.PanelVisible),
        (InputActions.ToggleRegionMap, CycleRegionMap),
        (InputActions.CameraFocusSettlement, FocusNextSettlement),
        (InputActions.WorldNew, () => SetStatus(SessionCommands.NewWorld(_session, CreateRandomSeed()))),
        (InputActions.WorldQuickSave, () => SetStatus(SessionCommands.QuickSave(_session, GlobalQuickSavePath))),
        (InputActions.WorldQuickLoad, () => SetStatus(SessionCommands.QuickLoad(_session, GlobalQuickSavePath))),
        (InputActions.SimTogglePause, () => SetStatus(SessionCommands.TogglePause(_session))),
        (InputActions.SimSpeedDown, () => SetStatus(SessionCommands.ChangeSpeed(_session, -1))),
        (InputActions.SimSpeedUp, () => SetStatus(SessionCommands.ChangeSpeed(_session, 1))),
        .. CreateMiracleHotkeys(),
        (InputActions.MiracleCancel, () => _divine.Select(null)),
        (InputActions.CreatureFocus, FocusCreature),
        (InputActions.ToggleDiplomacy, () => _diplomacy.PanelVisible = !_diplomacy.PanelVisible),
        (InputActions.CreatureReward, () => _divine.JudgeCreature(CreatureCommandKind.Reward)),
        (InputActions.CreaturePunish, () => _divine.JudgeCreature(CreatureCommandKind.Punish)),
        (InputActions.CreatureSend, () => _divine.SendCreature(_camera.CursorGroundPoint)),
        (InputActions.HandGrab, () => _divine.UseHand(_camera.CursorGroundPoint, Input.IsKeyPressed(Key.Shift))),
        (InputActions.ToggleMapOverlay, _divine.CycleOverlay),
        (InputActions.DebugGrantPower, () => SetStatus(SessionCommands.GrantPower(_session))),
        (InputActions.SimStepTick, () => SetStatus(SessionCommands.StepTick(_session))),
        (InputActions.SimStepSettlement, () => SetStatus(SessionCommands.StepSettlement(_session))),
        (InputActions.DebugSpoilFood, () => SetStatus(SessionCommands.SpoilFood(_session))),
        (InputActions.DebugAddSettlers, () => SetStatus(SessionCommands.AddSettlers(_session))),
        .. _nature.Hotkeys(),
    ];

    /// <summary>One hotkey per slot of the miracle bar; Shift with 1–4 selects the slots after the tenth: Anoint a Prophet and the domains' miracles (ADR-021, ADR-032).</summary>
    private IEnumerable<(StringName Action, Action Run)> CreateMiracleHotkeys()
    {
        for (var i = 0; i < InputActions.MiracleSlots.Count; i++)
        {
            var slot = i;
            yield return (InputActions.MiracleSlots[slot], () => _divine.SelectSlot(Input.IsKeyPressed(Key.Shift) && slot < 4 ? InputActions.MiracleSlots.Count + slot : slot));
        }
    }

    /// <summary>Loads the save given by <c>--load</c>, or generates a world from <c>--seed</c> (or the default seed).</summary>
    private GameSession CreateSession(GameContent content, LaunchOptions options)
    {
        string? loadFailure = null;
        if (options.LoadPath is { } loadPath)
        {
            try
            {
                var loaded = GameSession.Load(content, ProjectSettings.GlobalizePath(loadPath));
                SetStatus(string.Create(CultureInfo.InvariantCulture, $"Loaded {loadPath} at tick {loaded.World.Tick}"));
                return loaded;
            }
            catch (Exception exception) when (exception is SaveFormatException or IOException or UnauthorizedAccessException)
            {
                loadFailure = $"Could not load {loadPath}, generated a new world instead: {exception.Message}";
                GD.PushWarning(loadFailure);
            }
        }

        var seed = options.Seed ?? LaunchOptions.DefaultSeed;
        var world = options.NewWorld(content);
        var session = GameSession.CreateNew(content, seed, world);
        SetStatus(loadFailure ?? SessionCommands.Describe(content, seed, world));
        return session;
    }

    /// <summary>Rebuilds every visual from the current state and points the camera at the settlement.</summary>
    private void ShowWorld()
    {
        // A larger island thins the fog, so its far shore still shows from the camera's greater height (ADR-027).
        var extent = _session.World.Regions.Columns * _session.World.Regions.RegionSize;
        if (GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment is { } environment)
        {
            _baseFogDensity ??= environment.FogDensity;
            environment.FogDensity = _baseFogDensity.Value * MathF.Min(1f, FogReferenceExtent / extent);
        }

        _presenter.Rebuild(_session.World, _session.Terrain);
        _divinePresenter.Rebuild(_session.Terrain, _session.World.Regions.Columns * _session.World.Regions.RegionSize);
        _creatures.Rebuild(_session.Terrain);
        _weather.Rebuild(_session.Terrain);
        _disasters.Rebuild(_session.Terrain, _session.World.Regions.RegionSize);
        _migrants.Rebuild(_session.Terrain);
        _divine?.Reset();
        _focusIndex = -1;
        _camera.SetTerrain(_session.Terrain);
        FocusSettlement(distance: null, snap: true);
    }

    /// <summary>Moves the camera to the focused settlement (the first unless <c>--focus</c> chose another).</summary>
    /// <param name="distance">New camera distance, or <c>null</c> to keep the zoom.</param>
    /// <param name="snap">Jump there immediately instead of gliding.</param>
    private void FocusSettlement(float? distance, bool snap)
    {
        var settlements = _session.World.Settlements;
        if (settlements.Count > 0)
        {
            _camera.FocusOn(settlements[Math.Clamp(_focusIndex, 0, settlements.Count - 1)].Position, distance, snap);
        }
    }

    /// <summary>Applies the divine debug options: starting power, focus, casts and overlay.</summary>
    private void ApplyDivineOptions(LaunchOptions options)
    {
        if (options.DivinePower is { } power && _session.LocalGod is { } god && !_session.IsReplica)
        {
            god.DivinePower = power;
        }

        if (options.FocusSettlement is { } focus && _session.World.Settlements.Count > 0)
        {
            _focusIndex = Math.Min(focus, _session.World.Settlements.Count) - 1;
            FocusSettlement(distance: null, snap: true);
        }

        foreach (var miracle in options.CastAtStart)
        {
            var target = _session.World.Settlements.Count > 0 ? _session.World.Settlements[Math.Max(0, _focusIndex)].Position : default;
            SetStatus(SessionCommands.CastMiracle(_session, miracle, target));
        }

        _pendingJudge = options.Judge;
        if (options.SelectMiracle is { } selected)
        {
            _divine.Select(selected);
        }

        while (_divine.Overlay != options.Overlay && options.Overlay is not null)
        {
            _divine.CycleOverlay();
        }

        var settlements = _session.World.Settlements;
        _nature.ApplyStartOptions(options, settlements.Count > 0 ? settlements[Math.Max(0, _focusIndex)].Position : null);
    }

    /// <summary>Shows the diplomacy panel once it is ready, when the launch options ask for it.</summary>
    /// <param name="visible">Whether to show it.</param>
    private void ShowDiplomacyAtStart(bool visible) => _diplomacy.PanelVisible = visible;

    /// <summary>Glides to the player's Creature (spec §56: focus Creature).</summary>
    private void FocusCreature()
    {
        if (_session.LocalCreature is { } creature)
        {
            _camera.FocusOn(creature.Position, CreatureFocusDistance, snap: false);
            SetStatus($"Looking at {creature.Name}");
        }
    }

    /// <summary>Glides to the next settlement in turn, starting with the player's home.</summary>
    private void FocusNextSettlement()
    {
        var settlements = _session.World.Settlements;
        if (settlements.Count == 0)
        {
            return;
        }

        _focusIndex = (_focusIndex + 1) % settlements.Count;
        _camera.FocusOn(settlements[_focusIndex].Position, FocusDistance, snap: false);
        SetStatus($"Looking at {settlements[_focusIndex].Name}");
    }

    /// <summary>Shows the region debug map's next layer, or hides the map after the last one.</summary>
    private void CycleRegionMap()
    {
        _regionMapLayer = RegionDebugMap.Next(_regionMapLayer);
        SetStatus(_regionMapLayer is { } layer ? $"Debug map: {layer}" : "Debug map off");
    }

    /// <summary>Redraws the region debug map when its layer, the cursor region, the tick or the world changed.</summary>
    /// <param name="cursorRegion">Region under the cursor, if any.</param>
    private void UpdateRegionMap(int? cursorRegion)
    {
        (RegionDebugLayer Layer, int? CursorRegion, long Tick, WorldState World)? wanted = _regionMapLayer is { } layer
            ? (layer, cursorRegion, _session.World.Tick, _session.World)
            : null;
        if (wanted == _shownRegionMap)
        {
            return;
        }

        _shownRegionMap = wanted;
        _overlay.ShowRegionMap(wanted is { } map ? RegionDebugMap.Build(map.World, map.Layer, _palette, map.CursorRegion, InfluenceSamples(map.Layer)) : null);
    }

    /// <summary>The local god's influence per region, for the influence layer of the debug map.</summary>
    private float[]? InfluenceSamples(RegionDebugLayer layer) =>
        layer == RegionDebugLayer.Influence && _session.LocalGodId is { } god
            ? Pantheon.Simulation.Belief.InfluenceField.Build(_session.Content.DivineRules, _session.World, god).SampleRegions(_session.World.Regions)
            : null;

    /// <summary>Region containing a world point, if any.</summary>
    private int? RegionAt(NVector3? point) =>
        point is { } p && _session.World.Regions.TryGetRegionIdAt(p.X, p.Z, out var regionId) ? regionId : null;

    /// <summary>Collects this frame's data for the debug overlay.</summary>
    private DebugSnapshot CreateSnapshot(NVector3? cursor) => new()
    {
        FramesPerSecond = Engine.GetFramesPerSecond(),
        World = _session.World,
        SettlementRules = _session.Content.SettlementRules,
        DivineRules = _session.Content.DivineRules,
        CreatureRules = _session.Content.Creatures,
        WeatherRules = _session.Content.Weather,
        DisasterRules = _session.Content.Disasters,
        MigrationRules = _session.Content.Migration,
        ConflictRules = _session.Content.Diplomacy.Conflict,
        MigrationPressure = PressureUnder(cursor),
        RefugeeChoices = [.. _session.World.Gods.Select(god => (god.Id, _session.PreviewRefugeePolicy(god))).Where(c => c.Item2 is not null).Select(c => (c.Id, c.Item2!.Value.Policy, c.Item2.Value.Reason))],
        LocalGodId = _session.LocalGodId,
        LocalCreatureId = _session.LocalCreature?.Id,
        CreatureOptions = _session.PreviewCreatureOptions(),
        CreatureResponses = Pantheon.Game.Creatures.CreatureCommands.PreviewResponses(_session),
        NetworkLines = _network.Lines(_session),
        GodDiplomacyCandidates = _session.World.Gods
            .Where(god => god.Controller == GodController.Ai)
            .Select(god => (god.Id, _session.PreviewGodDiplomacy(god)))
            .ToList(),
        GodAiCandidates = _session.World.Gods
            .Where(god => god.Controller == GodController.Ai)
            .Select(god => (god.Id, _session.PreviewGodAi(god)))
            .ToList(),
        GodGoalOptions = [.. _session.World.Gods.Where(god => god.Controller == GodController.Ai).Select(god => (god.Id, _session.PreviewGodGoals(god)))],
        SelectedSettlementId = cursor is { } point
            ? SettlementPicker.Nearest(_session.World, new GroundPosition(point.X, point.Z), SelectDistance)?.Id
            : null,
        IsPaused = _session.Scheduler.IsPaused,
        TimeScale = _session.Scheduler.TimeScale,
        TicksPerSecond = _session.Scheduler.TicksPerSecond,
        TerrainBackend = _presenter.TerrainBackend,
        TerrainIsFallback = _presenter.TerrainIsFallback,
        ZoomBand = _camera.Model.Band,
        CameraDistance = _camera.Model.Distance,
        CursorWorldPosition = cursor,
        RegionBoundariesVisible = _presenter.RegionBoundariesVisible,
        StatusMessage = _status,
        Crowds = _presenter.CrowdStatuses,
        HolySiteRules = _session.Content.HolySites,
        WaterRules = _session.Content.Water,
    };

    /// <summary>The pressure to leave on the settlement near the cursor, for the debug panel.</summary>
    private Pantheon.Simulation.Migration.MigrationPressure? PressureUnder(NVector3? cursor) =>
        cursor is { } point && SettlementPicker.Nearest(_session.World, new GroundPosition(point.X, point.Z), SelectDistance) is { } settlement
            ? _session.MigrationPressureOn(settlement)
            : null;

    /// <summary>One-line world summary printed at the end of unattended runs.</summary>
    private string DescribeWorld() =>
        $"{DebugReadout.Summarize(_session.World, _session.Content.SettlementRules, _presenter.CrowdStatuses)}; terrain {_presenter.TerrainBackend}"
        + string.Concat(_network.Lines(_session).Select(line => "\n" + line));

    /// <summary>Remembers a status line for the overlay and echoes it to the log.</summary>
    private void SetStatus(string message)
    {
        _status = message;
        GD.Print(message);
    }

    /// <summary>A fresh seed for F2. Only the shell picks random seeds; the simulation never does.</summary>
    private static WorldSeed CreateRandomSeed() => new((ulong)Random.Shared.NextInt64(1, MaxRandomSeed));
}
