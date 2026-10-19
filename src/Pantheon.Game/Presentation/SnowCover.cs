using System;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Snow lying on the land (ADR-028) as an image of one pixel per region, white with the snow's depth
/// as its opacity, for the client to drape over the terrain as it does the map overlays.
/// </summary>
public static class SnowCover
{
    // Less snow than this is not drawn, and the deepest snow is drawn this opaque.
    private const float Shown = 0.02f;
    private const float MaxOpacity = 0.9f;

    private static readonly RgbColor White = RgbColor.ParseHex("#f4f7fb");

    /// <summary>The snow image, or <c>null</c> where no snow lies.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The image.</returns>
    public static OverlayImage? Build(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var cells = world.Regions.Cells;
        var rgba = new byte[cells.Count * 4];
        var any = false;
        for (var i = 0; i < cells.Count; i++)
        {
            var snow = Math.Clamp(cells[i].Snow, 0f, 1f);
            if (snow < Shown)
            {
                continue;
            }

            any = true;
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], rgba[(i * 4) + 3]) =
                (RgbColor.ToByte(White.R), RgbColor.ToByte(White.G), RgbColor.ToByte(White.B), RgbColor.ToByte(snow * MaxOpacity));
        }

        return any ? new OverlayImage(world.Regions.Columns, world.Regions.Rows, rgba, "Snow") : null;
    }
}
