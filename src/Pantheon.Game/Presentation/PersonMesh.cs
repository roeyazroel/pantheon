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
        var b = new Builder(detailed ? 1 : 0);
        var (round, limb) = (detailed ? 8 : 5, detailed ? 6 : 4);
        foreach (var (side, leg, arm) in new[] { (1f, PersonPart.LeftLeg, PersonPart.LeftArm), (-1f, PersonPart.RightLeg, PersonPart.RightArm) })
        {
            b.Tube(new Vector3(side * 0.095f, 0.07f, 0f), new Vector3(side * 0.1f, HipHeight + 0.02f, 0f), 0.06f, 0.088f, 1f, limb, leg, PersonSlot.Trousers);
            b.Box(new Vector3(side * 0.095f, 0.045f, 0.035f), new Vector3(0.1f, 0.09f, 0.23f), leg, PersonSlot.Leather);
            b.Tube(new Vector3(side * 0.24f, ShoulderHeight + 0.01f, 0f), new Vector3(side * 0.255f, 0.91f, 0.01f), 0.062f, 0.05f, 1f, limb, arm, PersonSlot.Clothes);
            b.Ball(new Vector3(side * 0.255f, 0.86f, 0.015f), new Vector3(0.045f, 0.055f, 0.045f), arm, PersonSlot.Skin);
        }

        b.Tube(new Vector3(0f, 0.72f, 0f), new Vector3(0f, ShoulderHeight + 0.02f, 0f), 0.21f, 0.2f, 0.62f, round, PersonPart.Torso, PersonSlot.Clothes);
        b.Ball(new Vector3(0f, ShoulderHeight + 0.01f, 0f), new Vector3(0.205f, 0.075f, 0.13f), PersonPart.Torso, PersonSlot.Clothes);
        b.Tube(new Vector3(0f, HipHeight, 0f), new Vector3(0f, HipHeight + 0.05f, 0f), 0.215f, 0.215f, 0.65f, round, PersonPart.Torso, PersonSlot.Leather);
        b.Tube(new Vector3(0f, ShoulderHeight, 0f), new Vector3(0f, 1.53f, 0f), 0.05f, 0.047f, 1f, limb, PersonPart.Head, PersonSlot.Skin);
        b.Ball(new Vector3(0f, 1.625f, 0.012f), new Vector3(0.1f, 0.118f, 0.11f), PersonPart.Head, PersonSlot.Skin);
        b.Ball(new Vector3(0f, 1.66f, -0.016f), new Vector3(0.107f, 0.1f, 0.112f), PersonPart.Hair, PersonSlot.Hair);
        b.Ball(new Vector3(0f, 1.635f, -0.012f), new Vector3(0.118f, 0.128f, 0.122f), PersonPart.Scarf, PersonSlot.Scarf);
        b.Tube(new Vector3(0f, 1.695f, 0f), new Vector3(0f, 1.715f, 0f), 0.28f, 0.28f, 1f, round + 2, PersonPart.StrawHat, PersonSlot.Straw, capped: true);
        b.Tube(new Vector3(0f, 1.7f, 0f), new Vector3(0f, 1.81f, 0f), 0.13f, 0.08f, 1f, round, PersonPart.StrawHat, PersonSlot.Straw, capped: true);
        b.Tube(new Vector3(0f, 0.1f, 0f), new Vector3(0f, HipHeight + 0.04f, 0f), 0.27f, 0.205f, 0.78f, round + 2, PersonPart.Dress, PersonSlot.Clothes, capped: true);

        // Tools in the right hand, pointing as they are held with the arm hanging down.
        var hand = new Vector3(-0.255f, 0.86f, 0.03f);
        b.Tube(hand + new Vector3(0f, 0.2f, -0.12f), new Vector3(-0.255f, 0.03f, 0.62f), 0.018f, 0.018f, 1f, 4, PersonPart.Hoe, PersonSlot.Wood);
        b.Box(new Vector3(-0.255f, 0.035f, 0.66f), new Vector3(0.16f, 0.035f, 0.13f), PersonPart.Hoe, PersonSlot.Leather);
        b.Tube(hand, hand + new Vector3(0f, 0f, 0.32f), 0.016f, 0.016f, 1f, 4, PersonPart.Hammer, PersonSlot.Wood);
        b.Box(hand + new Vector3(0f, 0f, 0.33f), new Vector3(0.06f, 0.15f, 0.07f), PersonPart.Hammer, PersonSlot.Leather);
        b.Tube(new Vector3(0.27f, 0.04f, 0.07f), new Vector3(0.27f, 1.95f, 0.07f), 0.022f, 0.026f, 1f, 5, PersonPart.Staff, PersonSlot.Wood, capped: true);
        return b.Finish();
    }

    /// <summary>Accumulates the figure's shapes.</summary>
    private sealed class Builder(int detail)
    {
        private readonly List<Vector3> _positions = [];
        private readonly List<Vector3> _normals = [];
        private readonly List<Vector2> _tags = [];
        private readonly List<int> _indices = [];

        /// <summary>A tapering tube from one point to another, elliptical by <paramref name="depth"/>, smooth-sided.</summary>
        public void Tube(Vector3 from, Vector3 to, float fromRadius, float toRadius, float depth, int sides, PersonPart part, PersonSlot slot, bool capped = false)
        {
            var axis = Vector3.Normalize(to - from);
            var across = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, axis));
            var deep = Vector3.Normalize(Vector3.Cross(axis, across));
            var start = _positions.Count;
            for (var i = 0; i <= sides; i++)
            {
                var angle = MathF.Tau * i / sides;
                var (c, s) = (MathF.Cos(angle), MathF.Sin(angle) * depth);
                var normal = Vector3.Normalize((across * c) + (deep * s / (depth * depth)));
                Add(from + (((across * c) + (deep * s)) * fromRadius), normal, part, slot);
                Add(to + (((across * c) + (deep * s)) * toRadius), normal, part, slot);
            }

            for (var i = 0; i < sides; i++)
            {
                var k = start + (2 * i);
                Quad(k, k + 2, k + 3, k + 1);
            }

            if (capped)
            {
                Cap(from, -axis, across, deep, depth, fromRadius, sides, part, slot);
                Cap(to, axis, across, deep, depth, toRadius, sides, part, slot);
            }
        }

        /// <summary>An ellipsoid with the given radii.</summary>
        public void Ball(Vector3 centre, Vector3 radii, PersonPart part, PersonSlot slot)
        {
            var (around, up) = detail > 0 ? (8, 6) : (5, 3);
            var start = _positions.Count;
            for (var j = 0; j <= up; j++)
            {
                var polar = MathF.PI * j / up;
                for (var i = 0; i <= around; i++)
                {
                    var azimuth = MathF.Tau * i / around;
                    var unit = new Vector3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
                    Add(centre + (unit * radii), Vector3.Normalize(unit / radii), part, slot);
                }
            }

            for (var j = 0; j < up; j++)
            {
                for (var i = 0; i < around; i++)
                {
                    var k = start + (j * (around + 1)) + i;
                    Quad(k, k + around + 1, k + around + 2, k + 1);
                }
            }
        }

        /// <summary>A box with flat faces.</summary>
        public void Box(Vector3 centre, Vector3 size, PersonPart part, PersonSlot slot)
        {
            var half = size / 2f;
            foreach (var normal in new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ })
            {
                var u = MathF.Abs(normal.Y) > 0.5f ? Vector3.UnitX : Vector3.UnitY;
                var v = Vector3.Cross(normal, u);
                var start = _positions.Count;
                foreach (var (a, c) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    Add(centre + ((normal + (u * a) + (v * c)) * half), normal, part, slot);
                }

                Quad(start, start + 1, start + 2, start + 3);
            }
        }

        /// <summary>The finished figure, its triangles facing outwards.</summary>
        public PersonMeshData Finish()
        {
            var indices = _indices.ToArray();
            for (var t = 0; t < indices.Length; t += 3)
            {
                // Wind every triangle so that its face normal agrees with its vertices' normals.
                var (a, b, c) = (_positions[indices[t]], _positions[indices[t + 1]], _positions[indices[t + 2]]);
                var face = Vector3.Cross(b - a, c - a);
                var outward = _normals[indices[t]] + _normals[indices[t + 1]] + _normals[indices[t + 2]];
                if (Vector3.Dot(face, outward) > 0f)
                {
                    (indices[t + 1], indices[t + 2]) = (indices[t + 2], indices[t + 1]);
                }
            }

            return new PersonMeshData([.. _positions], [.. _normals], [.. _tags], indices);
        }

        /// <summary>A flat disc closing a tube's end.</summary>
        private void Cap(Vector3 centre, Vector3 normal, Vector3 across, Vector3 deep, float depth, float radius, int sides, PersonPart part, PersonSlot slot)
        {
            var hub = _positions.Count;
            Add(centre, normal, part, slot);
            for (var i = 0; i <= sides; i++)
            {
                var angle = MathF.Tau * i / sides;
                Add(centre + (((across * MathF.Cos(angle)) + (deep * MathF.Sin(angle) * depth)) * radius), normal, part, slot);
            }

            for (var i = 0; i < sides; i++)
            {
                _indices.AddRange([hub, hub + 1 + i, hub + 2 + i]);
            }
        }

        private void Quad(int a, int b, int c, int d) => _indices.AddRange([a, b, c, a, c, d]);

        private void Add(Vector3 position, Vector3 normal, PersonPart part, PersonSlot slot)
        {
            _positions.Add(position);
            _normals.Add(normal);
            _tags.Add(new Vector2((int)part, (int)slot));
        }
    }
}
