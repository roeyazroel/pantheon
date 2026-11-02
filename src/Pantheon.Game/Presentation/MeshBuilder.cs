using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation;

/// <summary>Triangles of a shape built in code, each vertex tagged with a part (X) and a colour slot (Y) for its shader.</summary>
/// <param name="Positions">Vertex positions.</param>
/// <param name="Normals">Vertex normals.</param>
/// <param name="Tags">Each vertex's part (X) and slot (Y).</param>
/// <param name="Indices">Triangles, three indices each, wound for Godot (clockwise seen from outside).</param>
public sealed record TaggedMesh(Vector3[] Positions, Vector3[] Normals, Vector2[] Tags, int[] Indices);

/// <summary>
/// Builds low-poly shapes for the figures and buildings made in code (ADR-049, ADR-052): tubes,
/// ellipsoids, boxes, prisms and gabled roofs, each vertex tagged for its shader, every triangle
/// wound to face outwards. Pure.
/// </summary>
public sealed class MeshBuilder(bool detailed)
{
    private readonly List<Vector3> _positions = [];
    private readonly List<Vector3> _normals = [];
    private readonly List<Vector2> _tags = [];
    private readonly List<int> _indices = [];

    /// <summary>A tapering tube from one point to another, elliptical by <paramref name="depth"/>, smooth-sided.</summary>
    /// <param name="from">One end's centre.</param>
    /// <param name="to">The other end's centre.</param>
    /// <param name="fromRadius">Radius at <paramref name="from"/>.</param>
    /// <param name="toRadius">Radius at <paramref name="to"/>; 0 makes a cone.</param>
    /// <param name="depth">The cross-section's depth as a share of its width.</param>
    /// <param name="sides">Sides around.</param>
    /// <param name="part">The part, for the shader.</param>
    /// <param name="slot">The colour slot, for the shader.</param>
    /// <param name="capped">Whether to close the ends.</param>
    public void Tube(Vector3 from, Vector3 to, float fromRadius, float toRadius, float depth, int sides, int part, int slot, bool capped = false)
    {
        var axis = Vector3.Normalize(to - from);
        var across = MathF.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, axis));
        var deep = Vector3.Normalize(Vector3.Cross(axis, across));
        var start = _positions.Count;
        for (var i = 0; i <= sides; i++)
        {
            var angle = MathF.Tau * i / sides;
            var (c, s) = (MathF.Cos(angle), MathF.Sin(angle) * depth);
            var radial = (across * c) + (deep * s / (depth * depth));
            var slope = (fromRadius - toRadius) / Vector3.Distance(from, to);
            var normal = Vector3.Normalize(Vector3.Normalize(radial) + (axis * slope));
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
            if (toRadius > 0f)
            {
                Cap(to, axis, across, deep, depth, toRadius, sides, part, slot);
            }
        }
    }

    /// <summary>An ellipsoid with the given radii.</summary>
    /// <param name="centre">Its centre.</param>
    /// <param name="radii">Its radii.</param>
    /// <param name="part">The part.</param>
    /// <param name="slot">The colour slot.</param>
    /// <param name="lowerHalf">Whether to include the lower half; a dome leaves it out.</param>
    public void Ball(Vector3 centre, Vector3 radii, int part, int slot, bool lowerHalf = true)
    {
        var (around, up) = detailed ? (8, 6) : (5, 3);
        var rows = lowerHalf ? up : (up + 1) / 2;
        var start = _positions.Count;
        for (var j = 0; j <= rows; j++)
        {
            var polar = MathF.PI * j / up;
            for (var i = 0; i <= around; i++)
            {
                var azimuth = MathF.Tau * i / around;
                var unit = new Vector3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
                Add(centre + (unit * radii), Vector3.Normalize(unit / radii), part, slot);
            }
        }

        for (var j = 0; j < rows; j++)
        {
            for (var i = 0; i < around; i++)
            {
                var k = start + (j * (around + 1)) + i;
                Quad(k, k + around + 1, k + around + 2, k + 1);
            }
        }
    }

    /// <summary>A box with flat faces.</summary>
    /// <param name="centre">Its centre.</param>
    /// <param name="size">Its size.</param>
    /// <param name="part">The part.</param>
    /// <param name="slot">The colour slot.</param>
    public void Box(Vector3 centre, Vector3 size, int part, int slot)
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

    /// <summary>A straight beam between two points, level across its width: a plank, a rail or a rafter at any slope.</summary>
    /// <param name="from">The middle of one end.</param>
    /// <param name="to">The middle of the other; it must not lie straight above <paramref name="from"/>.</param>
    /// <param name="width">Across the beam, level.</param>
    /// <param name="height">Up through the beam.</param>
    /// <param name="part">The part.</param>
    /// <param name="slot">The colour slot.</param>
    public void Beam(Vector3 from, Vector3 to, float width, float height, int part, int slot)
    {
        var along = Vector3.Normalize(to - from);
        var side = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, along));
        var up = Vector3.Cross(along, side);
        var (w, h) = (side * (width / 2f), up * (height / 2f));
        foreach (var (normal, offset, across) in new[] { (up, h, w), (-up, -h, w), (side, w, h), (-side, -w, h) })
        {
            var start = _positions.Count;
            foreach (var (end, a) in new[] { (from, -1f), (to, -1f), (to, 1f), (from, 1f) })
            {
                Add(end + offset + (across * a), normal, part, slot);
            }

            Quad(start, start + 1, start + 2, start + 3);
        }

        foreach (var (end, normal) in new[] { (from, -along), (to, along) })
        {
            var start = _positions.Count;
            foreach (var (a, c) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                Add(end + (w * a) + (h * c), normal, part, slot);
            }

            Quad(start, start + 1, start + 2, start + 3);
        }
    }

    /// <summary>
    /// A gabled roof: two slopes meeting in a ridge along X, from the eaves at the centre's height
    /// to the ridge, with gable ends of <paramref name="gableSlot"/>.
    /// </summary>
    /// <param name="centre">The centre of the eaves' rectangle.</param>
    /// <param name="width">Along the ridge.</param>
    /// <param name="depth">Across the ridge.</param>
    /// <param name="rise">From the eaves to the ridge.</param>
    /// <param name="part">The part.</param>
    /// <param name="slot">The slopes' colour slot.</param>
    /// <param name="gableSlot">The gable ends' colour slot.</param>
    /// <param name="thickness">The slopes' thickness, so the roof reads from below.</param>
    /// <param name="walls">The walls' width and depth under the roof, where the gable ends stand; the roof's own when <c>null</c>.</param>
    public void Gable(Vector3 centre, float width, float depth, float rise, int part, int slot, int gableSlot, float thickness = 0.12f, Vector2? walls = null)
    {
        var (hw, hd) = (width / 2f, depth / 2f);
        var ridge = centre + new Vector3(0f, rise, 0f);
        foreach (var side in new[] { 1f, -1f })
        {
            var eave = centre + new Vector3(0f, 0f, side * hd);
            var normal = Vector3.Normalize(new Vector3(0f, hd, side * rise));
            var lift = normal * thickness;
            Roof(eave + new Vector3(-hw, 0f, 0f), eave + new Vector3(hw, 0f, 0f), ridge + new Vector3(hw, 0f, 0f), ridge + new Vector3(-hw, 0f, 0f), slot, thickness);
        }

        var (gx, gz) = walls is { } under ? (under.X / 2f, under.Y / 2f) : (hw - 0.05f, hd);
        foreach (var end in new[] { 1f, -1f })
        {
            var x = end * gx;
            var start = _positions.Count;
            var normal = new Vector3(end, 0f, 0f);
            Add(new Vector3(x, centre.Y, centre.Z - gz) + new Vector3(centre.X, 0f, 0f), normal, part, gableSlot);
            Add(new Vector3(x, centre.Y, centre.Z + gz) + new Vector3(centre.X, 0f, 0f), normal, part, gableSlot);
            Add(new Vector3(x, ridge.Y, centre.Z) + new Vector3(centre.X, 0f, 0f), normal, part, gableSlot);
            _indices.AddRange([start, start + 1, start + 2]);
        }
    }

    /// <summary>
    /// A hipped roof: four slopes up from the eaves' rectangle to a ridge along its longer side, so
    /// that it has no gable ends.
    /// </summary>
    /// <param name="centre">The centre of the eaves' rectangle.</param>
    /// <param name="width">Along X.</param>
    /// <param name="depth">Along Z.</param>
    /// <param name="rise">From the eaves to the ridge.</param>
    /// <param name="ridge">The ridge's length; 0 brings the slopes to a point.</param>
    /// <param name="slot">The slopes' colour slot.</param>
    public void Hip(Vector3 centre, float width, float depth, float rise, float ridge, int slot)
    {
        var (hw, hd) = (width / 2f, depth / 2f);
        var along = width >= depth ? new Vector3(ridge / 2f, 0f, 0f) : new Vector3(0f, 0f, ridge / 2f);
        var top = centre + new Vector3(0f, rise, 0f);
        var (r0, r1) = (top - along, top + along);
        Vector3 Eave(float x, float z) => centre + new Vector3(x * hw, 0f, z * hd);
        var (nw, ne, se, sw) = (Eave(-1f, -1f), Eave(1f, -1f), Eave(1f, 1f), Eave(-1f, 1f));
        if (width >= depth)
        {
            Roof(sw, se, r1, r0, slot);
            Roof(ne, nw, r0, r1, slot);
            Roof(se, ne, r1, r1, slot);
            Roof(nw, sw, r0, r0, slot);
        }
        else
        {
            Roof(ne, se, r1, r0, slot);
            Roof(sw, nw, r0, r1, slot);
            Roof(se, sw, r1, r1, slot);
            Roof(nw, ne, r0, r0, slot);
        }
    }

    /// <summary>
    /// One slope of a roof, up from its eave (<paramref name="a"/> to <paramref name="b"/>) to its top
    /// (<paramref name="c"/> to <paramref name="d"/>): a thick slab with a closed eave. Each vertex's
    /// part says how far up the slope it lies, 0 at the eave and 1 at the top, for the building shader.
    /// </summary>
    /// <param name="a">The eave's one end.</param>
    /// <param name="b">The eave's other end.</param>
    /// <param name="c">The top above <paramref name="b"/>.</param>
    /// <param name="d">The top above <paramref name="a"/>.</param>
    /// <param name="slot">The colour slot.</param>
    /// <param name="thickness">The slab's thickness.</param>
    public void Roof(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int slot, float thickness = 0.12f)
    {
        var normal = Vector3.Normalize(Vector3.Cross(b - a, Vector3.Lerp(c, d, 0.5f) - a));
        normal = normal.Y < 0f ? -normal : normal;
        var lift = normal * thickness;
        var start = _positions.Count;
        foreach (var (corner, up) in new[] { (a, 0), (b, 0), (c, 1), (d, 1) })
        {
            Add(corner + lift, normal, up, slot);
        }

        Quad(start, start + 1, start + 2, start + 3);
        start = _positions.Count;
        foreach (var corner in new[] { a, b, c, d })
        {
            Add(corner, -normal, 0, slot);
        }

        Quad(start, start + 1, start + 2, start + 3);
        var eave = Vector3.Normalize(Vector3.Cross(b - a, normal));
        eave = Vector3.Dot(eave, a - Vector3.Lerp(c, d, 0.5f)) < 0f ? -eave : eave;
        start = _positions.Count;
        foreach (var corner in new[] { a, b, b + lift, a + lift })
        {
            Add(corner, eave, 0, slot);
        }

        Quad(start, start + 1, start + 2, start + 3);
    }

    /// <summary>The finished shape, its triangles facing outwards.</summary>
    /// <returns>The triangles.</returns>
    public TaggedMesh Finish()
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

        return new TaggedMesh([.. _positions], [.. _normals], [.. _tags], indices);
    }

    /// <summary>A flat disc closing a tube's end.</summary>
    private void Cap(Vector3 centre, Vector3 normal, Vector3 across, Vector3 deep, float depth, float radius, int sides, int part, int slot)
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

    private void Add(Vector3 position, Vector3 normal, int part, int slot)
    {
        _positions.Add(position);
        _normals.Add(normal);
        _tags.Add(new Vector2(part, slot));
    }
}
