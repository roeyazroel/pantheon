using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;
using Pantheon.Client.Camera;
using Pantheon.Client.Presentation;
using Pantheon.Client.UI;
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

    // F2 seeds stay short enough to read off the overlay and pass back with --seed.
    private const long MaxRandomSeed = 1_000_000_000;

    private GameSession _session = null!;
    private BiomePalette _palette = null!;
    private WorldPresenter _presenter = null!;
    private GodCameraController _camera = null!;
    private DebugOverlay _overlay = null!;
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
        _session = CreateSession(content, options);
        if (options.AdvanceTicks is { } ticks)
        {
            SetStatus(SessionCommands.AdvanceTicks(_session, ticks));
        }

        _session.WorldReplaced += ShowWorld;
        _hotkeys = CreateHotkeys();

        ShowWorld();
        if (options.CameraDistance is { } distance)
        {
            FocusSettlement(distance, snap: true);
        }

        _overlay.SetFallbackBadgeVisible(_presenter.TerrainIsFallback);
        if (options.QuitAfterFrames is { } frames)
        {
            _camera.EdgeScrollEnabled = false;
            AddChild(SmokeTestDriver.Create(frames, options.ScreenshotPath, DescribeWorld));
        }
    }

    /// <summary>Advances the fixed-step simulation, then refreshes the visuals and the overlay.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        _session.Update(delta);
        _presenter.Sync(_session.World);

        var cursor = _camera.CursorGroundPoint;
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
        (InputActions.CameraFocusSettlement, () => FocusSettlement(FocusDistance, snap: false)),
        (InputActions.WorldNew, () => SetStatus(SessionCommands.NewWorld(_session, CreateRandomSeed()))),
        (InputActions.WorldQuickSave, () => SetStatus(SessionCommands.QuickSave(_session, GlobalQuickSavePath))),
        (InputActions.WorldQuickLoad, () => SetStatus(SessionCommands.QuickLoad(_session, GlobalQuickSavePath))),
        (InputActions.SimTogglePause, () => SetStatus(SessionCommands.TogglePause(_session))),
        .. CreateSpeedHotkeys(),
        (InputActions.SimStepTick, () => SetStatus(SessionCommands.StepTick(_session))),
        (InputActions.SimStepSettlement, () => SetStatus(SessionCommands.StepSettlement(_session))),
        (InputActions.DebugSpoilFood, () => SetStatus(SessionCommands.SpoilFood(_session))),
        (InputActions.DebugAddSettlers, () => SetStatus(SessionCommands.AddSettlers(_session))),
    ];

    /// <summary>One hotkey per <see cref="SessionCommands.SpeedPresets"/> entry.</summary>
    private IEnumerable<(StringName Action, Action Run)> CreateSpeedHotkeys()
    {
        for (var i = 0; i < InputActions.SimSpeedPresets.Count; i++)
        {
            var preset = i;
            yield return (InputActions.SimSpeedPresets[preset], () => SetStatus(SessionCommands.SetSpeedPreset(_session, preset)));
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
        _camera.SetTerrain(_session.Terrain);
        FocusSettlement(distance: null, snap: true);
    }

    /// <summary>Moves the camera to the first settlement.</summary>
    /// <param name="distance">New camera distance, or <c>null</c> to keep the zoom.</param>
    /// <param name="snap">Jump there immediately instead of gliding.</param>
    private void FocusSettlement(float? distance, bool snap)
    {
        if (_session.World.Settlements.Count > 0)
        {
            _camera.FocusOn(_session.World.Settlements[0].Position, distance, snap);
        }
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
        _overlay.ShowRegionMap(wanted is { } map ? RegionDebugMap.Build(map.World, map.Layer, _palette, map.CursorRegion) : null);
    }

    /// <summary>Region containing a world point, if any.</summary>
    private int? RegionAt(NVector3? point) =>
        point is { } p && _session.World.Regions.TryGetRegionIdAt(p.X, p.Z, out var regionId) ? regionId : null;

    /// <summary>Collects this frame's data for the debug overlay.</summary>
    private DebugSnapshot CreateSnapshot(NVector3? cursor) => new()
    {
        FramesPerSecond = Engine.GetFramesPerSecond(),
        World = _session.World,
        SettlementRules = _session.Content.SettlementRules,
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
    };

    /// <summary>One-line world summary printed at the end of unattended runs.</summary>
    private string DescribeWorld() =>
        $"{DebugReadout.Summarize(_session.World, _session.Content.SettlementRules)}; terrain {_presenter.TerrainBackend}";

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
