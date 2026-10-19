using System;
using System.Collections.Generic;
using Pantheon.Domain.Belief;
using Pantheon.Domain.World;

namespace Pantheon.Simulation.Belief;

/// <summary>
/// A god's divine influence (spec §10, ADR-009). It is derived from the world, never stored: each
/// settlement with followers is a source whose strength is its adherence times Faith and whose
/// reach grows with the square root of its followers, the god's Creature carries a source
/// with it (ADR-010) and every holy site it keeps is one (ADR-021). Sources combine like independent chances,
/// <c>1 − Π(1 − influence)</c>, so overlapping sources add up but never pass 1. Several gods can
/// hold influence over the same place.
/// </summary>
public sealed class InfluenceField
{
    private readonly InfluenceSource[] _sources;

    private InfluenceField(int godId, InfluenceSource[] sources)
    {
        GodId = godId;
        _sources = sources;
    }

    /// <summary>The god whose influence this is.</summary>
    public int GodId { get; }

    /// <summary>The sources of the influence.</summary>
    public IReadOnlyList<InfluenceSource> Sources => _sources;

    /// <summary>Collects a god's influence sources from the world.</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="world">World state.</param>
    /// <param name="godId">The god.</param>
    /// <returns>The god's influence field.</returns>
    public static InfluenceField Build(DivineRules rules, WorldState world, int godId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(world);
        var sources = new List<InfluenceSource>();
        foreach (var settlement in world.Settlements)
        {
            if (BeliefModel.Find(settlement, godId) is not { } belief)
            {
                continue;
            }

            var followers = Math.Max(0, settlement.Population) * SimMath.Clamp01(belief.Adherence);
            var strength = Math.Min(1f, rules.InfluenceStrength * SimMath.Clamp01(belief.Adherence) * SimMath.Clamp01(belief.Faith));
            if (strength > 0f)
            {
                var reach = rules.InfluenceBaseRadius + (rules.InfluenceRadiusPerSqrtFollower * MathF.Sqrt(followers));
                sources.Add(new InfluenceSource(settlement.Position, strength, reach));
            }
        }

        foreach (var creature in world.Creatures)
        {
            if (creature.GodId == godId && creature.Activity.Phase != Domain.Creatures.CreaturePhase.Banished && rules.CreatureInfluenceStrength > 0f)
            {
                sources.Add(new InfluenceSource(creature.Position, Math.Min(1f, rules.CreatureInfluenceStrength), rules.CreatureInfluenceReach * Creatures.CreatureAttributes.Affinity(rules, creature)));
            }
        }

        foreach (var site in world.HolySites)
        {
            if (site.KeeperGodId == godId && rules.HolySiteInfluenceStrength > 0f)
            {
                sources.Add(new InfluenceSource(site.Position, rules.HolySiteInfluenceStrength, rules.HolySiteInfluenceReach));
            }
        }

        return new InfluenceField(godId, [.. sources]);
    }

    /// <summary>The god's influence at a point.</summary>
    /// <param name="position">Ground position.</param>
    /// <returns>Influence (0..1).</returns>
    public float At(GroundPosition position)
    {
        var none = 1f;
        foreach (var source in _sources)
        {
            var dx = position.X - source.Position.X;
            var dz = position.Z - source.Position.Z;
            none *= 1f - source.At(MathF.Sqrt((dx * dx) + (dz * dz)));
        }

        return SimMath.Clamp01(1f - none);
    }

    /// <summary>The god's influence at the centre of every region, in region id order.</summary>
    /// <param name="grid">Region grid.</param>
    /// <returns>Influence per region (0..1).</returns>
    public float[] SampleRegions(RegionGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var values = new float[grid.Cells.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = At(grid.GetRegionCenter(i));
        }

        return values;
    }
}
