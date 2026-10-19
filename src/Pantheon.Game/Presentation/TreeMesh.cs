using System;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>Which part of a tree a vertex belongs to, for the tree shader.</summary>
public enum TreeSlot
{
    /// <summary>The trunk and branches.</summary>
    Trunk,

    /// <summary>The leaves, needles or fronds.</summary>
    Leaves,
}

/// <summary>
/// The trees as meshes built in code (spec §61, §93, ADR-053): seven species, each standing on the
/// ground at the origin, with its trunk and its leaves tagged for the tree shader, which bends them
/// in the wind by their height. Pure.
/// </summary>
public static class TreeMesh
{
    /// <summary>A tree.</summary>
    /// <param name="kind">Its species.</param>
    /// <param name="detailed">The detailed tree or the simple one.</param>
    /// <returns>The triangles.</returns>
    public static TaggedMesh Build(TreeKind kind, bool detailed)
    {
        var b = new MeshBuilder(detailed);
        var (round, limb) = (detailed ? 8 : 5, detailed ? 6 : 4);
        var (trunk, leaves) = ((int)TreeSlot.Trunk, (int)TreeSlot.Leaves);
        switch (kind)
        {
            case TreeKind.Pine:
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 3.2f, 0f), 0.26f, 0.15f, 1f, limb, 0, trunk);
                foreach (var (low, high, radius) in new[] { (1.8f, 4.4f, 1.9f), (3.2f, 5.6f, 1.5f), (4.5f, 6.8f, 1.1f), (5.8f, 7.8f, 0.7f) })
                {
                    b.Tube(new Vector3(0f, low, 0f), new Vector3(0f, high, 0f), radius, 0f, 1f, round, 0, leaves, capped: true);
                }

                break;
            case TreeKind.Fir:
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 2f, 0f), 0.22f, 0.16f, 1f, limb, 0, trunk);
                for (var tier = 0; tier < 5; tier++)
                {
                    var low = 1.1f + (tier * 1.35f);
                    b.Tube(new Vector3(0f, low, 0f), new Vector3(0f, low + 2.2f, 0f), 1.45f - (tier * 0.22f), 0f, 1f, round, 0, leaves, capped: true);
                }

                break;
            case TreeKind.Oak:
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 2.8f, 0f), 0.4f, 0.28f, 1f, limb, 0, trunk);
                b.Tube(new Vector3(0f, 2.4f, 0f), new Vector3(1.1f, 3.7f, 0.3f), 0.18f, 0.1f, 1f, limb, 0, trunk);
                b.Tube(new Vector3(0f, 2.5f, 0f), new Vector3(-0.9f, 3.8f, -0.5f), 0.17f, 0.1f, 1f, limb, 0, trunk);
                foreach (var (centre, radius) in new[] { (new Vector3(0f, 4.6f, 0f), 2.1f), (new Vector3(1.3f, 4.1f, 0.4f), 1.5f), (new Vector3(-1.2f, 4.2f, -0.5f), 1.5f), (new Vector3(0.2f, 4.3f, -1.3f), 1.4f), (new Vector3(-0.3f, 5.6f, 0.6f), 1.3f) })
                {
                    b.Ball(centre, new Vector3(radius, radius * 0.8f, radius), 0, leaves);
                }

                break;
            case TreeKind.Birch:
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 6.2f, 0f), 0.15f, 0.07f, 1f, limb, 0, trunk);
                foreach (var (centre, radius) in new[] { (new Vector3(0f, 5.2f, 0f), 1.1f), (new Vector3(0.5f, 4.2f, 0.2f), 0.9f), (new Vector3(-0.4f, 3.6f, -0.3f), 0.85f), (new Vector3(0.1f, 6.3f, -0.2f), 0.8f) })
                {
                    b.Ball(centre, new Vector3(radius, radius * 1.3f, radius), 0, leaves);
                }

                break;
            case TreeKind.Palm:
                var top = new Vector3(0.9f, 6.4f, 0f);
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 2.4f, 0f), 0.28f, 0.22f, 1f, limb, 0, trunk);
                b.Tube(new Vector3(0.2f, 2.4f, 0f), new Vector3(0.55f, 4.6f, 0f), 0.22f, 0.19f, 1f, limb, 0, trunk);
                b.Tube(new Vector3(0.55f, 4.6f, 0f), top, 0.19f, 0.16f, 1f, limb, 0, trunk);
                for (var frond = 0; frond < 7; frond++)
                {
                    var angle = MathF.Tau * frond / 7f;
                    var reach = new Vector3(MathF.Cos(angle) * 2.8f, -1.1f, MathF.Sin(angle) * 2.8f);
                    b.Tube(top, top + reach, 0.35f, 0.05f, 0.18f, 4, 0, leaves);
                }

                break;
            case TreeKind.Willow:
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0f, 2.6f, 0f), 0.36f, 0.26f, 1f, limb, 0, trunk);
                b.Ball(new Vector3(0f, 4.3f, 0f), new Vector3(2.6f, 1.7f, 2.6f), 0, leaves, lowerHalf: false);
                var strands = detailed ? 14 : 8;
                for (var strand = 0; strand < strands; strand++)
                {
                    // Long switches of leaves hang from the dome's rim nearly to the ground.
                    var angle = MathF.Tau * (strand + (0.5f * (strand % 2))) / strands;
                    var (dx, dz) = (MathF.Cos(angle), MathF.Sin(angle));
                    var rim = new Vector3(dx * 2.3f, 4.2f, dz * 2.3f);
                    b.Tube(rim, rim + new Vector3(dx * 0.4f, -2.6f - (0.6f * (strand % 3) / 2f), dz * 0.4f), 0.42f, 0.08f, 0.5f, 4, 0, leaves);
                }
                break;
            case TreeKind.Snag:
                b.Tube(new Vector3(0f, -0.3f, 0f), new Vector3(0.2f, 5.2f, 0f), 0.3f, 0.06f, 1f, limb, 0, trunk);
                b.Tube(new Vector3(0.05f, 2.6f, 0f), new Vector3(1.3f, 3.9f, 0.3f), 0.12f, 0.03f, 1f, 4, 0, trunk);
                b.Tube(new Vector3(0.1f, 3.4f, 0f), new Vector3(-1f, 4.6f, -0.4f), 0.1f, 0.03f, 1f, 4, 0, trunk);
                b.Tube(new Vector3(0.15f, 4.1f, 0f), new Vector3(0.6f, 5f, 0.9f), 0.08f, 0.02f, 1f, 4, 0, trunk);
                break;
        }

        return b.Finish();
    }
}
