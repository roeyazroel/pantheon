using System;
using System.Collections.Generic;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Domain.World.Generation;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Simulation.WorldGen;

/// <summary>Chooses the sites, names and sizes of the settlements founded at world creation.</summary>
public static class SettlementFounder
{
    /// <summary>Id given to the first settlement of a world; the others follow in order.</summary>
    public const int StartingSettlementId = 1;

    /// <summary>Health of a newly founded settlement: its founders are healthy.</summary>
    public const float InitialHealth = 1f;

    /// <summary>Minimum terrain height at the site centre, so villages never stand in the surf.</summary>
    private const float MinimumSiteHeight = 1.5f;

    /// <summary>Maximum slope (rise over run) at the site centre.</summary>
    private const float MaximumSiteSlope = 0.35f;

    /// <summary>
    /// Founds up to <see cref="StartingSettlementSettings.Count"/> settlements on the best habitable
    /// regions that lie at least <see cref="StartingSettlementSettings.MinimumSpacing"/> regions from
    /// each other, best first. Names are drawn from the pool without repeats and populations vary by
    /// up to <see cref="StartingSettlementSettings.PopulationVariation"/>, both from seeded streams.
    /// </summary>
    /// <param name="grid">Generated region grid.</param>
    /// <param name="terrain">Generated terrain, used to reject wet or steep sites.</param>
    /// <param name="seed">World seed; it picks the names and populations.</param>
    /// <param name="settings">Starting settlement parameters.</param>
    /// <returns>The settlements, ids from <see cref="StartingSettlementId"/>; at least one.</returns>
    /// <exception cref="InvalidOperationException">No region is habitable.</exception>
    public static List<SettlementState> FoundAll(RegionGrid grid, TerrainField terrain, WorldSeed seed, StartingSettlementSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var sites = SelectSites(grid, terrain, settings.CenterPreference, settings.Count, settings.MinimumSpacing);
        var names = ShuffledNames(seed, settings.Names);
        var sizes = new SimulationRandom(SeedMixer.Derive(seed, "settlement.founding"));
        var settlements = new List<SettlementState>(sites.Count);
        for (var i = 0; i < sites.Count; i++)
        {
            var variation = settings.PopulationVariation * ((2f * sizes.NextFloat()) - 1f);
            var population = Math.Clamp((int)MathF.Round(settings.Population * (1f + variation)), 1, settings.Housing);
            settlements.Add(new SettlementState
            {
                Id = StartingSettlementId + i,
                Name = names[i],
                RegionId = sites[i],
                Position = grid.GetRegionCenter(sites[i]),
                Population = population,
                Food = settings.Food,
                Housing = settings.Housing,
                Health = InitialHealth,
                ConstructionProgress = 0f,
                Beliefs = [],
            });
        }

        return settlements;
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> habitable regions, best score first, each at least
    /// <paramref name="spacing"/> regions (Chebyshev distance) from the ones before it. The score is
    /// fertility minus <paramref name="centerPreference"/> times the normalised distance to the island
    /// centre; ties go to the lowest id.
    /// </summary>
    /// <param name="grid">Region grid.</param>
    /// <param name="terrain">Terrain used for site height and slope checks.</param>
    /// <param name="centerPreference">Weight of the distance penalty.</param>
    /// <param name="count">Most sites wanted.</param>
    /// <param name="spacing">Least distance between sites, in regions.</param>
    /// <returns>Region ids of the sites; at least one.</returns>
    /// <exception cref="InvalidOperationException">No region is habitable.</exception>
    public static IReadOnlyList<int> SelectSites(RegionGrid grid, TerrainField terrain, float centerPreference, int count, int spacing)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(terrain);
        var candidates = new List<(float Score, int Id)>();
        var centerX = grid.Columns * grid.RegionSize * 0.5f;
        var centerZ = grid.Rows * grid.RegionSize * 0.5f;
        foreach (var region in grid.Cells)
        {
            var site = grid.GetRegionCenter(region.Id);
            if (IsHabitable(region, terrain, site))
            {
                var dx = (site.X - centerX) / centerX;
                var dz = (site.Z - centerZ) / centerZ;
                candidates.Add((region.Fertility - (centerPreference * MathF.Sqrt((dx * dx) + (dz * dz))), region.Id));
            }
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("The generated world has no habitable land region for the starting settlement.");
        }

        candidates.Sort(static (a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Id.CompareTo(b.Id));
        var chosen = new List<int>();
        foreach (var (_, id) in candidates)
        {
            if (chosen.Count >= count)
            {
                break;
            }

            if (chosen.TrueForAll(other => ChebyshevDistance(grid, id, other) >= spacing))
            {
                chosen.Add(id);
            }
        }

        return chosen;
    }

    /// <summary>The name pool in an order drawn from the world seed (Fisher–Yates).</summary>
    private static List<string> ShuffledNames(WorldSeed seed, IReadOnlyList<string> pool)
    {
        var names = new List<string>(pool);
        var random = new SimulationRandom(SeedMixer.Derive(seed, "settlement.names"));
        for (var i = names.Count - 1; i > 0; i--)
        {
            var j = random.NextInt(i + 1);
            (names[i], names[j]) = (names[j], names[i]);
        }

        return names;
    }

    /// <summary>Grid distance between two regions, counting diagonal steps as one.</summary>
    private static int ChebyshevDistance(RegionGrid grid, int a, int b)
    {
        var (columnA, rowA) = grid.GetCoordinates(a);
        var (columnB, rowB) = grid.GetCoordinates(b);
        return Math.Max(Math.Abs(columnA - columnB), Math.Abs(rowA - rowB));
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
