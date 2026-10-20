using System;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Settings;

/// <summary>
/// The presentation content as the player's graphics settings scale it (spec §105, ADR-056): the
/// citizens shown, how far the ground cover reaches and the forests are planted, and how much of the
/// land the fallback renderer draws in full detail. Pure.
/// </summary>
public static class GraphicsScaling
{
    /// <summary>Terrain3D's finest mesh at full terrain quality, in vertices a side.</summary>
    public const int FullTerrainMesh = 48;

    /// <summary>Terrain3D's finest mesh at the lowest terrain quality.</summary>
    public const int LeastTerrainMesh = 24;

    /// <summary>The content scaled by the settings.</summary>
    /// <param name="content">The content as shipped.</param>
    /// <param name="settings">The player's settings.</param>
    /// <returns>The content to show the world with.</returns>
    public static PresentationContent Apply(PresentationContent content, GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(settings);
        var view = content.SettlementView;
        var citizens = view.Citizens with { MaxRepresentatives = (int)MathF.Round(view.Citizens.MaxRepresentatives * settings.Citizens) };

        // Forests thin out from the far edge, never closer than half their reach: from on high the
        // land should still read as wooded.
        var forests = 0.5f + (0.5f * settings.Vegetation);
        var streaming = view.Streaming with
        {
            ForestLoadRadius = view.Streaming.ForestLoadRadius * forests,
            ForestUnloadRadius = view.Streaming.ForestUnloadRadius * forests,
            TerrainDetailRadius = view.Streaming.TerrainDetailRadius * settings.TerrainQuality,
            TerrainCoarseRadius = view.Streaming.TerrainCoarseRadius * settings.TerrainQuality,
        };
        var cover = view.GroundCover with { Radius = view.GroundCover.Radius * settings.Vegetation };
        return content with { SettlementView = view with { Citizens = citizens, Streaming = streaming, GroundCover = cover } };
    }

    /// <summary>Terrain3D's finest mesh for a terrain quality: 48 vertices a side at full quality, down to 24, in steps of 8.</summary>
    /// <param name="quality">The terrain quality (0.5..1).</param>
    /// <returns>The mesh size.</returns>
    public static int TerrainMeshSize(float quality) =>
        Math.Clamp((int)MathF.Round(FullTerrainMesh * quality / 8f) * 8, LeastTerrainMesh, FullTerrainMesh);
}
