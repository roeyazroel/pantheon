using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Camera;
using Pantheon.Game.Population;
using Pantheon.Simulation.Creatures;
using Pantheon.Simulation.Gods;

namespace Pantheon.Game.Diagnostics;

/// <summary>Everything the debug overlay shows for one frame, collected by the Godot shell.</summary>
public sealed record DebugSnapshot
{
    /// <summary>Rendered frames per second.</summary>
    public required double FramesPerSecond { get; init; }

    /// <summary>Current world.</summary>
    public required WorldState World { get; init; }

    /// <summary>Settlement rules, for the settlement readouts and the step cadence.</summary>
    public required SettlementRules SettlementRules { get; init; }

    /// <summary>Divine rules, for the god and belief readouts.</summary>
    public required DivineRules DivineRules { get; init; }

    /// <summary>The god the local player controls, if any.</summary>
    public int? LocalGodId { get; init; }

    /// <summary>Creature rules, for the levels in the Creature view; <c>null</c> hides the next level's experience.</summary>
    public CreatureRules? CreatureRules { get; init; }

    /// <summary>The local player's Creature, whose options are listed.</summary>
    public int? LocalCreatureId { get; init; }

    /// <summary>Each AI god's candidate actions now, best first, by god id.</summary>
    public IReadOnlyList<(int GodId, IReadOnlyList<AiCandidate> Candidates)> GodAiCandidates { get; init; } = [];

    /// <summary>The local Creature's options now, best first.</summary>
    public IReadOnlyList<CreatureOption> CreatureOptions { get; init; } = [];

    /// <summary>Settlement shown in detail; the others get one line each. <c>null</c> shows the first.</summary>
    public int? SelectedSettlementId { get; init; }

    /// <summary>Whether the simulation clock is paused.</summary>
    public required bool IsPaused { get; init; }

    /// <summary>Simulation time scale.</summary>
    public required double TimeScale { get; init; }

    /// <summary>Simulation ticks per second at time scale 1.</summary>
    public required double TicksPerSecond { get; init; }

    /// <summary>Name of the active terrain renderer.</summary>
    public required string TerrainBackend { get; init; }

    /// <summary>Whether the active terrain renderer is the fallback mesh.</summary>
    public required bool TerrainIsFallback { get; init; }

    /// <summary>Current camera zoom band.</summary>
    public required CameraZoomBand ZoomBand { get; init; }

    /// <summary>Current camera distance in metres.</summary>
    public required float CameraDistance { get; init; }

    /// <summary>Ground point under the mouse cursor, if the cursor is over the terrain.</summary>
    public Vector3? CursorWorldPosition { get; init; }

    /// <summary>Whether region boundaries are drawn.</summary>
    public required bool RegionBoundariesVisible { get; init; }

    /// <summary>Latest one-line status, for example the result of a quicksave.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Simulation tier and representative citizens of each settlement; settlements without a status read as Tier 1.</summary>
    public IReadOnlyList<CrowdStatus> Crowds { get; init; } = [];
}
