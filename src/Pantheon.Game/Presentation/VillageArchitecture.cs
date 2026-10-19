using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>How a village's houses look.</summary>
/// <param name="Style">How they are built.</param>
/// <param name="TwoStoreys">Whether they have an upper storey.</param>
/// <param name="Wall">The walls' colour.</param>
/// <param name="Roof">The roofs' colour.</param>
/// <param name="Motif">The god's style the houses take, if any.</param>
public readonly record struct VillageLook(HouseStyle Style, bool TwoStoreys, RgbColor Wall, RgbColor Roof, StyleMotif? Motif);

/// <summary>
/// How a village builds (spec §61, ADR-052): its land picks the style (adobe in the desert, stilts
/// in the swamp, stone in the highlands and on volcanic land, timber in forests and the tundra,
/// round huts elsewhere, longhouses once those villages grow), its size a second storey, and the
/// god most of it follows, when strongly felt, the colours: turf roofs for a god of nature and
/// growth, dark timber and roofs for a god of war and death, whitewash and blue for a god of
/// learning. Pure; the look changes as the village does.
/// </summary>
public static class VillageArchitecture
{
    private const ulong Salt = 52;

    /// <summary>The style a land builds in, before a village grows.</summary>
    /// <param name="biome">The land's biome.</param>
    /// <returns>The style.</returns>
    public static HouseStyle StyleOf(Biome biome) => biome switch
    {
        Biome.Desert => HouseStyle.Adobe,
        Biome.Swamp => HouseStyle.StiltHouse,
        Biome.Highland => HouseStyle.StoneCottage,
        Biome.Volcanic => HouseStyle.StoneCottage,
        Biome.Forest => HouseStyle.Longhouse,
        Biome.Tundra => HouseStyle.Longhouse,
        Biome.Grassland => HouseStyle.RoundHut,
        Biome.RiverValley => HouseStyle.RoundHut,
        Biome.Coast => HouseStyle.RoundHut,
        Biome.Island => HouseStyle.RoundHut,
        Biome.Sea => HouseStyle.StiltHouse,
    };

    /// <summary>A village's look.</summary>
    /// <param name="settlement">The village.</param>
    /// <param name="world">The world, for its land and its gods.</param>
    /// <param name="houses">Its houses now.</param>
    /// <param name="settings">Architecture settings.</param>
    /// <param name="style">The god's style in the village (ADR-042), if any.</param>
    /// <returns>The look.</returns>
    public static VillageLook Of(SettlementState settlement, WorldState world, int houses, ArchitectureSettings settings, ReligiousStyle? style)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        var biome = world.Regions.TryGetRegionIdAt(settlement.Position.X, settlement.Position.Z, out var region) ? world.Regions.Cells[region].Biome : Biome.Grassland;
        var built = StyleOf(biome);
        built = built == HouseStyle.RoundHut && houses >= settings.LonghouseHouses ? HouseStyle.Longhouse : built;
        var colors = settings.Styles[built];
        var pick = PresentationHash.Mix(Salt, world.Seed.Value, (ulong)(uint)settlement.Id);
        var (wall, roof) = (colors.Walls[(int)(pick % (ulong)colors.Walls.Count)], colors.Roofs[(int)((pick >> 16) % (ulong)colors.Roofs.Count)]);
        StyleMotif? motif = style is { } felt && felt.Strength >= settings.MotifStrength ? felt.Motif : null;
        (wall, roof) = motif switch
        {
            StyleMotif.Garden => (wall, settings.GardenRoof),
            StyleMotif.Monument => (settings.MonumentWall, settings.MonumentRoof),
            StyleMotif.Lore => (settings.LoreWall, settings.LoreRoof),
            null => (wall, roof),
        };
        return new VillageLook(built, HouseMesh.HasUpperStorey(built) && houses >= settings.TwoStoreyHouses, wall, roof, motif);
    }

    /// <summary>A house's own shade of the village's colours, so no two look quite alike.</summary>
    /// <param name="settings">Architecture settings.</param>
    /// <param name="settlementId">The village.</param>
    /// <param name="slot">The house's place in the village.</param>
    /// <returns>A brightness factor around 1.</returns>
    public static float ShadeOf(ArchitectureSettings settings, int settlementId, int slot)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return 1f + ((PresentationHash.Unit(Salt, (ulong)(uint)settlementId, (ulong)(uint)slot) - 0.5f) * 2f * settings.HouseVariation);
    }
}
