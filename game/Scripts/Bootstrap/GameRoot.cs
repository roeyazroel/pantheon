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
using Pantheon.Game.Performance;
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
    private const float FocusDistance = 220f;

    // C brings the camera this close to the Creature, and aims at this share of its height (ADR-058).
    private const float CreatureFocusDistance = 60f;
    private const float CreatureAimShare = 0.5f;

    // How far from where the hand presses debris is knocked away, in metres (ADR-057).
    private const float PushReach = 12f;


    // The island the fog was tuned for, in metres across.
    private const float FogReferenceExtent = 2048f;

    private GameSession _session = null!;
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
    private DebugView _debug = null!;
    private Hud? _hud;
    private GameFlow _flow = null!;


    /// <summary>Builds the session and the world visuals. Quits with exit code 1 when the content is broken.</summary>
    public override void _Ready()
    {
        UiTheme.Apply();
        var arguments = PendingLaunch.Take();
        var options = LaunchOptions.Parse(arguments);
        Startup.ReportLaunchWarnings(options);
        Startup.CheckInputMap();
        var settings = Startup.LoadSettings();
        GD.Print($"Profile kept in {Startup.Profile.Kind}");
        var loaded = Startup.TryLoadContent(settings);
        if (loaded is null)
        {
            SetProcess(false);
            SetProcessUnhandledInput(false);
            GetTree().Quit(1);
            return;
        }

        var (content, presentation) = options.Stress ? (StressScenario.Content(loaded.Value.Content), StressScenario.Presentation(loaded.Value.Presentation)) : loaded.Value;
        _camera = GetNode<GodCameraController>("GodCamera");
        _overlay = GetNode<DebugOverlay>("DebugOverlay");
        _presenter = new WorldPresenter(GetNode<Node3D>("World"), _camera.ViewCamera, presentation, content.SettlementRules, options.Terrain);
        var worldNode = GetNode<Node3D>("World");
        _divinePresenter = new DivinePresenter(worldNode.AddLayer("Divine"), presentation.DivineView);
        _creatures = new CreaturePresenter(worldNode.AddLayer("Creatures"), presentation.DivineView);
        _weather = new WeatherPresenter(worldNode.AddLayer("Weather"), presentation.WeatherView, content.Weather, GetNodeOrNull<DirectionalLight3D>("Sun"), GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment);
        _disasters = new DisasterPresenter(worldNode.AddLayer("Disasters"), _camera, content.Disasters.Hazards, presentation.SettlementView.Streaming.EffectRadius);
        _migrants = new Pantheon.Client.Presentation.Migration.MigrationPresenter(worldNode.AddLayer("Migrants"), presentation.DivineView, Pantheon.Client.Presentation.People.PersonAssets.For(presentation.SettlementView));
        var hand = DivineHand.Create();
        worldNode.AddChild(hand);
        hand.Pressed += point => _presenter.PushDebris(point, PushReach);
        _camera.Roofs = _presenter.RoofAt;
        _camera.FollowEnded += () => SetStatus("The camera is free again");
        _network = new Pantheon.Client.Network.NetworkDriver(SetStatus);
        var minds = Startup.TryLoadMinds();
        _session = _network.Join(content, options) ?? CreateSession(content, minds, options);
        _network.Host(_session, options);
        _debug = new DebugView(_session, _presenter, _camera, _overlay, _network, presentation.Palette, () => _disasters.Fires, presentation.SettlementView.Streaming, presentation.SettlementView.GroundCover.Radius);
        _nature = new NatureDebug(_session, _camera, SetStatus);
        var view = presentation.DivineView;
        var hud = new Hud
        {
            Name = "Hud",
            MiracleLook = id => (MiracleGlyphs.For(id, view.EffectFor(id).Style), view.EffectFor(id).Color.ToGodot()),
            CooldownOf = id => (content.Miracles.Find(id)?.CooldownTicks ?? 0) / SimulationScheduler.DefaultTicksPerSecond,
            GodColor = id => view.GodColor(id).ToGodot(),
            LocalGod = () => _session.LocalGodId,
        };
        AddChild(hud);
        _hud = hud;
        hud.PauseClicked += () => SetStatus(SessionCommands.TogglePause(_session));
        hud.SpeedClicked += preset => SetStatus(SessionCommands.SetSpeedPreset(_session, preset));
        hud.AlertClicked += position => _camera.FocusOn(position, FocusDistance, snap: false);
        hud.CreatureLookClicked += FocusCreature;
        _divine = new DivineController(_session, hud, _divinePresenter, presentation.DivineView, SetStatus, _creatures, hand);
        _camera.LeftGrabAllowed = () => _divine.SelectedMiracle is null && !_divine.SendMode && !_divine.FightMode && !_divine.WorkMode;
        _diplomacy = new DiplomacyPanel { Name = "Diplomacy" };
        AddChild(_diplomacy);
        _diplomacy.CommandClicked += (god, kind) => SetStatus(SessionCommands.CommandDiplomacy(_session, god, kind));
        _diplomacy.PolicyClicked += policy => SetStatus(Pantheon.Game.Migration.MigrationCommands.SetPolicy(_session, policy));
        CallDeferred(nameof(ShowDiplomacyAtStart), options.DiplomacyPanel);
        var flow = new GameFlow { Name = "GameFlow" };
        AddChild(flow);
        flow.Setup(_session, _network, _divine, (_diplomacy, hud), _camera, SetStatus, options, (settings, minds));
        _flow = flow;
        var audio = new Pantheon.Client.Presentation.Audio.AudioPresenter { Name = "Audio" };
        AddChild(audio);
        audio.Setup(_session, () => new GroundPosition(_camera.Model.Focus.X, _camera.Model.Focus.Y), point => _session.Terrain.SampleHeight(point.X, point.Z), settings);
        _divine.EventsHandled += audio.Hear;
        _divine.EventsHandled += _presenter.React;
        SettingsApplier.Apply(settings, GetViewport(), GetNodeOrNull<DirectionalLight3D>("Sun"), startup: true, GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment, _session, _camera);
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
        var villagers = new VillagerController { Name = "Villagers" };
        AddChild(villagers);
        villagers.Setup(_session, _camera, worldNode.AddLayer("Villagers"), presentation, SetStatus, options);
        _overlay.PanelVisible = options.DebugPanel ?? false;
        _session.Scheduler.TimeScale = options.Speed ?? _session.Scheduler.TimeScale;
        if (options.AdvanceTicks is { } ticks)
        {
            SetStatus(SessionCommands.AdvanceTicks(_session, ticks));
        }

        _session.WorldReplaced += ShowWorld;
        _session.TerrainChanged += () => Rebuild(newWorld: false);
        _hotkeys = CreateHotkeys();

        ShowWorld();
        ApplyDivineOptions(options);
        if (options.FocusCreature && _session.LocalCreature is { } creature)
        {
            _camera.Model.FaceFront(creature.Heading);
            _camera.FocusOn(creature.Position, options.CameraDistance ?? CreatureFocusDistance, snap: true);
            FollowCreature(creature);
        }
        else if (options.CameraDistance is { } distance)
        {
            FocusSettlement(distance, snap: true);
        }

        if (options.FocusWildlife && Wildlife.NearestDeer(_session.World, _session.Terrain, new System.Numerics.Vector2(_camera.Model.Focus.X, _camera.Model.Focus.Y), _session.Crowds.Seconds, presentation.SettlementView.Wildlife) is { } deer)
        {
            _camera.FocusOn(new GroundPosition(deer.X, deer.Z), options.CameraDistance ?? 18f, snap: true);
        }

        if (options.Stress && !_session.IsReplica)
        {
            SetStatus(StressScenario.Apply(_session));
            _camera.FocusOn(StressScenario.Busiest(_session.World), options.CameraDistance ?? StressScenario.CameraDistance, snap: true);
        }

        _overlay.SetFallbackBadgeVisible(_presenter.TerrainIsFallback);
        if (options.QuitAfterFrames is { } frames)
        {
            _camera.EdgeScrollEnabled = false;
            _camera.LeftGrabAllowed = static () => false;
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
        _debug.Performance.Simulate(_session, delta);
        _debug.Performance.Frame(delta);
        _presenter.Sync(_session.World, delta);
        var crowds = _session.Crowds.Advance(delta, _session.Scheduler.TimeScale, _session.Scheduler.IsPaused, _presenter.CrowdPace);
        _presenter.UpdateCitizens(_session.World, crowds, _camera.ViewCamera.GlobalPosition, delta);

        _creatures.Sync(_session.World, (float)delta, !_session.Scheduler.IsPaused, _session.LocalGodId, _divine.HeldCreatureAt(_camera.CursorGroundPoint), (float)_session.Scheduler.Alpha);
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
        _debug.Update(cursor, cursorRegion, _status, delta);
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
        (InputActions.ToggleRegionMap, () => SetStatus(_debug.CycleRegionMap())),
        (InputActions.CameraFocusSettlement, FocusNextSettlement),
        (InputActions.WorldNew, () => _flow.NewRandomWorld()),
        (InputActions.WorldQuickSave, () => _flow.QuickSave()),
        (InputActions.WorldQuickLoad, () => _flow.QuickLoad()),
        (InputActions.SimTogglePause, () => SetStatus(SessionCommands.TogglePause(_session))),
        (InputActions.SimSpeedDown, () => SetStatus(SessionCommands.ChangeSpeed(_session, -1))),
        (InputActions.SimSpeedUp, () => SetStatus(SessionCommands.ChangeSpeed(_session, 1))),
        .. CreateMiracleHotkeys(),
        (InputActions.MiracleCancel, () => _divine.Select(null)),
        (InputActions.CreatureFocus, FocusCreature),
        (InputActions.ToggleDiplomacy, () => _diplomacy.PanelVisible = !_diplomacy.PanelVisible),
        (InputActions.CreatureReward, () => _divine.JudgeCreature(CreatureCommandKind.Reward)),
        (InputActions.CreaturePunish, () => _divine.JudgeCreature(CreatureCommandKind.Punish)),
        (InputActions.CreatureSend, () =>
        {
            // V sends the Creature to the cursor; Shift+V sets it on the rival nearest the cursor (ADR-062).
            if (Input.IsKeyPressed(Key.Shift))
            {
                _divine.FightAt(_camera.CursorGroundPoint);
            }
            else
            {
                _divine.SendCreature(_camera.CursorGroundPoint);
            }
        }),
        (InputActions.CreatureWork, () => _divine.WorkAt(_camera.CursorGroundPoint)),
        (InputActions.HandGrab, () => _divine.UseHand(_camera.CursorGroundPoint, Input.IsKeyPressed(Key.Shift), Input.IsKeyPressed(Key.Ctrl))),
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

    /// <summary>Loads the save given by <c>--load</c>, or generates a world from <c>--seed</c> (or the default seed), its rivals written by the scribes (ADR-065).</summary>
    private GameSession CreateSession(GameContent content, Pantheon.Game.Minds.MindContent? minds, LaunchOptions options)
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
        world = minds is null ? world : world with { Born = Pantheon.Game.Minds.PantheonBirth.Write(content, minds, seed, world) };
        var session = GameSession.CreateNew(content, seed, world);
        SetStatus(loadFailure ?? SessionCommands.Describe(content, seed, world));
        return session;
    }

    /// <summary>Rebuilds every visual from the current state and points the camera at the settlement.</summary>
    private void ShowWorld() => Rebuild(newWorld: true);

    /// <summary>Rebuilds every visual from the current state: for a new world the camera goes to its settlement; for reshaped land (ADR-045) it stays.</summary>
    private void Rebuild(bool newWorld)
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
        _creatures.Prepare(_session.World);
        _weather.Rebuild(_session.Terrain);
        _disasters.Rebuild(_session.Terrain, _session.World.Regions.RegionSize);
        _migrants.Rebuild(_session.Terrain);
        _camera.SetTerrain(_session.Terrain);
        if (newWorld)
        {
            _divine?.Reset();
            _focusIndex = -1;
            FocusSettlement(distance: null, snap: true);
        }
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
        if (options.Fight)
        {
            _divine.FightNearest();
        }

        if (options.Work is { } work && settlements.Count > 0)
        {
            _divine.WorkFor(settlements[Math.Max(0, _focusIndex)], forest: work == "forest");
        }
    }

    /// <summary>Shows the diplomacy panel once it is ready, when the launch options ask for it.</summary>
    /// <param name="visible">Whether to show it.</param>
    private void ShowDiplomacyAtStart(bool visible) => _diplomacy.PanelVisible = visible;

    /// <summary>Glides to the player's Creature and follows it until the player pans away (spec §56: focus Creature; ADR-058).</summary>
    private void FocusCreature()
    {
        if (_session.LocalCreature is { } creature)
        {
            _camera.Model.FaceFront(creature.Heading);
            _camera.FocusOn(creature.Position, CreatureFocusDistance, snap: false);
            FollowCreature(creature);
            SetStatus($"Following {creature.Name}: move the view to let go");
        }
    }

    /// <summary>Keeps the camera on a Creature as it is drawn, aiming at its body.</summary>
    private void FollowCreature(CreatureState creature)
    {
        var id = creature.Id;
        var height = _creatures.Find(id)?.HeadHeight ?? 12f;
        _camera.Follow(() => _creatures.Find(id)?.Position, height * CreatureAimShare);
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
        _camera.StopFollowing();
        _camera.FocusOn(settlements[_focusIndex].Position, FocusDistance, snap: false);
        SetStatus($"Looking at {settlements[_focusIndex].Name}");
    }

    /// <summary>Region containing a world point, if any.</summary>
    private int? RegionAt(NVector3? point) =>
        point is { } p && _session.World.Regions.TryGetRegionIdAt(p.X, p.Z, out var regionId) ? regionId : null;

    /// <summary>One-line world summary printed at the end of unattended runs.</summary>
    private string DescribeWorld() =>
        $"{DebugReadout.Summarize(_session.World, _session.Content.SettlementRules, _presenter.CrowdStatuses)}; terrain {_presenter.TerrainBackend}"
        + string.Concat(_network.Lines(_session).Select(line => "\n" + line)) + "\n" + _debug.Report();

    /// <summary>Remembers a status line for the overlay and echoes it to the log.</summary>
    private void SetStatus(string message)
    {
        _status = message;
        _hud?.ShowStatus(message);
        GD.Print(message);
    }

}
