using System;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.World;
using Pantheon.Game.Disasters;

namespace Pantheon.Game.Presentation;

/// <summary>
/// What the hazards of ADR-029 leave on the ground, for the client to draw: blighted fields and
/// landslide rubble as an image of one pixel per region (draped over the terrain like the snow),
/// and the colour and fading of the marks earthquakes, landslides and meteors leave.
/// </summary>
public static class HazardCover
{
    // Less rubble than this is not drawn; the deepest rubble and the worst blight are drawn this opaque.
    private const float RubbleShown = 0.02f;
    private const float MaxOpacity = 0.85f;

    private static readonly RgbColor Rubble = RgbColor.ParseHex("#6e6253");
    private static readonly RgbColor Blighted = RgbColor.ParseHex("#8a7a2e");
    private static readonly RgbColor Crack = RgbColor.ParseHex("#2b2119");
    private static readonly RgbColor Slide = RgbColor.ParseHex("#5a4a38");
    private static readonly RgbColor Crater = RgbColor.ParseHex("#1c1612");

    /// <summary>The cover image, or <c>null</c> where no field is blighted and no rubble lies.</summary>
    /// <param name="world">World state.</param>
    /// <returns>The image: rubble in grey-brown, blight in sickly yellow, as opaque as each is deep.</returns>
    public static OverlayImage? Build(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var cells = world.Regions.Cells;
        var rgba = new byte[cells.Count * 4];
        var any = false;
        for (var i = 0; i < cells.Count; i++)
        {
            var (color, depth) = cells[i].Rubble >= RubbleShown
                ? (Rubble, cells[i].Rubble)
                : cells[i].Blight >= DisasterText.BlightShown ? (Blighted, cells[i].Blight) : (Rubble, 0f);
            if (depth <= 0f)
            {
                continue;
            }

            any = true;
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], rgba[(i * 4) + 3]) =
                (RgbColor.ToByte(color.R), RgbColor.ToByte(color.G), RgbColor.ToByte(color.B), RgbColor.ToByte(Math.Clamp(depth, 0f, 1f) * MaxOpacity));
        }

        return any ? new OverlayImage(world.Regions.Columns, world.Regions.Rows, rgba, "Blight and rubble") : null;
    }

    /// <summary>The colour of a mark on the land.</summary>
    /// <param name="kind">What left it.</param>
    /// <returns>Dark earth for a crack, brown for a slide, near black for a crater.</returns>
    public static RgbColor MarkColor(TerrainMarkKind kind) => kind switch
    {
        TerrainMarkKind.Crack => Crack,
        TerrainMarkKind.Slide => Slide,
        TerrainMarkKind.Crater => Crater,
    };

    /// <summary>How opaque a mark is drawn: fully when new, fading to nothing as it reaches the age at which it is forgotten.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="tick">The current tick.</param>
    /// <param name="hazards">Hazard rules, for how long marks last.</param>
    /// <returns>Opacity (0..1).</returns>
    public static float MarkOpacity(TerrainMark mark, long tick, HazardRules hazards)
    {
        ArgumentNullException.ThrowIfNull(mark);
        ArgumentNullException.ThrowIfNull(hazards);
        var life = hazards.MarkSeconds * SimulationTime.TicksPerSecond;
        return life <= 0f ? 0f : Math.Clamp(1f - ((tick - mark.Tick) / life), 0f, 1f);
    }
}
