using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation;
using Pantheon.Client.Presentation.Creatures;
using Pantheon.Client.Presentation.Divine;
using Pantheon.Client.UI;
using Pantheon.Domain.Creatures;
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

    private GameSession _session = null!;
    private BiomePalette _palette = null!;
    private WorldPresenter _presenter = null!;
    private GodCameraController _camera = null!;
    private DebugOverlay _overlay = null!;
    private DivinePresenter _divinePresenter = null!;
    private CreaturePresenter _creatures = null!;
    private DivineController _divine = null!;
    private int _focusIndex = -1;
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
        var options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        ReportLaunchWarnings(options);
        CheckInputMap();
        var loaded = TryLoadContent();
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
        var hand = DivineHand.Create();
        worldNode.AddChild(hand);
        _session = CreateSession(content, options);
        var hud = new Hud { Name = "Hud" };
        AddChild(hud);
        hud.AlertClicked += position => _camera.FocusOn(position, FocusDistance, snap: false);
        hud.CreatureLookClicked += FocusCreature;
        _divine = new DivineController(_session, hud, _divinePresenter, presentation.DivineView, SetStatus, _creatures, hand);
        _overlay.PanelVisible = options.DebugPanel ?? false;
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

    /// <summary>Advances the fixed-step simulation, then refreshes the visuals, the citizens and the overlay.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        _session.Update(delta);
        _presenter.Sync(_session.World);
        _presenter.UpdateCitizens(_session.World, _session.SimulationSeconds, _camera.ViewCamera.GlobalPosition);

        _creatures.Sync(_session.World, (float)delta, !_session.Scheduler.IsPaused);
        if (_pendingJudge is { } judge)
        {
            _pendingJudge = null;
            _divine.JudgeCreature(judge);
        }
        var cursor = _camera.CursorGroundPoint;
        _divine.Process(delta, cursor, _camera.Model.Distance);
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
        (InputActions.CreatureReward, () => _divine.JudgeCreature(CreatureCommandKind.Reward)),
        (InputActions.CreaturePunish, () => _divine.JudgeCreature(CreatureCommandKind.Punish)),
        (InputActions.CreatureSend, () => _divine.SendCreature(_camera.CursorGroundPoint)),
        (InputActions.ToggleMapOverlay, _divine.CycleOverlay),
        (InputActions.DebugGrantPower, () => SetStatus(SessionCommands.GrantPower(_session))),
        (InputActions.SimStepTick, () => SetStatus(SessionCommands.StepTick(_session))),
        (InputActions.SimStepSettlement, () => SetStatus(SessionCommands.StepSettlement(_session))),
        (InputActions.DebugSpoilFood, () => SetStatus(SessionCommands.SpoilFood(_session))),
        (InputActions.DebugAddSettlers, () => SetStatus(SessionCommands.AddSettlers(_session))),
    ];

    /// <summary>One hotkey per slot of the miracle bar.</summary>
    private IEnumerable<(StringName Action, Action Run)> CreateMiracleHotkeys()
    {
        for (var i = 0; i < InputActions.MiracleSlots.Count; i++)
        {
            var slot = i;
            yield return (InputActions.MiracleSlots[slot], () => _divine.SelectSlot(slot));
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
        var session = GameSession.CreateNew(content, seed);
        SetStatus(loadFailure ?? $"New world from seed {seed}");
        return session;
    }

    /// <summary>Rebuilds every visual from the current state and points the camera at the settlement.</summary>
    private void ShowWorld()
    {
        _presenter.Rebuild(_session.World, _session.Terrain);
        _divinePresenter.Rebuild(_session.Terrain, _session.World.Regions.Columns * _session.World.Regions.RegionSize);
        _creatures.Rebuild(_session.Terrain);
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
        if (options.DivinePower is { } power && _session.LocalGod is { } god)
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
    }

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
        LocalGodId = _session.LocalGodId,
        LocalCreatureId = _session.LocalCreature?.Id,
        CreatureOptions = _session.PreviewCreatureOptions(),
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
    };

    /// <summary>One-line world summary printed at the end of unattended runs.</summary>
    private string DescribeWorld() =>
        $"{DebugReadout.Summarize(_session.World, _session.Content.SettlementRules, _presenter.CrowdStatuses)}; terrain {_presenter.TerrainBackend}";

    /// <summary>Remembers a status line for the overlay and echoes it to the log.</summary>
    private void SetStatus(string message)
    {
        _status = message;
        GD.Print(message);
    }

    /// <summary>A fresh seed for F2. Only the shell picks random seeds; the simulation never does.</summary>
    private static WorldSeed CreateRandomSeed() => new((ulong)Random.Shared.NextInt64(1, MaxRandomSeed));

    /// <summary>Loads the simulation and presentation content, logging the reason on failure.</summary>
    private static (GameContent Content, PresentationContent Presentation)? TryLoadContent()
    {
        try
        {
            return (GodotContentLoader.LoadGameContent(), GodotContentLoader.LoadPresentationContent());
        }
        catch (Exception exception) when (exception is ContentFormatException or IOException)
        {
            GD.PushError($"Game content could not be loaded: {exception.Message}");
            return null;
        }
    }

    /// <summary>Logs problems found in the command-line options.</summary>
    private static void ReportLaunchWarnings(LaunchOptions options)
    {
        foreach (var warning in options.Warnings)
        {
            GD.PushWarning($"Launch options: {warning}");
        }
    }

    /// <summary>Reports actions that are referenced in code but missing from <c>project.godot</c>.</summary>
    private static void CheckInputMap()
    {
        foreach (var action in InputActions.All)
        {
            if (!InputMap.HasAction(action))
            {
                GD.PushError($"Input action '{action}' is missing from project.godot.");
            }
        }
    }
}
