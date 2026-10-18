using System;
using System.Collections.Generic;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>
/// Structural checks on a deserialized world. JSON binding only guarantees types; these checks
/// catch hand-edited or corrupted saves that would otherwise fail later inside the simulation.
/// </summary>
internal static class SaveIntegrity
{
    /// <summary>Throws <see cref="SaveFormatException"/> listing every inconsistency found.</summary>
    /// <param name="world">Freshly deserialized world.</param>
    public static void Validate(WorldState world)
    {
        var errors = new List<string>();

        if (world.Tick < 0)
        {
            errors.Add("Tick must not be negative.");
        }

        if ((world.Random.S0 | world.Random.S1 | world.Random.S2 | world.Random.S3) == 0)
        {
            errors.Add("Random state must not be all zeros.");
        }

        ValidateRegions(world, errors);
        ValidateSettlements(world, errors);

        if (errors.Count > 0)
        {
            throw new SaveFormatException("The save file is inconsistent:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors));
        }
    }

    /// <summary>Checks that the grid matches the generation settings and that cell ids are sequential.</summary>
    private static void ValidateRegions(WorldState world, List<string> errors)
    {
        var grid = world.Regions;
        var generation = world.Generation;

        if (grid.Columns != generation.RegionsPerSide || grid.Rows != generation.RegionsPerSide)
        {
            errors.Add($"Region grid is {grid.Columns}x{grid.Rows} but the generation settings specify {generation.RegionsPerSide} regions per side.");
            return;
        }

        if (generation.RegionsPerSide > 0)
        {
            var expectedSize = generation.HeightmapResolution / generation.RegionsPerSide * generation.VertexSpacing;
            if (grid.RegionSize != expectedSize)
            {
                errors.Add($"RegionSize is {grid.RegionSize} but the generation settings imply {expectedSize}.");
            }
        }

        if (grid.Cells.Count != grid.Columns * grid.Rows)
        {
            errors.Add($"Region grid has {grid.Cells.Count} cells but {grid.Columns * grid.Rows} are required.");
            return;
        }

        for (var i = 0; i < grid.Cells.Count; i++)
        {
            if (grid.Cells[i] is not { } cell)
            {
                errors.Add($"Region {i} is null.");
            }
            else if (cell.Id != i)
            {
                errors.Add($"Region at index {i} has id {cell.Id}.");
            }
        }
    }

    /// <summary>Checks settlement ids, region references, non-negative quantities and health in 0..1.</summary>
    private static void ValidateSettlements(WorldState world, List<string> errors)
    {
        var ids = new HashSet<int>();
        var regionCount = world.Regions.Cells.Count;

        foreach (var settlement in world.Settlements)
        {
            if (settlement is null)
            {
                errors.Add("Settlement list contains null.");
                continue;
            }

            if (!ids.Add(settlement.Id))
            {
                errors.Add($"Settlement id {settlement.Id} is used more than once.");
            }

            if (settlement.RegionId < 0 || settlement.RegionId >= regionCount)
            {
                errors.Add($"Settlement {settlement.Id} references missing region {settlement.RegionId}.");
            }

            if (settlement.Population < 0 || settlement.Housing < 0 || settlement.Food < 0f)
            {
                errors.Add($"Settlement {settlement.Id} has negative population, housing or food.");
            }

            if (!(settlement.Health is >= 0f and <= 1f))
            {
                errors.Add($"Settlement {settlement.Id} has health {settlement.Health}; it must lie between 0 and 1.");
            }

            if (settlement.ConstructionProgress < 0f)
            {
                errors.Add($"Settlement {settlement.Id} has negative construction progress.");
            }
        }
    }
}
