using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Settlements;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Population;

/// <summary>
/// The representative citizens of one settlement as one client shows them: its tier, the roster
/// derived from the aggregates, and where each figure is drawn. It only reads the world.
/// </summary>
/// <remarks>
/// Each frame the client calls <see cref="SelectTier"/>, <see cref="Refresh"/> and
/// <see cref="Advance"/>. Figures follow <see cref="CitizenRoutine"/>; when the roster changes
/// under them (a new home, a new role), they walk to their new route at
/// <see cref="CitizenSettings.CatchUpSpeed"/> instead of jumping. Representatives added while the
/// settlement is in Tier 2 step out of their front door; on promotion everyone appears in place.
/// </remarks>
public sealed class SettlementCrowd : ICitizenWays
{
    private readonly WorldSeed _seed;
    private readonly int _settlementId;
    private readonly SettlementViewSettings _settings;
    private readonly DayNightSettings? _day;
    private IReadOnlyList<RepresentativeCitizen> _roster = [];
    private Vector2[] _shown = [];
    private bool[] _placed = [];
    private bool[] _inside = [];
    private Vector2[]?[] _homeways = [];
    private int[] _waypoint = [];
    private float? _lastHour;
    private CitizenPose[] _poses = [];
    private RosterInputs? _rosterInputs;
    private double? _lastSeconds;
    private double? _rebuiltAt;
    private double? _bakedAt;
    private IReadOnlyList<BridgeSpan> _bridges = [];
    private readonly LocalNavLand _land = new();

    // Which of a village's people go in first.
    private const ulong EveningSalt = 80;

    private SettlementState? _settlement;
    private SettlementSite? _site;
    private (int Houses, HutPlacement? Construction, int Ruins, bool Temple, int Roads)? _gridFor;

    /// <summary>Creates the crowd of a settlement, in Tier 1 with nobody shown.</summary>
    /// <param name="seed">World seed, for the roster hashes.</param>
    /// <param name="settlementId">Settlement shown.</param>
    /// <param name="settings">Settlement presentation settings.</param>
    /// <param name="day">How the day passes, for who is abroad (ADR-080); nobody ever goes in without it.</param>
    public SettlementCrowd(WorldSeed seed, int settlementId, SettlementViewSettings settings, DayNightSettings? day = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _seed = seed;
        _settlementId = settlementId;
        _settings = settings;
        _day = day;
    }

    /// <summary>Current tier.</summary>
    public SimulationTier Tier { get; private set; } = SimulationTier.Statistical;

    /// <summary>Current representatives, by index; empty in Tier 1.</summary>
    public IReadOnlyList<RepresentativeCitizen> Roster => _roster;

    /// <summary>Poses from the latest <see cref="Advance"/>, by representative index.</summary>
    public IReadOnlyList<CitizenPose> Poses => _poses;

    /// <summary>Changes whenever the roster is rebuilt or cleared, so views know to refresh roles and counts.</summary>
    public int RosterVersion { get; private set; }

    /// <summary>The village's local navigation in Tier 2 (ADR-046), or <c>null</c> before the figures first walk.</summary>
    public LocalNavGrid? Navigation { get; private set; }

    /// <summary>Moves the settlement between tiers for the camera distance; leaving Tier 2 removes every figure.</summary>
    /// <param name="cameraDistance">Distance from the camera to the settlement centre, in metres.</param>
    public void SelectTier(float cameraDistance)
    {
        Tier = TierSelector.Select(Tier, cameraDistance, _settings.Citizens);
        if (Tier == SimulationTier.Statistical && _rosterInputs is not null)
        {
            Clear();
        }
    }

    /// <summary>
    /// In Tier 2, rebuilds the roster when the representative count, the role split or the layout
    /// changed, at most once every <see cref="CitizenSettings.RosterRefreshSeconds"/> of the crowds'
    /// time (ADR-056): at ×16 a big village's roles shift every few frames.
    /// </summary>
    /// <param name="settlement">Current settlement state.</param>
    /// <param name="labour">Its labour split.</param>
    /// <param name="site">Its current layout.</param>
    public void Refresh(SettlementState settlement, LabourAllocation labour, SettlementSite site)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(site);
        if (Tier == SimulationTier.Statistical)
        {
            return;
        }

        (_settlement, _site) = (settlement, site);
        var count = RepresentativeRoster.ComputeCount(settlement.Population, _settings.Citizens.MaxRepresentatives);
        var inputs = RosterInputs.Of(RepresentativeRoster.SplitRoles(count, settlement.Population, labour), site);
        if (inputs == _rosterInputs || (_rosterInputs is not null && Waiting(_rebuiltAt)))
        {
            return;
        }

        Rebuild(RepresentativeRoster.Build(_seed, settlement, labour, site, _settings), promoted: _rosterInputs is null);
        (_rosterInputs, _rebuiltAt) = (inputs, _lastSeconds);
    }

    /// <summary>Poses every representative for the crowds' time (<see cref="CrowdClock"/>), standing on the terrain.</summary>
    /// <param name="simulationSeconds">The crowds' time in seconds.</param>
    /// <param name="terrain">Terrain, for the figures' heights.</param>
    /// <param name="roads">The world's roads, whose bridges the village's walkers cross (ADR-057).</param>
    /// <param name="hour">The hour of the day (ADR-080); when <c>null</c> people keep to their routine.</param>
    public void Advance(double simulationSeconds, TerrainField terrain, IReadOnlyList<Domain.Roads.RoadState>? roads = null, float? hour = null)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        Bake(terrain, roads);
        var elapsed = _lastSeconds is { } last ? Math.Max(0.0, simulationSeconds - last) : 0.0;
        _lastSeconds = simulationSeconds;
        var maxStep = (float)(_settings.Citizens.CatchUpSpeed * elapsed);
        var homeStep = HomeStep(hour, (float)elapsed);
        for (var i = 0; i < _roster.Count; i++)
        {
            var phase = hour is { } now && _day is { } day ? CitizenEvening.Of(day, now, ShareOf(i)) : EveningPhase.Abroad;
            var pose = phase == EveningPhase.Abroad
                ? Rising(i, simulationSeconds, maxStep)
                : GoingIn(i, phase, homeStep, simulationSeconds);
            var height = FootingAt(terrain, pose.Ground) + pose.Bounce;
            _poses[i] = new CitizenPose(new Vector3(pose.Ground.X, height, pose.Ground.Y), pose.Yaw, pose.Lean, pose.Activity, pose.Cycle, _inside[i]);
        }

        _lastHour = hour;
    }

    /// <summary>A figure's own place among its neighbours, for when it goes in and comes out.</summary>
    private float ShareOf(int index) => PresentationHash.Unit(EveningSalt, (ulong)(uint)_settlementId, (ulong)(uint)index);

    /// <summary>How far people walk home this frame, hurrying if the evening is passing fast.</summary>
    private float HomeStep(float? hour, float elapsed)
    {
        if (_day is not { } day || hour is not { } now)
        {
            return 0f;
        }

        var passed = _lastHour is { } last ? ((now - last + DayNight.Hours) % DayNight.Hours) : 0f;
        return day.HomeSpeed * elapsed * CitizenEvening.Hurry(day, passed, elapsed);
    }

    /// <summary>A figure on its routine; one who was indoors steps out of its door, and its routine leads it from there.</summary>
    private RoutinePose Rising(int index, double seconds, float maxStep)
    {
        if (_inside[index])
        {
            _inside[index] = false;
            _shown[index] = DoorOf(index);
        }

        _homeways[index] = null;
        return Follow(index, CitizenRoutine.Evaluate(_roster[index], seconds, _settings.Citizens, this), maxStep, seconds);
    }

    /// <summary>A figure whose day is done: it walks the village's ways to its door and goes in. Once night has come it is in, wherever it had got to.</summary>
    private RoutinePose GoingIn(int index, EveningPhase phase, float step, double seconds)
    {
        var door = DoorOf(index);
        if (phase == EveningPhase.Indoors || _inside[index] || !_placed[index])
        {
            (_inside[index], _placed[index], _shown[index]) = (true, true, door);
            return new RoutinePose(door, 0f, 0f, 0f, CitizenActivity.AtHome, 0f);
        }

        if (_homeways[index] is null)
        {
            _homeways[index] = HomewayOf(index, door);
            _waypoint[index] = 0;
        }

        var way = _homeways[index]!;
        var (at, left) = (_shown[index], step);
        while (_waypoint[index] < way.Length)
        {
            var next = way[_waypoint[index]];
            var gap = Vector2.Distance(at, next);
            if (gap > left)
            {
                at += (next - at) * (left / gap);
                _shown[index] = at;
                var walked = (float)(seconds * _settings.Citizens.CatchUpSpeed);
                return new RoutinePose(at, GroundMath.YawAlong(next - at), 0f, CitizenRoutine.Bounce(walked, _settings.Citizens), CitizenActivity.Walking, CitizenRoutine.Strides(walked, _settings.Citizens));
            }

            (at, left) = (next, left - gap);
            _waypoint[index]++;
        }

        (_inside[index], _shown[index]) = (true, door);
        return new RoutinePose(door, 0f, 0f, 0f, CitizenActivity.AtHome, 0f);
    }

    /// <summary>The way from where a figure stands to its door: round what stands between, to the spot before the door, and in.</summary>
    private Vector2[] HomewayOf(int index, Vector2 door)
    {
        var home = _roster[index].Home;
        var round = Navigation?.Route(_shown[index], home);
        var way = new Vector2[(round?.Length ?? 1) + 1];
        if (round is null)
        {
            way[0] = home;
        }
        else
        {
            Array.Copy(round, way, round.Length);
        }

        way[^1] = door;
        return way;
    }

    /// <summary>Where a figure goes in: its house's door, or for those with no house the spot where it rests.</summary>
    private Vector2 DoorOf(int index)
    {
        var (citizen, reach) = (_roster[index], _day?.DoorReach ?? 2f);
        return _site is { } site && citizen.House >= 0 && citizen.House < site.Houses.Count ? site.DoorOf(citizen.House, reach) : citizen.Home;
    }

    /// <summary>The height a walker stands at: on a bridge's deck over the water, or on the ground.</summary>
    private float FootingAt(TerrainField terrain, Vector2 ground)
    {
        foreach (var bridge in _bridges)
        {
            if (bridge.DeckOver(ground) is { } deck)
            {
                return deck;
            }
        }

        return terrain.SampleHeight(ground.X, ground.Y);
    }

    /// <summary>The settlement's tier and representatives, for the debug overlay.</summary>
    /// <param name="population">Current inhabitants.</param>
    /// <param name="cameraDistance">Distance from the camera to the settlement centre, in metres.</param>
    /// <returns>The status.</returns>
    public CrowdStatus GetStatus(int population, float cameraDistance) => new(
        _settlementId,
        Tier,
        _roster.Count,
        _roster.Count > 0 ? (float)population / _roster.Count : 0f,
        cameraDistance,
        _settings.Citizens.RegionalTierEnterDistance,
        _settings.Citizens.RegionalTierExitDistance,
        Navigation is { } grid ? (grid.Cells, grid.Blocked, grid.Searches, grid.Detours) : null,
        Array.FindAll(_inside, inside => inside).Length);

    /// <inheritdoc />
    public IReadOnlyList<Vector2>? Way(int index, CitizenLeg leg, Vector2 from, Vector2 to) => Navigation?.Way(from, to);

    /// <summary>
    /// Bakes the village's local navigation again when what stands in the way changed (its huts, the
    /// building site's place, its ruins or its temple) or its roads were built further over water,
    /// while figures are shown. The layout itself is laid out again as building goes on, which moves
    /// nothing.
    /// </summary>
    private void Bake(TerrainField terrain, IReadOnlyList<Domain.Roads.RoadState>? roads)
    {
        if (_roster.Count == 0 || _site is not { } site || _settlement is not { } settlement)
        {
            return;
        }

        var key = (site.Houses.Count, site.Construction?.House, site.Ruins.Count, Simulation.Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Temple), RoadsBuilt(roads, settlement.Id));
        if (_gridFor == key || (_gridFor is not null && Waiting(_bakedAt)))
        {
            return;
        }

        var bridges = new List<BridgeSpan>();
        foreach (var road in roads ?? [])
        {
            if ((road.FromSettlementId == settlement.Id || road.ToSettlementId == settlement.Id) && road.Built > 0f)
            {
                bridges.AddRange(BridgeLayout.Spans(road, terrain));
            }
        }

        _bridges = bridges;
        Navigation = LocalNavGrid.Build(site, settlement, terrain, _settings.Citizens, _land, bridges);
        (_gridFor, _bakedAt) = (key, _lastSeconds);
    }

    /// <summary>How far the village's roads are built, as one number that changes when any of them grows.</summary>
    private static int RoadsBuilt(IReadOnlyList<Domain.Roads.RoadState>? roads, int settlementId)
    {
        var built = new HashCode();
        foreach (var road in roads ?? [])
        {
            if (road.FromSettlementId == settlementId || road.ToSettlementId == settlementId)
            {
                built.Add(road.Id);
                built.Add((int)MathF.Round(road.Built * 100f));
            }
        }

        return built.ToHashCode();
    }

    /// <summary>Whether too little of the crowds' time has passed since a rebuild for another.</summary>
    private bool Waiting(double? since) =>
        since is { } at && _lastSeconds is { } now && now - at < _settings.Citizens.RosterRefreshSeconds;

    /// <summary>
    /// Moves a figure along its routine, or towards it at the catch-up speed when the routine is
    /// out of reach this frame. Figures not placed yet appear on their routine.
    /// </summary>
    private RoutinePose Follow(int index, RoutinePose target, float maxStep, double seconds)
    {
        var shown = _placed[index] ? _shown[index] : target.Ground;
        _placed[index] = true;
        var gap = target.Ground - shown;
        var distance = gap.Length();
        var pose = distance <= maxStep
            ? target
            : new RoutinePose(
                shown + (gap * (maxStep / distance)),
                GroundMath.YawAlong(gap),
                0f,
                CitizenRoutine.Bounce((float)(seconds * _settings.Citizens.CatchUpSpeed), _settings.Citizens),
                CitizenActivity.Walking,
                CitizenRoutine.Strides((float)(seconds * _settings.Citizens.CatchUpSpeed), _settings.Citizens));
        _shown[index] = pose.Ground;
        return pose;
    }

    /// <summary>
    /// Replaces the roster. Representatives that stay keep their drawn position; new ones step out
    /// of their door, or after a promotion appear on their routine.
    /// </summary>
    private void Rebuild(IReadOnlyList<RepresentativeCitizen> roster, bool promoted)
    {
        var shown = new Vector2[roster.Count];
        var placed = new bool[roster.Count];
        var inside = new bool[roster.Count];
        var poses = new CitizenPose[roster.Count];
        for (var i = 0; i < roster.Count; i++)
        {
            if (i < _roster.Count)
            {
                shown[i] = _shown[i];
                placed[i] = _placed[i];
                inside[i] = _inside[i];
                poses[i] = _poses[i];
            }
            else if (!promoted)
            {
                shown[i] = roster[i].Home;
                placed[i] = true;
            }
        }

        _roster = roster;
        _shown = shown;
        _placed = placed;
        _inside = inside;
        _homeways = new Vector2[]?[roster.Count];
        _waypoint = new int[roster.Count];
        _poses = poses;
        RosterVersion++;
    }

    /// <summary>Removes every figure; the next promotion places everyone afresh.</summary>
    private void Clear()
    {
        (Navigation, _gridFor, _site, _settlement) = (null, null, null, null);
        _roster = [];
        _shown = [];
        _placed = [];
        _inside = [];
        _homeways = [];
        _waypoint = [];
        _poses = [];
        _rosterInputs = null;
        RosterVersion++;
    }

    /// <summary>
    /// What the roster depends on besides the seed, the settlement and the settings: the role
    /// counts (their total is the representative count) and the layout. Houses are a stable prefix
    /// of the house spiral and fields a run of the field spiral, so their counts, the first field
    /// slot and the building site identify the layout without comparing every placement.
    /// </summary>
    private readonly record struct RosterInputs(RoleCounts Roles, int Houses, HutPlacement? Construction, int Fields, int FirstFieldSlot)
    {
        /// <summary>The inputs of a roster.</summary>
        public static RosterInputs Of(RoleCounts roles, SettlementSite site) => new(
            roles,
            site.Houses.Count,
            site.Construction?.House,
            site.Fields.Count,
            site.Fields.Count > 0 ? site.Fields[0].Slot : -1);
    }
}
