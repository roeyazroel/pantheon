using System;
using System.Collections.Generic;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Population;

/// <summary>
/// Tier 2 tuning (the <c>Citizens</c> part of <c>settlement_view.json</c>): when representative
/// citizens appear, how many, where their routine takes them and how they are drawn. Distances
/// are in metres, durations in simulated seconds.
/// </summary>
public sealed record CitizenSettings
{
    /// <summary>Most representatives drawn per settlement.</summary>
    public required int MaxRepresentatives { get; init; }

    /// <summary>A settlement enters Tier 2 when the camera comes this close to its centre.</summary>
    public required float RegionalTierEnterDistance { get; init; }

    /// <summary>A settlement leaves Tier 2 when the camera moves farther than this; above the enter distance, so the tier does not flicker.</summary>
    public required float RegionalTierExitDistance { get; init; }

    /// <summary>Walking speed along the routine, in metres per second.</summary>
    public required float WalkSpeed { get; init; }

    /// <summary>Speed at which a rerouted citizen catches up with its new routine; at least the walking speed.</summary>
    public required float CatchUpSpeed { get; init; }

    /// <summary>
    /// The most seconds of the crowds' routines that pass in a second of real time (ADR-049), so
    /// people keep a human pace when the game runs faster.
    /// </summary>
    public float PaceCap { get; init; } = 1f;

    /// <summary>Time spent resting at home in each cycle.</summary>
    public required float RestSeconds { get; init; }

    /// <summary>Time spent working in each cycle.</summary>
    public required float WorkSeconds { get; init; }

    /// <summary>Time spent gathered at the shrine in each cycle.</summary>
    public required float GatherSeconds { get; init; }

    /// <summary>Residents rest up to this far out from and to either side of the spot in front of their door.</summary>
    public required float DoorSpread { get; init; }

    /// <summary>Villagers do their chores within this radius of their home spot.</summary>
    public required float WanderRadius { get; init; }

    /// <summary>Share of a field's side over which its farmers spread, 0..1.</summary>
    public required float WorkSpread { get; init; }

    /// <summary>Builders work on a ring of this radius around the house under construction.</summary>
    public required float BuilderRingRadius { get; init; }

    /// <summary>Inner radius of the gathering ring around the shrine.</summary>
    public required float GatherRadiusMin { get; init; }

    /// <summary>Outer radius of the gathering ring around the shrine.</summary>
    public required float GatherRadiusMax { get; init; }

    /// <summary>Distance covered by one step; the walking bob repeats once per step.</summary>
    public required float StrideLength { get; init; }

    /// <summary>Height of the walking bob.</summary>
    public required float StepBounce { get; init; }

    /// <summary>Deepest forward lean while working, in radians.</summary>
    public required float WorkLean { get; init; }

    /// <summary>Work strokes (lean and straighten) per second.</summary>
    public required float WorkStrokesPerSecond { get; init; }

    /// <summary>Size of the figures relative to life size; above 1 they read better from the god's-eye view.</summary>
    public required float FigureScale { get; init; }

    /// <summary>The side of a cell of a village's local navigation grid, in metres (spec §85, ADR-046).</summary>
    public required float NavCellSize { get; init; }

    /// <summary>The room figures keep from walls, the shrine and the temple, in metres.</summary>
    public required float NavClearance { get; init; }

    /// <summary>Clothing colour of farmers.</summary>
    public required RgbColor FarmerColor { get; init; }

    /// <summary>Clothing colour of builders.</summary>
    public required RgbColor BuilderColor { get; init; }

    /// <summary>Clothing colour of villagers.</summary>
    public required RgbColor VillagerColor { get; init; }

    /// <summary>The clothing colour of a role.</summary>
    /// <param name="role">Role.</param>
    /// <returns>Its colour.</returns>
    public RgbColor ColorOf(CitizenRole role) => role switch
    {
        CitizenRole.Builder => BuilderColor,
        CitizenRole.Farmer => FarmerColor,
        CitizenRole.Villager => VillagerColor,
    };

    /// <summary>Throws when a value is out of range, listing every problem.</summary>
    /// <exception cref="ArgumentException">At least one value is invalid.</exception>
    public void Validate()
    {
        var errors = new List<string>();
        Require(errors, MaxRepresentatives >= 0, "MaxRepresentatives must not be negative.");
        Require(errors, RegionalTierEnterDistance > 0f, "RegionalTierEnterDistance must be positive.");
        Require(errors, RegionalTierExitDistance >= RegionalTierEnterDistance, "RegionalTierExitDistance must be at least RegionalTierEnterDistance.");
        Require(errors, WalkSpeed > 0f, "WalkSpeed must be positive.");
        Require(errors, PaceCap > 0f, "PaceCap must be positive.");
        Require(errors, CatchUpSpeed >= WalkSpeed, "CatchUpSpeed must be at least WalkSpeed, or citizens could never catch up with their routine.");
        Require(errors, RestSeconds >= 0f && WorkSeconds >= 0f && GatherSeconds >= 0f, "RestSeconds, WorkSeconds and GatherSeconds must not be negative.");
        Require(errors, RestSeconds + WorkSeconds + GatherSeconds > 0f, "RestSeconds, WorkSeconds and GatherSeconds must not all be 0.");
        Require(errors, DoorSpread >= 0f, "DoorSpread must not be negative.");
        Require(errors, WanderRadius >= 0f, "WanderRadius must not be negative.");
        Require(errors, WorkSpread is >= 0f and <= 1f, "WorkSpread must lie between 0 and 1.");
        Require(errors, BuilderRingRadius >= 0f, "BuilderRingRadius must not be negative.");
        Require(errors, GatherRadiusMin >= 0f && GatherRadiusMax >= GatherRadiusMin, "The gathering ring needs 0 <= GatherRadiusMin <= GatherRadiusMax.");
        Require(errors, StrideLength > 0f, "StrideLength must be positive.");
        Require(errors, StepBounce >= 0f, "StepBounce must not be negative.");
        Require(errors, WorkLean is >= 0f and <= MathF.PI / 2f, "WorkLean must lie between 0 and pi/2 radians.");
        Require(errors, WorkStrokesPerSecond >= 0f, "WorkStrokesPerSecond must not be negative.");
        Require(errors, FigureScale > 0f, "FigureScale must be positive.");
        Require(errors, NavCellSize is >= 0.25f and <= 4f, "NavCellSize must lie between 0.25 and 4 metres.");
        Require(errors, NavClearance is >= 0f and <= 3f, "NavClearance must lie between 0 and 3 metres.");
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    /// <summary>Records <paramref name="error"/> unless <paramref name="valid"/> holds (NaN fails every comparison, so it is rejected too).</summary>
    private static void Require(List<string> errors, bool valid, string error)
    {
        if (!valid)
        {
            errors.Add(error);
        }
    }
}
