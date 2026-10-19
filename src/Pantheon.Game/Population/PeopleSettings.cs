using System;
using System.Collections.Generic;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Population;

/// <summary>How people look (spec §61, §80, ADR-049), from <c>settlement_view.json</c>. Presentation only.</summary>
public sealed record PeopleSettings
{
    /// <summary>Skin tones from lightest to darkest; a person's tone lies between two neighbours.</summary>
    public required IReadOnlyList<RgbColor> SkinTones { get; init; }

    /// <summary>Hair colours.</summary>
    public required IReadOnlyList<RgbColor> HairColors { get; init; }

    /// <summary>Farmers' clothes.</summary>
    public required IReadOnlyList<RgbColor> FarmerClothes { get; init; }

    /// <summary>Builders' clothes.</summary>
    public required IReadOnlyList<RgbColor> BuilderClothes { get; init; }

    /// <summary>Everyone else's clothes.</summary>
    public required IReadOnlyList<RgbColor> VillagerClothes { get; init; }

    /// <summary>Trousers, mixed with the tunic's colour.</summary>
    public required RgbColor Trousers { get; init; }

    /// <summary>Straw hats.</summary>
    public required RgbColor Straw { get; init; }

    /// <summary>Shoes, belts and the hoe's blade.</summary>
    public required RgbColor Leather { get; init; }

    /// <summary>Tool handles.</summary>
    public required RgbColor Wood { get; init; }

    /// <summary>Share of farmers in a straw hat.</summary>
    public required float FarmerHatChance { get; init; }

    /// <summary>Share of builders with a scarf tied over their hair.</summary>
    public required float BuilderScarfChance { get; init; }

    /// <summary>Share of everyone else in a scarf.</summary>
    public required float VillagerScarfChance { get; init; }

    /// <summary>Share of farmers and villagers in a long dress.</summary>
    public required float DressChance { get; init; }

    /// <summary>The shortest person, as a share of the figure's height.</summary>
    public required float MinHeight { get; init; }

    /// <summary>The tallest.</summary>
    public required float MaxHeight { get; init; }

    /// <summary>Beyond this distance from the camera, in metres, a village's people use the simpler figure.</summary>
    public required float DetailDistance { get; init; }

    /// <summary>How far the legs swing each way while walking, in radians.</summary>
    public required float LegSwing { get; init; }

    /// <summary>How far the arms swing each way while walking, in radians.</summary>
    public required float ArmSwing { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        var errors = new List<string>();
        if (SkinTones.Count < 2 || HairColors.Count == 0 || FarmerClothes.Count == 0 || BuilderClothes.Count == 0 || VillagerClothes.Count == 0 || HairColors.Count > 8)
        {
            errors.Add("People need at least two skin tones, one to eight hair colours and clothes for every role.");
        }

        if (!(FarmerHatChance is >= 0f and <= 1f) || !(BuilderScarfChance is >= 0f and <= 1f) || !(VillagerScarfChance is >= 0f and <= 1f) || !(DressChance is >= 0f and <= 1f))
        {
            errors.Add("The chances of hats, scarves and dresses must lie in 0..1.");
        }

        if (!(MinHeight > 0f) || !(MaxHeight >= MinHeight) || !(DetailDistance > 0f) || !(LegSwing is >= 0f and <= 1.2f) || !(ArmSwing is >= 0f and <= 1.2f))
        {
            errors.Add("Heights must be positive with MinHeight <= MaxHeight, DetailDistance positive, and the swings within 0..1.2 radians.");
        }

        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }
}
