using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;

namespace Pantheon.Persistence;

/// <summary>
/// Structural checks on a deserialized world. JSON binding only guarantees types; these checks
/// catch hand-edited or corrupted saves that would otherwise fail later inside the simulation.
/// </summary>
internal static class SaveIntegrity
{
    // Single-precision sums of shares may overshoot 1 by a rounding error.
    private const float AdherenceTolerance = 1e-4f;

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
        var godIds = ValidateGods(world, errors);
        var winners = new HashSet<int>();
        foreach (var winner in world.WinnerGodIds)
        {
            if (!godIds.Contains(winner) || !winners.Add(winner))
            {
                errors.Add($"The winners name missing or repeated god {winner}.");
            }
        }

        ValidateSettlements(world, godIds, errors);
        CreatureIntegrity.Validate(world, godIds, errors);
        DiplomacyIntegrity.Validate(world, godIds, errors);
        WeatherIntegrity.Validate(world, godIds, errors);
        DisasterIntegrity.Validate(world, godIds, errors);
        MigrationIntegrity.Validate(world, godIds, errors);
        HistoryIntegrity.Validate(world, godIds, errors);
        HolySiteIntegrity.Validate(world, godIds, errors);
        WaterIntegrity.Validate(world, errors);

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
            else if (!IsShare(cell.Blessing))
            {
                errors.Add($"Region {i} has blessing {cell.Blessing}; it must lie between 0 and 1.");
            }
            else if (!IsShare(cell.SoilMoisture))
            {
                errors.Add($"Region {i} has soil moisture {cell.SoilMoisture}; it must lie between 0 and 1.");
            }
            else if (!IsShare(cell.Fire) || !IsShare(cell.Scorch))
            {
                errors.Add($"Region {i} has fire {cell.Fire} or scorch {cell.Scorch}; both must lie between 0 and 1.");
            }
            else if (!IsShare(cell.FreshWater) || !IsShare(cell.Flood))
            {
                errors.Add($"Region {i} has fresh water {cell.FreshWater} or flood {cell.Flood}; both must lie between 0 and 1.");
            }
        }
    }

    /// <summary>Checks god ids, Divine Power, ranks and cooldowns.</summary>
    /// <returns>The ids of the gods.</returns>
    private static HashSet<int> ValidateGods(WorldState world, List<string> errors)
    {
        var ids = new HashSet<int>();
        foreach (var god in world.Gods)
        {
            if (god is null)
            {
                errors.Add("God list contains null.");
                continue;
            }

            if (!ids.Add(god.Id))
            {
                errors.Add($"God id {god.Id} is used more than once.");
            }

            if (!float.IsFinite(god.DivinePower) || god.DivinePower < 0f)
            {
                errors.Add($"God {god.Id} has Divine Power {god.DivinePower}; it must be finite and not negative.");
            }

            if (!Enum.IsDefined(god.Rank) || !Enum.IsDefined(god.Controller) || !Enum.IsDefined(god.Personality) || !Enum.IsDefined(god.RefugeePolicy))
            {
                errors.Add($"God {god.Id} has an unknown rank, controller, personality or refugee policy.");
            }

            if (god.DominantSinceTick is { } since && (since < 0 || since > world.Tick))
            {
                errors.Add($"God {god.Id} dominates since tick {since}, outside 0..{world.Tick}.");
            }

            if (god.Cooldowns.Exists(c => c is null || string.IsNullOrEmpty(c.MiracleId)))
            {
                errors.Add($"God {god.Id} has a cooldown without a miracle.");
            }

            if (god.Domains is null || god.Domains.Exists(d => !Enum.IsDefined(d)) || new HashSet<Domain.Gods.GodDomain>(god.Domains).Count != god.Domains.Count)
            {
                errors.Add($"God {god.Id} has no domain list, an unknown domain or a domain twice.");
            }
        }

        return ids;
    }

    /// <summary>
    /// Checks a population's beliefs (a settlement's or a migration group's): known gods, one entry
    /// each, values in 0..1, shares adding up to at most 1.
    /// </summary>
    /// <param name="beliefs">The beliefs.</param>
    /// <param name="owner">How errors name the population, for example "Settlement 3".</param>
    /// <param name="godIds">Ids of the world's gods.</param>
    /// <param name="errors">Error list to append to.</param>
    internal static void ValidateBeliefs(IReadOnlyList<GodBelief> beliefs, string owner, HashSet<int> godIds, List<string> errors)
    {
        var seen = new HashSet<int>();
        var total = 0f;
        foreach (var belief in beliefs)
        {
            if (belief is null)
            {
                errors.Add($"{owner} has a null belief.");
                continue;
            }

            if (!godIds.Contains(belief.GodId) || !seen.Add(belief.GodId))
            {
                errors.Add($"{owner} has a belief in missing or repeated god {belief.GodId}.");
            }

            if (!IsShare(belief.Adherence) || !IsShare(belief.Faith) || !IsShare(belief.Love) || !IsShare(belief.Fear) || !IsShare(belief.Doubt))
            {
                errors.Add($"{owner} has a belief in god {belief.GodId} with a value outside 0..1.");
            }

            total += belief.Adherence;
        }

        if (total > 1f + AdherenceTolerance)
        {
            errors.Add($"{owner} has followers adding up to {total}; the shares must add up to at most 1.");
        }
    }

    /// <summary>Whether a value lies in 0..1.</summary>
    internal static bool IsShare(float value) => value is >= 0f and <= 1f;

    /// <summary>Checks settlement ids, region references, non-negative quantities, health in 0..1 and beliefs.</summary>
    private static void ValidateSettlements(WorldState world, HashSet<int> godIds, List<string> errors)
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

            if (settlement.Population < 0 || settlement.Housing < 0 || settlement.Food < 0f || !(settlement.Water >= 0f) || !float.IsFinite(settlement.Water))
            {
                errors.Add($"Settlement {settlement.Id} has negative population, housing, food or water.");
            }

            if (!(settlement.Health is >= 0f and <= 1f))
            {
                errors.Add($"Settlement {settlement.Id} has health {settlement.Health}; it must lie between 0 and 1.");
            }

            if (settlement.ConstructionProgress < 0f || settlement.Ruins < 0 || settlement.ShieldedUntilTick < 0)
            {
                errors.Add($"Settlement {settlement.Id} has negative construction progress, ruins or shield.");
            }

            if (!float.IsFinite(settlement.Emigration) || settlement.Emigration < 0f)
            {
                errors.Add($"Settlement {settlement.Id} has emigration {settlement.Emigration}; it must be finite and not negative.");
            }

            ValidateBeliefs(settlement.Beliefs, $"Settlement {settlement.Id}", godIds, errors);
            HolySiteIntegrity.ValidateFaith(world, settlement, godIds, errors);
            SocietyIntegrity.Validate(settlement, godIds, errors);
        }
    }
}
