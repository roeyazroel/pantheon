using System;
using System.Linq;
using Pantheon.Client.Camera;
using Pantheon.Client.Network;
using Pantheon.Client.Presentation;
using Pantheon.Client.UI;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Presentation;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// The F1 debug panel and the F3 region debug map in the client: it gathers each frame's
/// <see cref="DebugSnapshot"/>, with the client's performance measurements (ADR-044), and redraws the
/// region map when its layer, the cursor's region or the tick changes.
/// </summary>
internal sealed class DebugView
{
    // The debug panel details the settlement nearest the cursor within this distance.
    private const float SelectDistance = 250f;

    // The panel's lines are rebuilt this often, in real seconds: building them weighs every AI god's options.
    private const double RefreshSeconds = 0.25;

    private readonly GameSession _session;
    private readonly WorldPresenter _presenter;
    private readonly GodCameraController _camera;
    private readonly DebugOverlay _overlay;
    private readonly NetworkDriver _network;
    private readonly BiomePalette _palette;
    private RegionDebugLayer? _regionMapLayer;
    private (RegionDebugLayer Layer, int? CursorRegion, long Tick, WorldState World)? _shownRegionMap;
    private double _age = RefreshSeconds;

    /// <summary>Creates the view over the client's parts.</summary>
    /// <param name="session">The session.</param>
    /// <param name="presenter">The world's visuals, for the terrain, labels and crowds.</param>
    /// <param name="camera">The camera, for its band and distance.</param>
    /// <param name="overlay">The debug panel.</param>
    /// <param name="network">The network, for its lines.</param>
    /// <param name="palette">Biome colours, for the region map.</param>
    /// <param name="fires">Fires drawn and burning, for the streaming line.</param>
    /// <param name="streaming">Streaming settings, for the streaming line.</param>
    public DebugView(GameSession session, WorldPresenter presenter, GodCameraController camera, DebugOverlay overlay, NetworkDriver network, BiomePalette palette, Func<(int Shown, int Burning)> fires, Pantheon.Game.Presentation.StreamingSettings streaming)
    {
        (_session, _presenter, _camera, _overlay, _network, _palette) = (session, presenter, camera, overlay, network, palette);
        (_fires, _streaming) = (fires, streaming);
    }

    private readonly Func<(int Shown, int Burning)> _fires;

    /// <summary>What streams near the camera now.</summary>
    private StreamingStatus Streaming() =>
        new(_presenter.Streaming.Forests.Loaded, _presenter.Streaming.Forests.Chunks, _presenter.Streaming.Terrain, _fires().Shown, _fires().Burning, _streaming);
    private readonly Pantheon.Game.Presentation.StreamingSettings _streaming;

    /// <summary>The client's performance measurements.</summary>
    public PerformanceMonitor Performance { get; } = new();

    /// <summary>Shows the region debug map's next layer, or hides the map after the last one.</summary>
    /// <returns>Status line.</returns>
    public string CycleRegionMap()
    {
        _regionMapLayer = RegionDebugMap.Next(_regionMapLayer);
        return _regionMapLayer is { } layer ? $"Debug map: {layer}" : "Debug map off";
    }

    /// <summary>Redraws the region map if needed and, while the panel is open, its lines a few times a second.</summary>
    /// <param name="cursor">The ground under the cursor, if any.</param>
    /// <param name="cursorRegion">The region under the cursor, if any.</param>
    /// <param name="status">The latest status line.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Update(NVector3? cursor, int? cursorRegion, string? status, double delta)
    {
        UpdateRegionMap(cursorRegion);
        _age += delta;
        if (_overlay.PanelVisible && _age >= RefreshSeconds)
        {
            _age = 0.0;
            _overlay.ShowModel(DebugReadout.Build(CreateSnapshot(cursor, status)));
        }
    }

    /// <summary>The end-of-run lines of an unattended run: the measurements, with a save timed.</summary>
    /// <returns>The lines.</returns>
    public string Report()
    {
        Performance.MeasureSave(_session);
        return string.Join("\n", PerformanceReadout.Describe(Performance.Sample(_presenter.CrowdStatuses, _presenter.Debris)).Append(PerformanceReadout.Census(_session.World)).Append(StreamingReadout.Describe(Streaming())));
    }

    /// <summary>Redraws the region debug map when its layer, the cursor region, the tick or the world changed.</summary>
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

    /// <summary>Collects this frame's data for the debug panel.</summary>
    private DebugSnapshot CreateSnapshot(NVector3? cursor, string? status) => new()
    {
        FramesPerSecond = Godot.Engine.GetFramesPerSecond(),
        World = _session.World,
        SettlementRules = _session.Content.SettlementRules,
        Style = _presenter.Style,
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
        Performance = Performance.Sample(_presenter.CrowdStatuses, _presenter.Debris),
        GodDiplomacyCandidates = [.. _session.World.Gods.Where(god => god.Controller == GodController.Ai).Select(god => (god.Id, _session.PreviewGodDiplomacy(god)))],
        GodAiCandidates = [.. _session.World.Gods.Where(god => god.Controller == GodController.Ai).Select(god => (god.Id, _session.PreviewGodAi(god)))],
        GodGoalOptions = [.. _session.World.Gods.Where(god => god.Controller == GodController.Ai).Select(god => (god.Id, _session.PreviewGodGoals(god)))],
        SelectedSettlementId = cursor is { } point ? SettlementPicker.Nearest(_session.World, new GroundPosition(point.X, point.Z), SelectDistance)?.Id : null,
        IsPaused = _session.Scheduler.IsPaused,
        TimeScale = _session.Scheduler.TimeScale,
        TicksPerSecond = _session.Scheduler.TicksPerSecond,
        TerrainBackend = _presenter.TerrainBackend,
        TerrainIsFallback = _presenter.TerrainIsFallback,
        ZoomBand = _camera.Model.Band,
        CameraDistance = _camera.Model.Distance,
        CursorWorldPosition = cursor,
        RegionBoundariesVisible = _presenter.RegionBoundariesVisible,
        StatusMessage = status,
        Crowds = _presenter.CrowdStatuses,
        CrowdClock = (_session.Crowds.Seconds, _presenter.CrowdPace),
        Streaming = Streaming(),
        HolySiteRules = _session.Content.HolySites,
        WaterRules = _session.Content.Water,
    };

    /// <summary>The pressure to leave on the settlement near the cursor, for the debug panel.</summary>
    private Pantheon.Simulation.Migration.MigrationPressure? PressureUnder(NVector3? cursor) =>
        cursor is { } point && SettlementPicker.Nearest(_session.World, new GroundPosition(point.X, point.Z), SelectDistance) is { } settlement
            ? _session.MigrationPressureOn(settlement)
            : null;
}
