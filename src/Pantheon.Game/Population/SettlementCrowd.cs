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
    private IReadOnlyList<RepresentativeCitizen> _roster = [];
    private Vector2[] _shown = [];
    private bool[] _placed = [];
    private CitizenPose[] _poses = [];
    private RosterInputs? _rosterInputs;
    private double? _lastSeconds;
    private SettlementState? _settlement;
    private SettlementSite? _site;
    private (int Houses, HutPlacement? Construction, int Ruins, bool Temple)? _gridFor;

    /// <summary>Creates the crowd of a settlement, in Tier 1 with nobody shown.</summary>
    /// <param name="seed">World seed, for the roster hashes.</param>
    /// <param name="settlementId">Settlement shown.</param>
    /// <param name="settings">Settlement presentation settings.</param>
    public SettlementCrowd(WorldSeed seed, int settlementId, SettlementViewSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _seed = seed;
        _settlementId = settlementId;
        _settings = settings;
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

    /// <summary>In Tier 2, rebuilds the roster when the representative count, the role split or the layout changed.</summary>
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
        if (inputs == _rosterInputs)
        {
            return;
        }

        Rebuild(RepresentativeRoster.Build(_seed, settlement, labour, site, _settings), promoted: _rosterInputs is null);
        _rosterInputs = inputs;
    }

    /// <summary>Poses every representative for the crowds' time (<see cref="CrowdClock"/>), standing on the terrain.</summary>
    /// <param name="simulationSeconds">The crowds' time in seconds.</param>
    /// <param name="terrain">Terrain, for the figures' heights.</param>
    public void Advance(double simulationSeconds, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        Bake(terrain);
        var elapsed = _lastSeconds is { } last ? Math.Max(0.0, simulationSeconds - last) : 0.0;
        _lastSeconds = simulationSeconds;
        var maxStep = (float)(_settings.Citizens.CatchUpSpeed * elapsed);
        for (var i = 0; i < _roster.Count; i++)
        {
            var pose = Follow(i, CitizenRoutine.Evaluate(_roster[i], simulationSeconds, _settings.Citizens, this), maxStep, simulationSeconds);
            var height = terrain.SampleHeight(pose.Ground.X, pose.Ground.Y) + pose.Bounce;
            _poses[i] = new CitizenPose(new Vector3(pose.Ground.X, height, pose.Ground.Y), pose.Yaw, pose.Lean, pose.Activity, pose.Cycle);
        }
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
        Navigation is { } grid ? (grid.Cells, grid.Blocked, grid.Searches, grid.Detours) : null);

    /// <inheritdoc />
    public IReadOnlyList<Vector2>? Way(int index, CitizenLeg leg, Vector2 from, Vector2 to) => Navigation?.Way(from, to);

    /// <summary>
    /// Bakes the village's local navigation again when what stands in the way changed (its huts, the
    /// building site's place, its ruins or its temple), while figures are shown. The layout itself is
    /// laid out again as building goes on, which moves nothing.
    /// </summary>
    private void Bake(TerrainField terrain)
    {
        if (_roster.Count == 0 || _site is not { } site || _settlement is not { } settlement)
        {
            return;
        }

        var key = (site.Houses.Count, site.Construction?.House, site.Ruins.Count, Simulation.Settlements.CivicModel.Has(settlement, Domain.Society.BuildingKind.Temple));
        if (_gridFor == key)
        {
            return;
        }

        Navigation = LocalNavGrid.Build(site, settlement, terrain, _settings.Citizens);
        _gridFor = key;
    }

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
        var poses = new CitizenPose[roster.Count];
        for (var i = 0; i < roster.Count; i++)
        {
            if (i < _roster.Count)
            {
                shown[i] = _shown[i];
                placed[i] = _placed[i];
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
