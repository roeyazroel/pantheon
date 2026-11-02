using System;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>
/// A village's temple and fortifications as meshes built in code (spec §61, ADR-052). The temple
/// takes the motif of the god it is raised to: a columned hall for a god of learning, a domed round
/// temple for a god of nature, a dark stepped pyramid with a brazier for a god of war, a plain hall
/// otherwise; its roof takes the god's colour. Walls are a palisade of pointed stakes with timber
/// towers, or crenellated stone with round stone towers where the village builds in stone. Pure.
/// </summary>
public static class CivicMesh
{
    /// <summary>The height at which a shrine's cradle holds its light, in metres.</summary>
    public const float ShrineTop = 8.5f;

    /// <summary>A village's shrine (ADR-078): three worn steps, a tapering shaft with carved bands and a flared cradle for its light, all stone.</summary>
    /// <param name="detailed">The detailed shrine or the simple one.</param>
    /// <returns>The triangles.</returns>
    public static TaggedMesh Shrine(bool detailed)
    {
        const int stone = (int)BuildingSlot.Stone;
        var b = new MeshBuilder(detailed);
        var sides = detailed ? 16 : 8;
        Vector3 At(float y) => new(0f, y, 0f);
        b.Tube(At(-0.6f), At(0.4f), 3.3f, 3.2f, 1f, sides, 0, stone, capped: true);
        b.Tube(At(0.4f), At(0.85f), 2.6f, 2.5f, 1f, sides, 0, stone, capped: true);
        b.Tube(At(0.85f), At(1.3f), 1.95f, 1.85f, 1f, sides, 0, stone, capped: true);
        b.Tube(At(1.3f), At(1.7f), 1.1f, 0.72f, 1f, sides, 0, stone, capped: true);
        b.Tube(At(1.7f), At(7.5f), 0.66f, 0.44f, 1f, sides, 0, stone);
        foreach (var (y, radius) in new[] { (3.1f, 0.72f), (5.3f, 0.64f), (7.2f, 0.56f) })
        {
            b.Tube(At(y - 0.1f), At(y + 0.1f), radius, radius, 1f, sides, 0, stone, capped: true);
        }

        b.Tube(At(7.5f), At(ShrineTop), 0.44f, 1.05f, 1f, sides, 0, stone, capped: true);
        return b.Finish();
    }

    /// <summary>A temple.</summary>
    /// <param name="motif">The style of the god it is raised to, if strongly felt.</param>
    /// <param name="detailed">The detailed temple or the simple one.</param>
    /// <returns>The triangles: its front to +Z.</returns>
    public static TaggedMesh Temple(StyleMotif? motif, bool detailed)
    {
        var b = new MeshBuilder(detailed);
        var (wall, roof, stone, trim) = ((int)BuildingSlot.Wall, (int)BuildingSlot.Roof, (int)BuildingSlot.Stone, (int)BuildingSlot.Trim);
        switch (motif)
        {
            case StyleMotif.Lore:
                b.Box(new Vector3(0f, 0f, 0.6f), new Vector3(7.2f, 1f, 7.4f), 0, stone);
                b.Box(new Vector3(0f, 2f, -0.6f), new Vector3(5.4f, 3.2f, 4.4f), 0, wall);
                foreach (var x in new[] { -2.5f, -1.5f, -0.5f, 0.5f, 1.5f, 2.5f })
                {
                    b.Tube(new Vector3(x, 0.5f, 3.2f), new Vector3(x, 3.6f, 3.2f), 0.24f, 0.2f, 1f, detailed ? 8 : 5, 0, wall);
                }

                b.Box(new Vector3(0f, 3.75f, 0.6f), new Vector3(6.4f, 0.35f, 6.6f), 0, wall);
                b.Gable(new Vector3(0f, 3.9f, 0.6f), 6.6f, 6.8f, 1.6f, 0, roof, wall);
                break;
            case StyleMotif.Garden:
                b.Tube(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 0.3f, 0f), 3.6f, 3.5f, 1f, detailed ? 16 : 8, 0, stone, capped: true);
                b.Tube(new Vector3(0f, 0.3f, 0f), new Vector3(0f, 3f, 0f), 2.6f, 2.5f, 1f, detailed ? 16 : 8, 0, wall);
                b.Ball(new Vector3(0f, 3f, 0f), new Vector3(2.9f, 2.4f, 2.9f), 0, roof, lowerHalf: false);
                for (var i = 0; i < 8; i++)
                {
                    var angle = MathF.Tau * i / 8f;
                    var at = new Vector3(MathF.Cos(angle) * 3.2f, 0f, MathF.Sin(angle) * 3.2f);
                    b.Tube(at + new Vector3(0f, 0.3f, 0f), at + new Vector3(0f, 3.3f, 0f), 0.16f, 0.13f, 1f, detailed ? 6 : 4, 0, trim);
                }

                b.Box(new Vector3(0f, 1.1f, 2.5f), new Vector3(1.1f, 1.8f, 0.2f), 0, (int)BuildingSlot.Door);
                break;
            case StyleMotif.Monument:
                foreach (var (size, y) in new[] { (7.4f, 0.1f), (5.8f, 1.3f), (4.2f, 2.5f) })
                {
                    b.Box(new Vector3(0f, y, 0f), new Vector3(size, 1.2f + (y < 1f ? 1f : 0f), size), 0, wall);
                }

                b.Box(new Vector3(0f, 4f, 0f), new Vector3(2.6f, 1.8f, 2.6f), 0, wall);
                b.Box(new Vector3(0f, 5.05f, 0f), new Vector3(3f, 0.3f, 3f), 0, roof);
                b.Box(new Vector3(0f, 1.6f, 3.4f), new Vector3(1.4f, 3.2f, 1.6f), 0, stone);
                b.Tube(new Vector3(0f, 5.2f, 0f), new Vector3(0f, 6f, 0f), 0.5f, 0.75f, 1f, detailed ? 8 : 5, 0, trim, capped: true);
                break;
            case null:
                b.Box(new Vector3(0f, 0f, 0f), new Vector3(6.6f, 1f, 5.2f), 0, stone);
                b.Box(new Vector3(0f, 1.9f, 0f), new Vector3(5.5f, 3f, 4f), 0, wall);
                b.Gable(new Vector3(0f, 3.4f, 0f), 6.2f, 4.9f, 2f, 0, roof, wall);
                b.Box(new Vector3(0f, 1.3f, 2.02f), new Vector3(1.1f, 1.8f, 0.15f), 0, (int)BuildingSlot.Door);
                break;
        }

        return b.Finish();
    }

    /// <summary>One stake of a palisade: a post with a pointed top, its foot at the origin.</summary>
    /// <param name="detailed">The detailed stake or the simple one.</param>
    /// <returns>The triangles.</returns>
    public static TaggedMesh Stake(bool detailed)
    {
        var b = new MeshBuilder(detailed);
        var sides = detailed ? 6 : 4;
        b.Tube(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 2.7f, 0f), 0.17f, 0.15f, 1f, sides, 0, (int)BuildingSlot.Trim);
        b.Tube(new Vector3(0f, 2.7f, 0f), new Vector3(0f, 3.3f, 0f), 0.15f, 0f, 1f, sides, 0, (int)BuildingSlot.Trim);
        return b.Finish();
    }

    /// <summary>A length of crenellated stone wall, 2.7 m long along X, its foot's middle at the origin.</summary>
    /// <param name="detailed">The detailed wall or the simple one.</param>
    /// <returns>The triangles.</returns>
    public static TaggedMesh WallSegment(bool detailed)
    {
        var b = new MeshBuilder(detailed);
        b.Box(new Vector3(0f, 1.15f, 0f), new Vector3(2.7f, 3.3f, 0.9f), 0, (int)BuildingSlot.Wall);
        foreach (var x in new[] { -0.9f, 0.45f })
        {
            b.Box(new Vector3(x, 3.1f, 0f), new Vector3(0.75f, 0.6f, 0.9f), 0, (int)BuildingSlot.Wall);
        }

        return b.Finish();
    }

    /// <summary>A watchtower by a gate: of timber on four posts under a pyramid roof, or a round stone tower under a cone.</summary>
    /// <param name="stone">Whether it is of stone.</param>
    /// <param name="detailed">The detailed tower or the simple one.</param>
    /// <returns>The triangles: its foot at the origin.</returns>
    public static TaggedMesh Tower(bool stone, bool detailed)
    {
        var b = new MeshBuilder(detailed);
        var (trim, roof) = ((int)BuildingSlot.Trim, (int)BuildingSlot.Roof);
        if (stone)
        {
            var sides = detailed ? 12 : 6;
            b.Tube(new Vector3(0f, -0.5f, 0f), new Vector3(0f, 6.2f, 0f), 1.8f, 1.6f, 1f, sides, 0, (int)BuildingSlot.Wall);
            b.Tube(new Vector3(0f, 6.2f, 0f), new Vector3(0f, 6.6f, 0f), 1.9f, 1.9f, 1f, sides, 0, (int)BuildingSlot.Wall, capped: true);
            b.Tube(new Vector3(0f, 6.6f, 0f), new Vector3(0f, 8.8f, 0f), 2.1f, 0f, 1f, sides, 0, roof, capped: true);
            b.Box(new Vector3(0f, 4.5f, 1.65f), new Vector3(0.35f, 0.8f, 0.2f), 0, (int)BuildingSlot.Window);
            return b.Finish();
        }

        foreach (var (x, z) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
        {
            b.Tube(new Vector3(x * 1.1f, -0.5f, z * 1.1f), new Vector3(x * 1f, 6.4f, z * 1f), 0.15f, 0.13f, 1f, detailed ? 6 : 4, 0, trim);
        }

        b.Box(new Vector3(0f, 5f, 0f), new Vector3(2.8f, 0.22f, 2.8f), 0, trim);
        foreach (var (x, z, w, d) in new[] { (0f, 1.35f, 2.8f, 0.1f), (0f, -1.35f, 2.8f, 0.1f), (1.35f, 0f, 0.1f, 2.8f), (-1.35f, 0f, 0.1f, 2.8f) })
        {
            b.Box(new Vector3(x, 5.5f, z), new Vector3(w, 0.8f, d), 0, trim);
        }

        b.Tube(new Vector3(0f, 6.3f, 0f), new Vector3(0f, 7.8f, 0f), 2.2f, 0f, 1f, 4, 0, roof, capped: true);
        return b.Finish();
    }
}
