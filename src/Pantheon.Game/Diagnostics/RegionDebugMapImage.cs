namespace Pantheon.Game.Diagnostics;

/// <summary>The F3 debug map: one RGBA8 pixel per region, row-major like the region grid's cells.</summary>
/// <param name="Width">Pixels across, one per region column.</param>
/// <param name="Height">Pixels down, one per region row.</param>
/// <param name="Rgba">Four bytes per pixel.</param>
/// <param name="Legend">What the colours mean, one item per line.</param>
public sealed record RegionDebugMapImage(int Width, int Height, byte[] Rgba, string Legend);
