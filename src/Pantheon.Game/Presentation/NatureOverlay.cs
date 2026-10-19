using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Ecology;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The Nature overlay (spec §18, ADR-028): every land region from red (lifeless) to green (rich in
/// life) by its biodiversity, scarred land in dark violet and snow in white.
/// </summary>
internal static class NatureOverlay
{
    /// <summary>The overlay's legend.</summary>
    public const string Legend = "Nature: the land's life from red (lifeless) to green (rich), scarred land in dark violet, snow in white";

    // Snow and scars this deep take over a region's colour.
    private const float Lying = 0.3f;

    private static readonly RgbColor Barren = RgbColor.ParseHex("#b8402a");
    private static readonly RgbColor Rich = RgbColor.ParseHex("#2f9e44");
    private static readonly RgbColor Scarred = RgbColor.ParseHex("#4a2a5c");
    private static readonly RgbColor SnowWhite = RgbColor.ParseHex("#f4f7fb");

    /// <summary>Paints every land region.</summary>
    /// <param name="world">World state.</param>
    /// <param name="opacity">Overlay opacity.</param>
    /// <param name="paint">Paints one region: region, colour, alpha.</param>
    public static void Paint(WorldState world, float opacity, Action<int, RgbColor, float> paint)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(paint);
        foreach (var region in world.Regions.Cells)
        {
            if (region.Biome == Biome.Sea)
            {
                continue;
            }

            var color = region.Snow >= Lying ? SnowWhite
                : region.Scar >= Lying ? Scarred
                : RgbColor.Lerp(Barren, Rich, Math.Clamp(EcologyModel.Biodiversity(region) / 0.8f, 0f, 1f));
            paint(region.Id, color, opacity);
        }
    }
}
