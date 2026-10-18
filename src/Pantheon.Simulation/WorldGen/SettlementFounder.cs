using System;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Chooses the site and name of the settlement founded at world creation.</summary>
public static class SettlementFounder
{
    /// <summary>Id given to the first settlement of a world.</summary>
    public const int StartingSettlementId = 1;

    /// <summary>Minimum terrain height at the site centre, so villages never stand in the surf.</summary>
    private const float MinimumSiteHeight = 1.5f;

    /// <summary>Maximum slope (rise over run) at the site centre.</summary>
    private const float MaximumSiteSlope = 0.35f;

    /// <summary>Founds the starting settlement on the best habitable region.</summary>
    /// <param name="grid">Generated region grid.</param>
    /// <param name="terrain">Generated terrain, used to reject wet or steep sites.</param>
    /// <param name="seed">World seed; it picks the name.</param>
    /// <param name="settings">Starting settlement parameters.</param>
    /// <returns>The new settlement state.</returns>
    public static SettlementState Found(RegionGrid grid, TerrainField terrain, WorldSeed seed, StartingSettlementSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var regionId = SelectSite(grid, terrain, settings.CenterPreference);
        var names = new SimulationRandom(SeedMixer.Derive(seed, "settlement.names"));

        return new SettlementState
        {
            Id = StartingSettlementId,
            Name = settings.Names[names.NextInt(settings.Names.Count)],
            RegionId = regionId,
            Position = grid.GetRegionCenter(regionId),
            Population = settings.Population,
            Food = settings.Food,
            Housing = settings.Housing,
        };
    }

    /// <summary>
    /// Returns the habitable region with the best score, where score = fertility minus
    /// <paramref name="centerPreference"/> times the normalised distance to the island centre.
    /// Ties go to the lowest id.
    /// </summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="terrain">Terrain used for site height and slope checks.</param>
    /// <param name="centerPreference">Weight of the distance penalty.</param>
    /// <returns>The id of the chosen region.</returns>
    /// <exception cref="InvalidOperationException">No region is habitable.</exception>
    public static int SelectSite(RegionGrid grid, TerrainField terrain, float centerPreference)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(terrain);

        var centerX = grid.Columns * grid.RegionSize * 0.5f;
        var centerZ = grid.Rows * grid.RegionSize * 0.5f;
        var bestId = -1;
        var bestScore = float.NegativeInfinity;

        foreach (var region in grid.Cells)
        {
            var site = grid.GetRegionCenter(region.Id);
            if (!IsHabitable(region, terrain, site))
            {
                continue;
            }

            var dx = (site.X - centerX) / centerX;
            var dz = (site.Z - centerZ) / centerZ;
            var score = region.Fertility - (centerPreference * MathF.Sqrt((dx * dx) + (dz * dz)));
            if (score > bestScore)
            {
                bestScore = score;
                bestId = region.Id;
            }
        }

        return bestId >= 0
            ? bestId
            : throw new InvalidOperationException("The generated world has no habitable land region for the starting settlement.");
    }

    /// <summary>Land region with fertile soil and a dry, gentle site at its centre.</summary>
    private static bool IsHabitable(RegionState region, TerrainField terrain, GroundPosition site)
    {
        if (region.Biome == Biome.Sea || region.Fertility <= 0f)
        {
            return false;
        }

        var sampleX = (int)MathF.Round(site.X / terrain.Spacing);
        var sampleZ = (int)MathF.Round(site.Z / terrain.Spacing);
        return terrain.SampleHeight(site.X, site.Z) >= MinimumSiteHeight && terrain.GetSlope(sampleX, sampleZ) <= MaximumSiteSlope;
    }
}
