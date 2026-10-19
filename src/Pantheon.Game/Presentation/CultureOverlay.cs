using System;
using Pantheon.Domain.Culture;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>The Culture overlay (ADR-024): each settlement's lands in the colour of the doctrine it holds most, grey where it has turned from the gods.</summary>
internal static class CultureOverlay
{
    /// <summary>The overlay's legend.</summary>
    public const string Legend = "Culture: pacifism white, militarism red, sacrifice purple, charity gold, nature worship green, knowledge blue, expansion orange, isolation brown; grey where people have turned from the gods";

    // A doctrine colours a settlement's lands from this hold, and secularism from this level.
    private const float Held = 0.25f;
    private const float Secular = 0.3f;

    private static readonly RgbColor Godless = RgbColor.ParseHex("#7a7a7a");

    /// <summary>Paints every living settlement's lands.</summary>
    /// <param name="world">World state.</param>
    /// <param name="opacity">Overlay opacity.</param>
    /// <param name="paint">Paints a settlement's lands around a region: region, colour, alpha.</param>
    public static void Paint(WorldState world, float opacity, Action<int, RgbColor, float> paint)
    {
        foreach (var settlement in world.Settlements)
        {
            if (settlement.Population <= 0)
            {
                continue;
            }

            var culture = settlement.Culture;
            if (culture.Secularism >= Secular)
            {
                paint(settlement.RegionId, Godless, Math.Min(1f, culture.Secularism + 0.2f) * opacity);
                continue;
            }

            var (strongest, hold) = Strongest(culture);
            if (hold >= Held)
            {
                paint(settlement.RegionId, ColorOf(strongest), Math.Min(1f, hold + 0.2f) * opacity);
            }
        }
    }

    /// <summary>The doctrine a culture holds most.</summary>
    private static (Doctrine Doctrine, float Hold) Strongest(CultureState culture)
    {
        var best = (Doctrine.Pacifism, culture.Pacifism);
        foreach (var doctrine in Enum.GetValues<Doctrine>())
        {
            if (culture.Get(doctrine) > best.Item2)
            {
                best = (doctrine, culture.Get(doctrine));
            }
        }

        return best;
    }

    /// <summary>A doctrine's colour.</summary>
    private static RgbColor ColorOf(Doctrine doctrine) => RgbColor.ParseHex(doctrine switch
    {
        Doctrine.Pacifism => "#f2f2ee",
        Doctrine.Militarism => "#c0392b",
        Doctrine.Sacrifice => "#7d3c98",
        Doctrine.Charity => "#f1c40f",
        Doctrine.NatureWorship => "#27ae60",
        Doctrine.KnowledgeSeeking => "#2e86c1",
        Doctrine.Expansionism => "#e67e22",
        Doctrine.Isolationism => "#8d6e63",
    });
}
