using System.Collections.Generic;

namespace Pantheon.Game.Hud;

/// <summary>Everything the player HUD shows for one frame (spec §57), built by <see cref="HudReadout"/>.</summary>
public sealed record HudModel
{
    /// <summary>Whether the local player has a god; without one the HUD shows only <see cref="GodLine"/>.</summary>
    public required bool HasGod { get; init; }

    /// <summary>God name and rank.</summary>
    public required string GodLine { get; init; }

    /// <summary>Divine Power, rounded.</summary>
    public required string PowerText { get; init; }

    /// <summary>Divine Power raised per simulated second.</summary>
    public required string IncomeText { get; init; }

    /// <summary>Followers and the next rank.</summary>
    public required string FollowersText { get; init; }

    /// <summary>The miracle bar.</summary>
    public required IReadOnlyList<MiracleSlot> Miracles { get; init; }

    /// <summary>What casting the selected miracle at the cursor would do, or <c>null</c> when none is selected.</summary>
    public string? TargetHint { get; init; }

    /// <summary>Whether the selected miracle can be cast at the cursor.</summary>
    public bool TargetValid { get; init; }

    /// <summary>Radius in metres of the selected miracle, for the targeting ring; 0 when none is selected.</summary>
    public float TargetRadius { get; init; }
}
