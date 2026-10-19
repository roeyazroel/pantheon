using System;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Migration;

namespace Pantheon.Game.Presentation;

/// <summary>Who decides what a god does: the local player, another player, or the AI.</summary>
public enum Authority
{
    /// <summary>The local player.</summary>
    You,

    /// <summary>Another player, at the host or joined to it.</summary>
    Player,

    /// <summary>The AI.</summary>
    Ai,
}

/// <summary>
/// The Authority overlay (spec §106's network authority, ADR-044): the host decides the whole world,
/// and each god's acts come from a player's intents or the AI. Each village's lands show who
/// commands the god most of its people follow. Pure.
/// </summary>
public static class AuthorityOverlay
{
    /// <summary>The overlay's legend.</summary>
    public const string Legend = "Authority: the host decides the world; the lands of the gods you command in gold, of gods other players command in blue, of the AI's gods in grey";

    private static readonly RgbColor Yours = RgbColor.ParseHex("#f2c94c");
    private static readonly RgbColor Players = RgbColor.ParseHex("#3d8fe0");
    private static readonly RgbColor Machine = RgbColor.ParseHex("#8c8c8c");

    /// <summary>Who commands a god.</summary>
    /// <param name="god">The god.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <returns>The authority.</returns>
    public static Authority Of(GodState god, int? localGodId)
    {
        ArgumentNullException.ThrowIfNull(god);
        return god.Id == localGodId ? Authority.You : god.Controller == GodController.Ai ? Authority.Ai : Authority.Player;
    }

    /// <summary>Paints each village's lands by who commands the god most of it follows.</summary>
    /// <param name="world">World state.</param>
    /// <param name="localGodId">The local player's god.</param>
    /// <param name="opacity">Overlay opacity.</param>
    /// <param name="territory">Paints a village's lands.</param>
    public static void Paint(WorldState world, int? localGodId, float opacity, Action<int, RgbColor, float> territory)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(territory);
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population > 0 && MigrationModel.MainGod(settlement.Beliefs) is { } main && world.Gods.Find(g => g.Id == main) is { } god)
            {
                territory(settlement.RegionId, Of(god, localGodId) switch
                {
                    Authority.You => Yours,
                    Authority.Player => Players,
                    Authority.Ai => Machine,
                }, opacity);
            }
        }
    }
}
