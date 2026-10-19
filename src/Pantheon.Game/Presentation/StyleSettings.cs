using System;
using System.Collections.Generic;

namespace Pantheon.Game.Presentation;

/// <summary>How religion shows in villages (spec §61, ADR-042), from <c>settlement_view.json</c>. Presentation only.</summary>
public sealed record StyleSettings
{
    /// <summary>Weight of the share of the god's domains that belong to a motif.</summary>
    public required float DomainWeight { get; init; }

    /// <summary>Weight of the village's strongest doctrine of a motif.</summary>
    public required float DoctrineWeight { get; init; }

    /// <summary>Weight of the god having a trait of a motif.</summary>
    public required float TraitWeight { get; init; }

    /// <summary>Weight of the people's love (gardens), fear (monuments) or half their faith (lore).</summary>
    public required float FeelingWeight { get; init; }

    /// <summary>The least strength (score times the god's share) for a style to show.</summary>
    public required float Threshold { get; init; }

    /// <summary>The most pieces a village raises, at full strength and size.</summary>
    public required int MaxPieces { get; init; }

    /// <summary>Metres from the shrine of the ring of monuments or columns, and where gardens begin.</summary>
    public required float ShrineRingRadius { get; init; }

    /// <summary>Metres between garden slots on their spiral.</summary>
    public required float GardenSpacing { get; init; }

    /// <summary>Metres a garden keeps clear of a house.</summary>
    public required float HouseClearance { get; init; }

    /// <summary>Colours of flower beds.</summary>
    public required IReadOnlyList<RgbColor> FlowerColors { get; init; }

    /// <summary>Colour of bushes.</summary>
    public required RgbColor Bush { get; init; }

    /// <summary>Colour of obelisks.</summary>
    public required RgbColor Obelisk { get; init; }

    /// <summary>Colour of a brazier's flame.</summary>
    public required RgbColor Flame { get; init; }

    /// <summary>Colour of columns.</summary>
    public required RgbColor Column { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(DomainWeight >= 0f && DoctrineWeight >= 0f && TraitWeight >= 0f && FeelingWeight >= 0f))
        {
            throw new ArgumentException("The weights must not be negative.");
        }

        if (!(Threshold is >= 0f and <= 1f) || MaxPieces < 2 || !(ShrineRingRadius > 0f) || !(GardenSpacing > 0f) || !(HouseClearance >= 0f))
        {
            throw new ArgumentException("Threshold must lie in 0..1, MaxPieces be at least 2, and the radius and spacing positive.");
        }

        if (FlowerColors is null || FlowerColors.Count == 0)
        {
            throw new ArgumentException("FlowerColors must hold a colour.");
        }
    }
}
