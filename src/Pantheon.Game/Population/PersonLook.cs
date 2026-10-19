using System;
using System.Collections.Generic;
using Pantheon.Game.Presentation;

namespace Pantheon.Game.Population;

/// <summary>What a person wears on their head.</summary>
public enum PersonHeadwear
{
    /// <summary>Bare-headed: their hair shows.</summary>
    None,

    /// <summary>A wide straw hat.</summary>
    StrawHat,

    /// <summary>A scarf over the hair.</summary>
    Scarf,
}

/// <summary>How one person looks (ADR-049).</summary>
/// <param name="Clothes">The colour of their tunic or dress.</param>
/// <param name="Skin">Their skin tone, 0 (the first of <see cref="PeopleSettings.SkinTones"/>) to 1 (the last).</param>
/// <param name="Hair">Index into <see cref="PeopleSettings.HairColors"/>.</param>
/// <param name="Headwear">What they wear on their head.</param>
/// <param name="Dress">Whether they wear a long dress rather than a tunic and trousers.</param>
/// <param name="Height">Their height as a share of the figure's.</param>
/// <param name="Staff">Whether they carry a staff, as prophets and elders do.</param>
public readonly record struct PersonLook(RgbColor Clothes, float Skin, int Hair, PersonHeadwear Headwear, bool Dress, float Height, bool Staff = false)
{
    /// <summary>The look packed for the person shader: hair + 8 × headwear + 32 × dress + 64 × staff.</summary>
    public float Packed => Hair + (8 * (int)Headwear) + (Dress ? 32 : 0) + (Staff ? 64 : 0);
}

/// <summary>
/// Gives each person a look of their own from <see cref="PresentationHash"/> of who they are, so
/// the same person always looks the same and showing them changes nothing in the simulation. Pure.
/// </summary>
public static class PersonLooks
{
    private const ulong Salt = 80;

    /// <summary>A crowd figure's look.</summary>
    /// <param name="settings">People settings.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="group">Their village, or another group's id.</param>
    /// <param name="index">Their place in it.</param>
    /// <param name="role">Their work, which picks their clothes and headwear.</param>
    /// <returns>The look.</returns>
    public static PersonLook Of(PeopleSettings settings, ulong seed, int group, int index, CitizenRole role)
    {
        ArgumentNullException.ThrowIfNull(settings);
        float U(ulong k) => PresentationHash.Unit(Salt, seed, PresentationHash.Mix((ulong)(uint)group, (ulong)(uint)index), k);
        var (clothes, headwearChance, headwear) = role switch
        {
            CitizenRole.Farmer => (settings.FarmerClothes, settings.FarmerHatChance, PersonHeadwear.StrawHat),
            CitizenRole.Builder => (settings.BuilderClothes, settings.BuilderScarfChance, PersonHeadwear.Scarf),
            CitizenRole.Villager => (settings.VillagerClothes, settings.VillagerScarfChance, PersonHeadwear.Scarf),
        };
        return new PersonLook(
            Pick(clothes, U(0)),
            U(1),
            Math.Min(settings.HairColors.Count - 1, (int)(U(2) * settings.HairColors.Count)),
            U(3) < headwearChance ? headwear : PersonHeadwear.None,
            role != CitizenRole.Builder && U(4) < settings.DressChance,
            settings.MinHeight + ((settings.MaxHeight - settings.MinHeight) * U(5)));
    }

    /// <summary>
    /// A prophet's or an elder's look: their own skin, hair and height, in a long robe of their god's
    /// colour, with a staff.
    /// </summary>
    /// <param name="settings">People settings.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="notableId">The notable's id.</param>
    /// <param name="robe">Their god's colour.</param>
    /// <returns>The look.</returns>
    public static PersonLook Notable(PeopleSettings settings, ulong seed, int notableId, RgbColor robe) =>
        Of(settings, seed, -1 - notableId, 0, CitizenRole.Villager) with { Clothes = robe, Dress = true, Headwear = PersonHeadwear.None, Staff = true };

    /// <summary>One of a list, by a unit value.</summary>
    private static RgbColor Pick(IReadOnlyList<RgbColor> colors, float unit) => colors[Math.Min(colors.Count - 1, (int)(unit * colors.Count))];
}
