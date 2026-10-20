using System;
using System.Collections.Generic;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;

namespace Pantheon.Game.Presentation;

/// <summary>
/// The gods' territories (spec §10, ADR-069): the land where a god's influence is the strongest of all
/// and at least the threshold its miracles need. Each god's lead (its influence less the greatest of
/// the threshold and every other god's) is sampled over a grid: its territory is where the lead is
/// above nothing, and its border where the lead is nil, so two gods' borders meet where their
/// influence is equal. Pure: it reads the influence fields only, so it can run off the main thread.
/// </summary>
public sealed class Territories
{
    /// <summary>The influence a territory needs: the least most miracles ask (miracles.json).</summary>
    public const float Threshold = 0.1f;

    /// <summary>The most grid cells along a side, so a vast island costs no more than a small one.</summary>
    public const int MaxCells = 160;

    /// <summary>The finest cell, in metres.</summary>
    public const float FinestCell = 16f;

    private readonly float[][] _lead;

    private Territories(int[] godIds, float[][] lead, int side, float cell, float[] shares)
    {
        (GodIds, _lead, Side, Cell) = (godIds, lead, side, cell);
        var byGod = new Dictionary<int, float>();
        for (var g = 0; g < godIds.Length; g++)
        {
            byGod[godIds[g]] = shares[g];
        }

        Shares = byGod;
    }

    /// <summary>The gods, in the fields' order.</summary>
    public IReadOnlyList<int> GodIds { get; }

    /// <summary>Samples along a side of the grid.</summary>
    public int Side { get; }

    /// <summary>The grid's cell, in metres.</summary>
    public float Cell { get; }

    /// <summary>The island's width, in metres.</summary>
    public float Extent => (Side - 1) * Cell;

    /// <summary>Each god's share of the land it holds (0..1), by god id.</summary>
    public IReadOnlyDictionary<int, float> Shares { get; }

    /// <summary>Every god's influence field, read from the world (on the main thread).</summary>
    /// <param name="rules">Divine rules.</param>
    /// <param name="world">The world.</param>
    /// <returns>The fields, one per god.</returns>
    public static IReadOnlyList<InfluenceField> Fields(Domain.Belief.DivineRules rules, WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var fields = new List<InfluenceField>();
        foreach (var god in world.Gods)
        {
            fields.Add(InfluenceField.Build(rules, world, god.Id));
        }

        return fields;
    }

    /// <summary>Samples every god's lead over the island.</summary>
    /// <param name="fields">The gods' influence fields.</param>
    /// <param name="extent">The island's width, in metres: the grid spans 0 to it on both axes.</param>
    /// <param name="threshold">The influence a territory needs.</param>
    /// <returns>The territories.</returns>
    public static Territories Build(IReadOnlyList<InfluenceField> fields, float extent, float threshold = Threshold)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var cells = Math.Clamp((int)MathF.Ceiling(extent / FinestCell), 2, MaxCells);
        var cell = extent / cells;
        var side = cells + 1;
        var influence = new float[fields.Count][];
        for (var g = 0; g < fields.Count; g++)
        {
            influence[g] = new float[side * side];
            for (var k = 0; k < side * side; k++)
            {
                influence[g][k] = fields[g].At(new GroundPosition(k % side * cell, k / side * cell));
            }
        }

        var lead = new float[fields.Count][];
        var shares = new float[fields.Count];
        var gods = new int[fields.Count];
        for (var g = 0; g < fields.Count; g++)
        {
            gods[g] = fields[g].GodId;
            lead[g] = new float[side * side];
            var held = 0;
            for (var k = 0; k < side * side; k++)
            {
                var others = threshold;
                for (var h = 0; h < fields.Count; h++)
                {
                    others = h == g ? others : MathF.Max(others, influence[h][k]);
                }

                lead[g][k] = influence[g][k] - others;
                held += lead[g][k] > 0f ? 1 : 0;
            }

            shares[g] = held / (float)(side * side);
        }

        return new Territories(gods, lead, side, cell, shares);
    }

    /// <summary>A god's lead at a point, between the grid's samples (bilinear): above 0 inside its territory.</summary>
    /// <param name="god">The god's index in <see cref="GodIds"/>.</param>
    /// <param name="x">World X, in metres.</param>
    /// <param name="z">World Z, in metres.</param>
    /// <returns>The lead.</returns>
    public float Lead(int god, float x, float z) => LeadAndSlope(god, x, z).Lead;

    /// <summary>
    /// A god's lead at a point and how far the point lies inside its border, in metres: the lead over
    /// how steeply it changes there. Negative outside.
    /// </summary>
    /// <param name="god">The god's index in <see cref="GodIds"/>.</param>
    /// <param name="x">World X, in metres.</param>
    /// <param name="z">World Z, in metres.</param>
    /// <returns>The lead and the distance inside.</returns>
    public (float Lead, float Inside) LeadAndSlope(int god, float x, float z)
    {
        var field = _lead[god];
        var fx = Math.Clamp(x / Cell, 0f, Side - 1.001f);
        var fz = Math.Clamp(z / Cell, 0f, Side - 1.001f);
        var (i, j) = ((int)fx, (int)fz);
        var (u, v) = (fx - i, fz - j);
        var k = (j * Side) + i;
        var (a, b, c, d) = (field[k], field[k + 1], field[k + Side], field[k + Side + 1]);
        var lead = a + ((b - a) * u) + (((c + ((d - c) * u)) - (a + ((b - a) * u))) * v);
        var gradX = ((b - a) + (((d - c) - (b - a)) * v)) / Cell;
        var gradZ = ((c - a) + (((d - b) - (c - a)) * u)) / Cell;
        var steep = MathF.Sqrt((gradX * gradX) + (gradZ * gradZ));
        return (lead, steep > 1e-6f ? lead / steep : MathF.Sign(lead) * float.MaxValue);
    }
}
