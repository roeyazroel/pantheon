using System;

namespace Pantheon.Client.Presentation;

/// <summary>
/// The player's graphics scaling that presenters read as they build (spec §105, ADR-056): the share
/// of particles drawn, and the terrain's detail. The settings set it; nothing in the simulation
/// reads it.
/// </summary>
internal static class GraphicsScale
{
    /// <summary>Share of the particles drawn (0.25..1).</summary>
    public static float Particles { get; set; } = 1f;

    /// <summary>The terrain's detail (0.5..1).</summary>
    public static float Terrain { get; set; } = 1f;

    /// <summary>A number of particles scaled by <see cref="Particles"/>, at least one.</summary>
    /// <param name="amount">The number at full particles.</param>
    /// <returns>The number to draw.</returns>
    public static int Of(int amount) => Math.Max(1, (int)MathF.Round(amount * Particles));
}
