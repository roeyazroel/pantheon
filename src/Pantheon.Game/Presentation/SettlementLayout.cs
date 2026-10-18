using System;
using System.Collections.Generic;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// Deterministic visual layout of a settlement: huts on a sunflower spiral around a central
/// shrine. The hut count follows the population, so growth is visible on screen.
/// </summary>
public static class SettlementLayout
{
    /// <summary>People represented by one hut.</summary>
    public const int PeoplePerHut = 5;

    /// <summary>Radius kept free around the shrine, in metres.</summary>
    public const float ShrineClearance = 9f;

    /// <summary>Spacing factor of the spiral, in metres.</summary>
    public const float HutSpacing = 6.5f;

    /// <summary>Huts are not placed on ground lower than this height (metres), which keeps them out of the surf.</summary>
    public const float MinimumGroundHeight = 0.6f;

    private const int AttemptsPerHut = 3;
    private const float MinimumScale = 0.85f;
    private const float ScaleRange = 0.3f;
    private const float YawJitter = 0.35f;

    private static readonly float GoldenAngle = MathF.PI * (3f - MathF.Sqrt(5f));

    /// <summary>Number of huts shown: one per <see cref="PeoplePerHut"/> inhabitants, capped by housing.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <returns>Hut count; at least 1 while anyone lives there.</returns>
    public static int ComputeHutCount(SettlementState settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        if (settlement.Population <= 0)
        {
            return 0;
        }

        var forPopulation = (settlement.Population + PeoplePerHut - 1) / PeoplePerHut;
        var forHousing = Math.Max(1, (settlement.Housing + PeoplePerHut - 1) / PeoplePerHut);
        return Math.Min(forPopulation, forHousing);
    }

    /// <summary>Computes hut placements. Positions over water are skipped.</summary>
    /// <param name="settlement">Settlement.</param>
    /// <param name="field">Terrain used for heights.</param>
    /// <param name="hutCount">Number of huts to place.</param>
    /// <returns>Up to <paramref name="hutCount"/> placements, the same for the same inputs.</returns>
    public static IReadOnlyList<HutPlacement> ComputeHuts(SettlementState settlement, TerrainField field, int hutCount)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentOutOfRangeException.ThrowIfNegative(hutCount);

        var huts = new List<HutPlacement>(hutCount);
        var center = new Vector2(settlement.Position.X, settlement.Position.Z);
        for (var slot = 0; huts.Count < hutCount && slot < hutCount * AttemptsPerHut; slot++)
        {
            var angle = slot * GoldenAngle;
            var radius = ShrineClearance + (HutSpacing * MathF.Sqrt(slot + 0.5f));
            var ground = center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
            var height = field.SampleHeight(ground.X, ground.Y);
            if (height < MinimumGroundHeight)
            {
                continue;
            }

            var jitter = Hash01(settlement.Id, slot);
            var facing = MathF.Atan2(center.X - ground.X, center.Y - ground.Y) + ((jitter - 0.5f) * YawJitter);
            huts.Add(new HutPlacement(new Vector3(ground.X, height, ground.Y), facing, MinimumScale + (ScaleRange * jitter)));
        }

        return huts;
    }

    /// <summary>Deterministic value in 0..1 for a settlement and slot.</summary>
    private static float Hash01(int settlementId, int slot)
    {
        var hash = unchecked(((ulong)(uint)settlementId << 32) | (uint)slot);
        hash = unchecked((hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL);
        hash = unchecked((hash ^ (hash >> 27)) * 0x94D049BB133111EBUL);
        hash ^= hash >> 31;
        return (hash >> 40) / (float)(1UL << 24);
    }
}
