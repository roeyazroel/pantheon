using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Presentation.Nature;

/// <summary>How finely a plant is grown: near the camera, or far from it.</summary>
public enum PlantDetail
{
    /// <summary>Every branch and cluster: for trees near the camera.</summary>
    Near,

    /// <summary>Fewer, larger cards and no twigs: for trees in the distance.</summary>
    Far,
}

/// <summary>
/// Grows the trees (spec §61, §93, ADR-073), each species in <see cref="Variants"/> variants: a trunk
/// flaring at its roots and leaning a little, branches curving up and out, twigs, and crowns of leaf
/// cards at their ends and about the crown's shell, lit as one rounded mass, darker within. Conifers
/// grow whorls of needle sprays narrowing to their tips; palms a ringed, curving trunk under arching
/// fronds; willows hang switches from their dome; a snag stands dead. Pure: the same kind, variant
/// and detail grow the same tree.
/// </summary>
public static class TreeGrowth
{
    /// <summary>How many variants of each species there are.</summary>
    public const int Variants = 3;

    private static readonly Vector3 Up = Vector3.UnitY;

    /// <summary>Grows a tree, standing on the ground at the origin.</summary>
    /// <param name="kind">Its species.</param>
    /// <param name="variant">Which of its variants, from 0.</param>
    /// <param name="detail">How finely.</param>
    /// <returns>Its mesh.</returns>
    public static PlantMesh Grow(TreeKind kind, int variant, PlantDetail detail)
    {
        var random = PlantRandom.For((ulong)kind + 1, (ulong)Math.Clamp(variant, 0, Variants - 1) + 1);
        var b = new PlantBuilder();
        var near = detail == PlantDetail.Near;
        switch (kind)
        {
            case TreeKind.Oak:
                Broadleaf(b, ref random, near, new Crown(2.6f, 0.36f, 6, (0.45f, 0.85f), (2.3f, 3.1f), 5.0f, (1.8f, 2.5f), LeafCell.Broad, new Vector3(0f, 4.7f, 0f), new Vector3(2.7f, 1.9f, 2.7f), 14));
                break;
            case TreeKind.Birch:
                Broadleaf(b, ref random, near, new Crown(3.0f, 0.15f, 9, (0.9f, 1.2f), (1.3f, 2.0f), 7.8f, (1.2f, 1.6f), LeafCell.Small, new Vector3(0f, 5.6f, 0f), new Vector3(1.7f, 2.3f, 1.7f), 12));
                break;
            case TreeKind.Willow:
                Broadleaf(b, ref random, near, new Crown(2.2f, 0.34f, 5, (0.55f, 0.9f), (2.2f, 2.8f), 0f, (1.5f, 2.0f), LeafCell.Small, new Vector3(0f, 4.4f, 0f), new Vector3(2.8f, 1.5f, 2.8f), 10));
                Switches(b, ref random, near, new Vector3(0f, 4.4f, 0f), new Vector3(2.8f, 1.5f, 2.8f));
                break;
            case TreeKind.Pine:
                Conifer(b, ref random, near, height: 9.6f, radius: 0.27f, crownBase: 3.6f, spacing: 0.62f, perWhorl: 5, reach: 2.3f, droop: 0.05f, rounded: true);
                break;
            case TreeKind.Fir:
                Conifer(b, ref random, near, height: 9.2f, radius: 0.23f, crownBase: 1.1f, spacing: 0.5f, perWhorl: 6, reach: 2.0f, droop: -0.3f, rounded: false);
                break;
            case TreeKind.Palm:
                Palm(b, ref random, near);
                break;
            case TreeKind.Snag:
                Snag(b, ref random, near);
                break;
        }

        return b.Finish();
    }

    /// <summary>A broadleaf crown's shape.</summary>
    /// <param name="CrownBase">Where the trunk forks into branches, in metres.</param>
    /// <param name="Radius">The trunk's radius above its flare.</param>
    /// <param name="Branches">How many main branches.</param>
    /// <param name="Elevation">The branches' rise from the level, in radians.</param>
    /// <param name="Length">The branches' length.</param>
    /// <param name="Leader">How high the trunk itself runs on; 0 when it forks and ends.</param>
    /// <param name="Cluster">A cluster's card size.</param>
    /// <param name="Cell">The leaves.</param>
    /// <param name="Centre">The crown's middle.</param>
    /// <param name="Radii">The crown's half-size.</param>
    /// <param name="Fill">How many clusters fill out the crown's shell.</param>
    private readonly record struct Crown(float CrownBase, float Radius, int Branches, (float From, float To) Elevation, (float From, float To) Length, float Leader, (float From, float To) Cluster, LeafCell Cell, Vector3 Centre, Vector3 Radii, int Fill);

    /// <summary>A broadleaf tree: trunk, branches, twigs and clusters, and its crown filled out.</summary>
    private static void Broadleaf(PlantBuilder b, ref PlantRandom random, bool near, Crown crown)
    {
        var top = crown.Leader > 0f ? crown.Leader : crown.CrownBase + 0.6f;
        var trunk = Trunk(ref random, crown.Radius, top, lean: 0.25f);
        b.Limb(trunk.Spine, trunk.Radii, near ? 8 : 5, 0.65f);
        var tips = new List<Vector3>();
        var count = near ? crown.Branches : Math.Max(3, crown.Branches / 2);
        var azimuth = random.Range(0f, MathF.Tau);
        for (var i = 0; i < count; i++)
        {
            azimuth += 2.39996f + random.Range(-0.3f, 0.3f);
            var height = crown.CrownBase + ((top - crown.CrownBase) * (crown.Leader > 0f ? random.Range(0.05f, 0.75f) : random.Range(0f, 0.3f)));
            var from = Along(trunk.Spine, height);
            var length = random.Range(crown.Length.From, crown.Length.To) * (crown.Leader > 0f ? 1f - (0.4f * (height - crown.CrownBase) / MathF.Max(1f, top - crown.CrownBase)) : 1f);
            var spine = Bough(ref random, from, azimuth, random.Range(crown.Elevation.From, crown.Elevation.To), length);
            b.Limb(spine, Taper(spine.Count, crown.Radius * 0.42f, 0.035f), near ? 5 : 4, 0.7f);
            tips.Add(spine[^1]);
            tips.Add(Vector3.Lerp(spine[1], spine[2], 0.5f));
            if (near)
            {
                for (var t = 0; t < 2; t++)
                {
                    var at = Vector3.Lerp(spine[1], spine[^1], random.Range(0.3f, 0.8f));
                    var twig = Bough(ref random, at, azimuth + random.Range(0.6f, 1.2f) * (t == 0 ? 1f : -1f), random.Range(0.3f, 0.8f), length * random.Range(0.4f, 0.6f));
                    b.Limb(twig, Taper(twig.Count, 0.06f, 0.015f), 3, 0.75f);
                    tips.Add(twig[^1]);
                }
            }
        }

        if (crown.Leader > 0f)
        {
            tips.Add(trunk.Spine[^1] + new Vector3(0f, 0.3f, 0f));
        }

        foreach (var tip in tips)
        {
            Cluster(b, ref random, tip, crown, near ? 3 : 2, near ? 1f : 1.35f);
        }

        for (var i = 0; i < (near ? crown.Fill : crown.Fill / 2); i++)
        {
            // The crown's shell, mostly its upper half, where the light falls.
            var angle = random.Range(0f, MathF.Tau);
            var rise = random.Range(-0.35f, 1f);
            var flat = MathF.Sqrt(MathF.Max(0f, 1f - (rise * rise)));
            var unit = new Vector3(MathF.Cos(angle) * flat, rise, MathF.Sin(angle) * flat) * random.Range(0.7f, 0.95f);
            Cluster(b, ref random, crown.Centre + (unit * crown.Radii), crown, near ? 3 : 2, near ? 1.1f : 1.45f);
        }
    }

    /// <summary>A cluster of leaves: crossed cards about a point, darker the deeper in the crown.</summary>
    private static void Cluster(PlantBuilder b, ref PlantRandom random, Vector3 at, Crown crown, int cards, float scale)
    {
        var size = random.Range(crown.Cluster.From, crown.Cluster.To) * scale;
        var depth = ((at - crown.Centre) / crown.Radii).Length();
        var light = Math.Clamp(0.3f + (0.75f * depth), 0.3f, 1f);
        var yaw = random.Range(0f, MathF.Tau);
        for (var c = 0; c < cards; c++)
        {
            // One card lies nearly flat, the others stand crossed about it, so the cluster is full from above and from the side.
            var turn = yaw + (MathF.PI * c / cards);
            var right = new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn));
            var tilt = c == 0 ? random.Range(1.1f, 1.45f) : random.Range(0.1f, 0.5f);
            var up = Vector3.Normalize((Up * MathF.Cos(tilt)) + (Vector3.Cross(right, Up) * MathF.Sin(tilt)));
            b.Card(at + new Vector3(random.Range(-0.15f, 0.15f), random.Range(-0.1f, 0.15f), random.Range(-0.15f, 0.15f)) * size, right, up, size, size, crown.Cell, crown.Centre, light);
        }
    }

    /// <summary>A willow's hanging switches, from the lower rim of its dome nearly to the ground.</summary>
    private static void Switches(PlantBuilder b, ref PlantRandom random, bool near, Vector3 centre, Vector3 radii)
    {
        var strands = near ? 26 : 12;
        for (var i = 0; i < strands; i++)
        {
            var angle = (MathF.Tau * i / strands) + random.Range(-0.15f, 0.15f);
            var outward = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            var root = centre + new Vector3(outward.X * radii.X * random.Range(0.75f, 0.98f), -radii.Y * random.Range(0.1f, 0.5f), outward.Z * radii.Z * random.Range(0.75f, 0.98f));
            var length = random.Range(2.3f, 3.4f);
            var spine = new List<Vector3>();
            var widths = new List<float>();
            for (var s = 0; s <= 3; s++)
            {
                var t = s / 3f;
                spine.Add(root + (outward * (0.35f * t)) - (Up * (length * t)));
                widths.Add(0.75f * (1f - (0.4f * t)));
            }

            b.Strip(spine, widths, new Vector3(-outward.Z, 0f, outward.X), LeafCell.Willow, centre, 0.85f);
        }
    }

    /// <summary>A conifer: a straight trunk to its tip, and whorls of needle sprays, longest low or about its middle.</summary>
    private static void Conifer(PlantBuilder b, ref PlantRandom random, bool near, float height, float radius, float crownBase, float spacing, int perWhorl, float reach, float droop, bool rounded)
    {
        var trunk = Trunk(ref random, radius, height, lean: 0.12f);
        b.Limb(trunk.Spine, trunk.Radii, near ? 7 : 5, 0.6f);
        var step = near ? spacing : spacing * 1.9f;
        var whorl = 0;
        for (var y = crownBase; y < height - 0.3f; y += step, whorl++)
        {
            var share = (y - crownBase) / (height - crownBase);
            var length = rounded
                ? reach * (0.35f + (0.65f * MathF.Sin(MathF.PI * MathF.Min(1f, (share * 0.85f) + 0.1f))))
                : (reach * MathF.Pow(1f - share, 0.85f)) + 0.35f;
            var at = Along(trunk.Spine, y);
            var n = near ? perWhorl : Math.Max(3, perWhorl - 2);
            for (var i = 0; i < n; i++)
            {
                var azimuth = (MathF.Tau * (i + (0.5f * (whorl % 2))) / n) + random.Range(-0.25f, 0.25f);
                var direction = Vector3.Normalize(new Vector3(MathF.Cos(azimuth), droop + random.Range(-0.12f, 0.12f) + (rounded ? 0.15f * (1f - share) : 0f), MathF.Sin(azimuth)));
                var reachOf = length * random.Range(0.8f, 1.1f);
                var tip = at + (direction * reachOf);
                if (near && reachOf > 1.1f)
                {
                    b.Limb([at, tip], [0.05f, 0.015f], 3, 0.6f);
                }

                var axis = new Vector3(0f, at.Y, 0f);
                var light = Math.Clamp(0.45f + (0.55f * share) + (0.2f * (reachOf / reach)), 0.35f, 1f);
                var middle = at + (direction * (reachOf * 0.55f));
                var side = Vector3.Normalize(Vector3.Cross(direction, Up));
                b.Card(middle, side, direction, reachOf * 0.7f, reachOf * 1.1f, LeafCell.Needles, axis, light, 0.7f);
                if (near)
                {
                    // A second spray tipped across the first gives the branch its depth.
                    var tipped = Vector3.Normalize(Vector3.Cross(direction, side) + (side * 0.6f));
                    b.Card(middle + (Up * 0.1f), tipped, direction, reachOf * 0.55f, reachOf, LeafCell.Needles, axis, light * 0.9f, 0.7f);
                }
            }
        }

        // The leader's tip.
        var crest = trunk.Spine[^1];
        for (var c = 0; c < 2; c++)
        {
            var turn = MathF.PI * c / 2f;
            b.Card(crest + new Vector3(0f, 0.2f, 0f), new Vector3(MathF.Cos(turn), 0f, MathF.Sin(turn)), Up, 0.8f, 1.6f, LeafCell.Needles, crest - new Vector3(0f, 1.5f, 0f), 1f, 0.5f);
        }
    }

    /// <summary>A palm: a ringed trunk curving up to a crown of fronds arching out and down.</summary>
    private static void Palm(PlantBuilder b, ref PlantRandom random, bool near)
    {
        var lean = random.Range(0f, MathF.Tau);
        var bend = new Vector3(MathF.Cos(lean), 0f, MathF.Sin(lean));
        var height = random.Range(6.3f, 7.4f);
        var spine = new List<Vector3>();
        var radii = new List<float>();
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8f;
            spine.Add(new Vector3(0f, (height * t) - 0.3f, 0f) + (bend * (1.4f * t * t)));
            radii.Add((i == 0 ? 0.34f : 0.24f) - (0.07f * t));
        }

        b.Limb(spine, radii, near ? 7 : 5, 0.7f);
        var crown = spine[^1];
        var fronds = near ? 11 : 7;
        for (var f = 0; f < fronds; f++)
        {
            var azimuth = (MathF.Tau * f / fronds) + random.Range(-0.2f, 0.2f);
            var outward = new Vector3(MathF.Cos(azimuth), 0f, MathF.Sin(azimuth));
            var rise = random.Range(0.25f, 0.9f);
            var length = random.Range(3.3f, 4.3f);
            var points = new List<Vector3>();
            var widths = new List<float>();
            var segments = near ? 6 : 4;
            for (var s = 0; s <= segments; s++)
            {
                var t = s / (float)segments;
                // Up and out at first, then over and down under its own weight.
                points.Add(crown + (outward * (length * t)) + (Up * ((length * rise * t) - (length * 0.95f * t * t))));
                widths.Add(1.5f * MathF.Max(0.15f, MathF.Sin(MathF.PI * MathF.Min(1f, (t * 0.9f) + 0.1f))));
            }

            var side = new Vector3(-outward.Z, random.Range(-0.2f, 0.2f), outward.X);
            b.Strip(points, widths, Vector3.Normalize(side), LeafCell.Frond, crown - (Up * 0.8f), 0.9f);
        }
    }

    /// <summary>A dead tree: a leaning trunk broken off, and a few bare, broken branches.</summary>
    private static void Snag(PlantBuilder b, ref PlantRandom random, bool near)
    {
        var trunk = Trunk(ref random, 0.3f, random.Range(4.5f, 5.6f), lean: 0.4f);
        b.Limb(trunk.Spine, trunk.Radii, near ? 7 : 5, 0.7f);
        var azimuth = random.Range(0f, MathF.Tau);
        for (var i = 0; i < (near ? 4 : 2); i++)
        {
            azimuth += 2.39996f;
            var from = Along(trunk.Spine, random.Range(2.2f, 4.4f));
            var spine = Bough(ref random, from, azimuth, random.Range(0.3f, 0.8f), random.Range(0.9f, 1.8f));
            b.Limb(spine, Taper(spine.Count, 0.1f, 0.04f), near ? 4 : 3, 0.75f);
        }
    }

    /// <summary>A trunk from below the ground to a height: flared at its roots, tapering, leaning a little as it rises.</summary>
    private static (List<Vector3> Spine, List<float> Radii) Trunk(ref PlantRandom random, float radius, float top, float lean)
    {
        var spine = new List<Vector3>();
        var radii = new List<float>();
        var offset = Vector3.Zero;
        float[] heights = [-0.3f, 0f, 0.3f, 0.9f];
        var all = new List<float>(heights);
        for (var y = 1.8f; y < top; y += 1.4f)
        {
            all.Add(y);
        }

        all.Add(top);
        foreach (var y in all)
        {
            if (y > 0.9f)
            {
                offset += new Vector3(random.Range(-lean, lean), 0f, random.Range(-lean, lean)) * 0.25f;
            }

            spine.Add(new Vector3(offset.X, y, offset.Z));
            var share = Math.Clamp(y / MathF.Max(1f, top), 0f, 1f);
            // The roots flare out over the first half metre.
            var flare = y < 0.5f ? 1f + (0.7f * (0.5f - MathF.Max(0f, y)) / 0.8f) : 1f;
            radii.Add(MathF.Max(0.03f, radius * flare * (1f - (0.62f * share))));
        }

        return (spine, radii);
    }

    /// <summary>A bough: from a point, rising at an elevation, curving up then out, with a little wander.</summary>
    private static List<Vector3> Bough(ref PlantRandom random, Vector3 from, float azimuth, float elevation, float length)
    {
        var outward = new Vector3(MathF.Cos(azimuth), 0f, MathF.Sin(azimuth));
        var direction = Vector3.Normalize((outward * MathF.Cos(elevation)) + (Up * MathF.Sin(elevation)));
        var points = new List<Vector3> { from };
        var at = from;
        float[] shares = [0.4f, 0.35f, 0.25f];
        foreach (var share in shares)
        {
            var wander = new Vector3(random.Range(-0.2f, 0.2f), random.Range(-0.05f, 0.25f), random.Range(-0.2f, 0.2f));
            direction = Vector3.Normalize(direction + wander + (outward * 0.15f));
            at += direction * (length * share);
            points.Add(at);
        }

        return points;
    }

    /// <summary>Radii tapering evenly from one to another along a limb.</summary>
    private static float[] Taper(int count, float from, float to)
    {
        var radii = new float[count];
        for (var i = 0; i < count; i++)
        {
            radii[i] = from + ((to - from) * i / MathF.Max(1, count - 1));
        }

        return radii;
    }

    /// <summary>The point of a spine at a height.</summary>
    private static Vector3 Along(List<Vector3> spine, float height)
    {
        for (var i = 1; i < spine.Count; i++)
        {
            if (spine[i].Y >= height)
            {
                var t = (height - spine[i - 1].Y) / MathF.Max(1e-4f, spine[i].Y - spine[i - 1].Y);
                return Vector3.Lerp(spine[i - 1], spine[i], Math.Clamp(t, 0f, 1f));
            }
        }

        return spine[^1];
    }
}
