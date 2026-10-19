using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>The parts of a person's figure, which the person shader moves on their own (ADR-049).</summary>
public enum PersonPart
{
    /// <summary>Tunic, shoulders and belt.</summary>
    Torso,

    /// <summary>Neck and head.</summary>
    Head,

    /// <summary>The left leg and shoe (on +X: the figure faces +Z).</summary>
    LeftLeg,

    /// <summary>The right leg and shoe.</summary>
    RightLeg,

    /// <summary>The left arm and hand.</summary>
    LeftArm,

    /// <summary>The right arm and hand.</summary>
    RightArm,

    /// <summary>Hair, hidden under a scarf.</summary>
    Hair,

    /// <summary>A straw hat.</summary>
    StrawHat,

    /// <summary>A scarf over the hair.</summary>
    Scarf,

    /// <summary>A hoe in the right hand.</summary>
    Hoe,

    /// <summary>A hammer in the right hand.</summary>
    Hammer,

    /// <summary>A long dress.</summary>
    Dress,

    /// <summary>A staff in the left hand, for prophets and elders.</summary>
    Staff,
}

/// <summary>Which colour a surface takes; the person shader paints each.</summary>
public enum PersonSlot
{
    /// <summary>The tunic or dress (the instance colour).</summary>
    Clothes,

    /// <summary>Skin.</summary>
    Skin,

    /// <summary>Trousers.</summary>
    Trousers,

    /// <summary>Hair.</summary>
    Hair,

    /// <summary>Straw.</summary>
    Straw,

    /// <summary>Leather: shoes, belt, the hoe's blade.</summary>
    Leather,

    /// <summary>Wood: tool handles.</summary>
    Wood,

    /// <summary>A scarf: the clothes' colour, lightened.</summary>
    Scarf,
}

/// <summary>A person's figure as triangles: feet at the origin, facing +Z, 1.7 m tall. Each vertex's UV holds its part and colour slot.</summary>
/// <param name="Positions">Vertex positions.</param>
/// <param name="Normals">Vertex normals.</param>
/// <param name="Tags">Each vertex's part (X) and slot (Y).</param>
/// <param name="Indices">Triangles, three indices each, wound for Godot (clockwise seen from outside).</param>
public sealed record PersonMeshData(Vector3[] Positions, Vector3[] Normals, Vector2[] Tags, int[] Indices);

/// <summary>
/// Builds the person figure (spec §61, §80, ADR-049) in two levels of detail, with the joints the
/// person shader turns the limbs about. Pure, so its proportions are tested headless.
/// </summary>
public static class PersonMesh
{
    /// <summary>The hips, about which the legs swing and the body bends (metres above the feet).</summary>
    public const float HipHeight = 0.88f;

    /// <summary>The shoulders, about which the arms swing.</summary>
    public const float ShoulderHeight = 1.41f;

    /// <summary>The neck, about which the head nods.</summary>
    public const float NeckHeight = 1.5f;

    /// <summary>The figure's height without a hat.</summary>
    public const float Height = 1.74f;

    /// <summary>Builds the figure.</summary>
    /// <param name="detailed">The detailed figure for people near the camera, or the simple one for those far off.</param>
    /// <returns>The triangles.</returns>
    public static PersonMeshData Build(bool detailed)
    {
        var b = new MeshBuilder(detailed);
        var (round, limb) = (detailed ? 8 : 5, detailed ? 6 : 4);
        foreach (var (side, leg, arm) in new[] { (1f, (int)PersonPart.LeftLeg, (int)PersonPart.LeftArm), (-1f, (int)PersonPart.RightLeg, (int)PersonPart.RightArm) })
        {
            b.Tube(new Vector3(side * 0.095f, 0.07f, 0f), new Vector3(side * 0.1f, HipHeight + 0.02f, 0f), 0.06f, 0.088f, 1f, limb, leg, (int)PersonSlot.Trousers);
            b.Box(new Vector3(side * 0.095f, 0.045f, 0.035f), new Vector3(0.1f, 0.09f, 0.23f), leg, (int)PersonSlot.Leather);
            b.Tube(new Vector3(side * 0.24f, ShoulderHeight + 0.01f, 0f), new Vector3(side * 0.255f, 0.91f, 0.01f), 0.062f, 0.05f, 1f, limb, arm, (int)PersonSlot.Clothes);
            b.Ball(new Vector3(side * 0.255f, 0.86f, 0.015f), new Vector3(0.045f, 0.055f, 0.045f), arm, (int)PersonSlot.Skin);
        }

        b.Tube(new Vector3(0f, 0.72f, 0f), new Vector3(0f, ShoulderHeight + 0.02f, 0f), 0.21f, 0.2f, 0.62f, round, (int)PersonPart.Torso, (int)PersonSlot.Clothes);
        b.Ball(new Vector3(0f, ShoulderHeight + 0.01f, 0f), new Vector3(0.205f, 0.075f, 0.13f), (int)PersonPart.Torso, (int)PersonSlot.Clothes);
        b.Tube(new Vector3(0f, HipHeight, 0f), new Vector3(0f, HipHeight + 0.05f, 0f), 0.215f, 0.215f, 0.65f, round, (int)PersonPart.Torso, (int)PersonSlot.Leather);
        b.Tube(new Vector3(0f, ShoulderHeight, 0f), new Vector3(0f, 1.53f, 0f), 0.05f, 0.047f, 1f, limb, (int)PersonPart.Head, (int)PersonSlot.Skin);
        b.Ball(new Vector3(0f, 1.625f, 0.012f), new Vector3(0.1f, 0.118f, 0.11f), (int)PersonPart.Head, (int)PersonSlot.Skin);
        b.Ball(new Vector3(0f, 1.66f, -0.016f), new Vector3(0.107f, 0.1f, 0.112f), (int)PersonPart.Hair, (int)PersonSlot.Hair);
        b.Ball(new Vector3(0f, 1.635f, -0.012f), new Vector3(0.118f, 0.128f, 0.122f), (int)PersonPart.Scarf, (int)PersonSlot.Scarf);
        b.Tube(new Vector3(0f, 1.695f, 0f), new Vector3(0f, 1.715f, 0f), 0.28f, 0.28f, 1f, round + 2, (int)PersonPart.StrawHat, (int)PersonSlot.Straw, capped: true);
        b.Tube(new Vector3(0f, 1.7f, 0f), new Vector3(0f, 1.81f, 0f), 0.13f, 0.08f, 1f, round, (int)PersonPart.StrawHat, (int)PersonSlot.Straw, capped: true);
        b.Tube(new Vector3(0f, 0.1f, 0f), new Vector3(0f, HipHeight + 0.04f, 0f), 0.27f, 0.205f, 0.78f, round + 2, (int)PersonPart.Dress, (int)PersonSlot.Clothes, capped: true);

        // Tools in the right hand, pointing as they are held with the arm hanging down.
        var hand = new Vector3(-0.255f, 0.86f, 0.03f);
        b.Tube(hand + new Vector3(0f, 0.2f, -0.12f), new Vector3(-0.255f, 0.03f, 0.62f), 0.018f, 0.018f, 1f, 4, (int)PersonPart.Hoe, (int)PersonSlot.Wood);
        b.Box(new Vector3(-0.255f, 0.035f, 0.66f), new Vector3(0.16f, 0.035f, 0.13f), (int)PersonPart.Hoe, (int)PersonSlot.Leather);
        b.Tube(hand, hand + new Vector3(0f, 0f, 0.32f), 0.016f, 0.016f, 1f, 4, (int)PersonPart.Hammer, (int)PersonSlot.Wood);
        b.Box(hand + new Vector3(0f, 0f, 0.33f), new Vector3(0.06f, 0.15f, 0.07f), (int)PersonPart.Hammer, (int)PersonSlot.Leather);
        b.Tube(new Vector3(0.27f, 0.04f, 0.07f), new Vector3(0.27f, 1.95f, 0.07f), 0.022f, 0.026f, 1f, 5, (int)PersonPart.Staff, (int)PersonSlot.Wood, capped: true);
        var mesh = b.Finish();
        return new PersonMeshData(mesh.Positions, mesh.Normals, mesh.Tags, mesh.Indices);
    }
}
