using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;

namespace Pantheon.Game.Presentation;

/// <summary>The biomes in words (spec §5, ADR-038). Pure.</summary>
public static class BiomeText
{
    /// <summary>A biome's name in a sentence.</summary>
    /// <param name="biome">The biome.</param>
    /// <returns>For example "river valley".</returns>
    public static string Name(Biome biome) => biome switch
    {
        Biome.Sea => "sea",
        Biome.Grassland => "grassland",
        Biome.Forest => "forest",
        Biome.Highland => "highland",
        Biome.Desert => "desert",
        Biome.Tundra => "tundra",
        Biome.Swamp => "swamp",
        Biome.Coast => "coast",
        Biome.RiverValley => "river valley",
        Biome.Island => "island",
        Biome.Volcanic => "volcanic land",
    };

    /// <summary>The island's land by biome, largest share first.</summary>
    /// <param name="regions">The region grid.</param>
    /// <returns>For example "grassland 41%, forest 22%, coast 12%, highland 9%, river valley 7%..."; empty without land.</returns>
    public static string Census(RegionGrid regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var counts = new Dictionary<Biome, int>();
        var land = 0;
        foreach (var region in regions.Cells)
        {
            if (region.Biome != Biome.Sea)
            {
                counts[region.Biome] = counts.GetValueOrDefault(region.Biome) + 1;
                land++;
            }
        }

        return land == 0 ? string.Empty : string.Join(", ", counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key)
            .Select(c => string.Format(CultureInfo.InvariantCulture, "{0} {1:0%}", Name(c.Key), c.Value / (float)land)));
    }
}
