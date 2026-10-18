using System;
using System.Collections.Generic;

namespace Pantheon.Game.Presentation;

/// <summary>How houses, the building site and fields are laid out around a settlement's shrine (the <c>Layout</c> part of <c>settlement_view.json</c>).</summary>
public sealed record SettlementLayoutSettings
{
    /// <summary>Radius kept free around the shrine, in metres.</summary>
    public required float ShrineClearance { get; init; }

    /// <summary>Spacing factor of the house spiral, in metres.</summary>
    public required float HouseSpacing { get; init; }

    /// <summary>Houses and fields are not placed on ground lower than this height (metres), which keeps them out of the surf.</summary>
    public required float MinimumGroundHeight { get; init; }

    /// <summary>Distance from a house's centre to the spot in front of its door, in metres.</summary>
    public required float DoorDistance { get; init; }

    /// <summary>The village works one field for every this many houses.</summary>
    public required int HousesPerField { get; init; }

    /// <summary>Most fields shown.</summary>
    public required int MaxFields { get; init; }

    /// <summary>Side of a square field in metres.</summary>
    public required float FieldSize { get; init; }

    /// <summary>Spacing factor of the field spiral, in metres.</summary>
    public required float FieldSpacing { get; init; }

    /// <summary>Gap between the centres of the outermost houses and the edge of the fields, in metres.</summary>
    public required float FieldClearance { get; init; }

    /// <summary>Steepest ground (rise over run) a field is laid on.</summary>
    public required float FieldMaxSlope { get; init; }

    /// <summary>Crop colours; each field shows one of them.</summary>
    public required IReadOnlyList<RgbColor> CropColors { get; init; }

    /// <summary>Throws when a value is out of range, listing every problem.</summary>
    /// <exception cref="ArgumentException">At least one value is invalid.</exception>
    public void Validate()
    {
        var errors = new List<string>();
        Require(errors, ShrineClearance >= 0f, "ShrineClearance must not be negative.");
        Require(errors, HouseSpacing > 0f, "HouseSpacing must be positive.");
        Require(errors, float.IsFinite(MinimumGroundHeight), "MinimumGroundHeight must be a finite number.");
        Require(errors, DoorDistance > 0f, "DoorDistance must be positive.");
        Require(errors, HousesPerField >= 1, "HousesPerField must be at least 1.");
        Require(errors, MaxFields >= 0, "MaxFields must not be negative.");
        Require(errors, FieldSize > 0f, "FieldSize must be positive.");
        Require(errors, FieldSpacing > 0f, "FieldSpacing must be positive.");
        Require(errors, FieldClearance >= 0f, "FieldClearance must not be negative.");
        Require(errors, FieldMaxSlope > 0f, "FieldMaxSlope must be positive.");
        Require(errors, CropColors.Count > 0, "CropColors must list at least one colour.");
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
