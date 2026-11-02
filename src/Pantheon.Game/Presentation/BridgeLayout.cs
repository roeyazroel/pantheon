using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Roads;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>
/// A bridge over water (ADR-054, ADR-077): its ends where the road meets either bank, and how far its
/// deck arches above the straight line between them, so that it meets the road at both ends whatever
/// their heights.
/// </summary>
/// <param name="From">The road on the near bank.</param>
/// <param name="To">The road on the far bank.</param>
/// <param name="Arch">How far the middle of the deck rises above the line between its ends, in metres.</param>
public readonly record struct BridgeSpan(Vector3 From, Vector3 To, float Arch)
{
    /// <summary>Half the width of the deck, in metres.</summary>
    public const float HalfWidth = (RoadLayout.Width / 2f) + 0.2f;

    /// <summary>The span's length over the ground, in metres.</summary>
    public float Length => new Vector2(To.X - From.X, To.Z - From.Z).Length();

    /// <summary>The height of the deck's top along the span.</summary>
    /// <param name="t">0 at <see cref="From"/>, 1 at <see cref="To"/>.</param>
    /// <returns>The height, in metres.</returns>
    public float DeckAt(float t) => From.Y + ((To.Y - From.Y) * t) + (Arch * 4f * t * (1f - t));

    /// <summary>The ground point under the span's middle line.</summary>
    /// <param name="t">0 at <see cref="From"/>, 1 at <see cref="To"/>.</param>
    /// <returns>The (X, Z) point.</returns>
    public Vector2 PointAt(float t) => new(From.X + ((To.X - From.X) * t), From.Z + ((To.Z - From.Z) * t));

    /// <summary>The height of the deck at a place, if the deck is there: walkers on it stand on it, not on the river's bed.</summary>
    /// <param name="point">A ground point.</param>
    /// <returns>The deck's height, or <c>null</c> off the bridge.</returns>
    public float? DeckOver(Vector2 point)
    {
        var (start, ahead) = (PointAt(0f), PointAt(1f) - PointAt(0f));
        var t = Vector2.Dot(point - start, ahead) / ahead.LengthSquared();
        return t is >= 0f and <= 1f && Vector2.Distance(point, start + (ahead * t)) <= HalfWidth ? DeckAt(t) : null;
    }

    /// <summary>Whether two spans carry different roads over the same crossing, so that one bridge serves both.</summary>
    /// <param name="other">The other span.</param>
    /// <returns><c>true</c> when their middles nearly meet.</returns>
    public bool SameCrossing(BridgeSpan other) => Vector2.Distance(PointAt(0.5f), other.PointAt(0.5f)) < 8f;
}

/// <summary>Where roads cross water (ADR-054, ADR-077): the spans a bridge carries them over, bank to bank. Pure.</summary>
public static class BridgeLayout
{
    // How far the deck stands above the water at the least, and how it arches for its length.
    private const float WaterClearance = 0.6f;
    private const float RestingArch = 0.35f;
    private const float ArchPerMetre = 0.025f;

    // A deck never climbs steeper than this share of its length, however the land lies under it.
    private const float SteepestArch = 0.3f;

    // Road sections a bank is walked up at most, the climb per section below which it has topped out,
    // and the highest a bank counts, in metres above the water's edge.
    private const int BankSections = 8;
    private const float FlatBank = 0.05f;
    private const float HighestBank = 6f;

    // Points along the span where the water and the land are looked at.
    private const int Soundings = 16;

    // The deck runs this far into either bank, so that no gap shows on a side-sloping bank.
    private const float BankOverlap = 1f;

    // Metres between deck segments, posts and piles; how far piles reach into the bed.
    private const float SegmentMetres = 1.5f;
    private const float PostMetres = 2f;
    private const float PileMetres = 4f;
    private const float PileFooting = 0.4f;
    private const float RailHeight = 0.95f;

    /// <summary>Whether a road's point lies over a river, a lake or the sea's edge, where a bridge carries it.</summary>
    /// <param name="terrain">Terrain.</param>
    /// <param name="point">A point of the road.</param>
    /// <returns><c>true</c> when it does.</returns>
    public static bool IsWet(TerrainField terrain, Vector3 point)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        return terrain.IsUnderFreshWater(point.X, point.Z) || terrain.SampleHeight(point.X, point.Z) < 0.2f;
    }

    /// <summary>The spans of a road's built part over fresh water or the sea.</summary>
    /// <param name="road">The road.</param>
    /// <param name="terrain">Terrain, for the ground and the water.</param>
    /// <returns>Each span, from the road on one bank to the road on the other.</returns>
    public static List<BridgeSpan> Spans(RoadState road, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(terrain);
        var centre = RoadLayout.Ribbon(road, terrain).Select(section => section.Centre).ToList();
        var spans = new List<BridgeSpan>();
        for (var i = 1; i < centre.Count; i++)
        {
            if (!IsWet(terrain, centre[i]) || IsWet(terrain, centre[i - 1]))
            {
                continue;
            }

            var end = i;
            while (end < centre.Count && IsWet(terrain, centre[end]))
            {
                end++;
            }

            if (end >= centre.Count)
            {
                break;
            }

            spans.Add(Between(centre[BankTop(centre, i - 1, -1, terrain)], centre[BankTop(centre, end, 1, terrain)], terrain));
            i = end;
        }

        return spans;
    }

    /// <summary>
    /// Where a bank tops out, walking along the road from the water's edge: a bridge reaches from top
    /// to top, so the road does not dive into a gully to meet it. It gives up on a bank that climbs
    /// into a hill.
    /// </summary>
    private static int BankTop(List<Vector3> centre, int edge, int direction, TerrainField terrain)
    {
        var top = edge;
        for (var i = 1; i <= BankSections; i++)
        {
            var at = edge + (i * direction);
            if (at < 0 || at >= centre.Count || IsWet(terrain, centre[at])
                || centre[at].Y < centre[top].Y + FlatBank || centre[at].Y > centre[edge].Y + HighestBank)
            {
                break;
            }

            top = at;
        }

        return top;
    }

    /// <summary>A span between two banks, arched just enough to clear what lies under it.</summary>
    private static BridgeSpan Between(Vector3 from, Vector3 to, TerrainField terrain)
    {
        var flat = new BridgeSpan(from, to, 0f);
        var arch = RestingArch + (flat.Length * ArchPerMetre);
        for (var i = 1; i < Soundings; i++)
        {
            var t = i / (float)Soundings;
            var point = flat.PointAt(t);
            arch = MathF.Max(arch, (Surface(terrain, point) + WaterClearance - flat.DeckAt(t)) / (4f * t * (1f - t)));
        }

        return flat with { Arch = MathF.Min(arch, flat.Length * SteepestArch) };
    }

    /// <summary>The height of the land or the water at a place, whichever is higher.</summary>
    private static float Surface(TerrainField terrain, Vector2 point)
    {
        var last = terrain.Resolution - 1;
        var x = Math.Clamp((int)MathF.Round(point.X / terrain.Spacing), 0, last);
        var z = Math.Clamp((int)MathF.Round(point.Y / terrain.Spacing), 0, last);
        var level = terrain.Hydrology.WaterSurface[(z * terrain.Resolution) + x];
        var land = MathF.Max(terrain.SampleHeight(point.X, point.Y), 0f);
        return float.IsNaN(level) ? land : MathF.Max(land, level);
    }

    /// <summary>
    /// A span's bridge along +Z, centred between its ends at their mean height (ADR-054, ADR-077): a
    /// planked deck on two beams following the arch, a rail and a lower rail on posts either side, and
    /// piles from the deck down into the bed.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="terrain">Terrain, for how far the piles reach.</param>
    /// <returns>The triangles, tagged as timber for the building shader.</returns>
    public static TaggedMesh Mesh(BridgeSpan span, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var b = new MeshBuilder(detailed: true);
        var (trim, wall) = ((int)BuildingSlot.Trim, (int)BuildingSlot.Wall);
        var length = MathF.Max(1f, span.Length);
        var middle = (span.From.Y + span.To.Y) / 2f;
        var overlap = BankOverlap / length;
        Vector3 Deck(float t, float side, float lift) => new(side, span.DeckAt(t) - middle + lift, (t - 0.5f) * length);

        var segments = Math.Max(3, (int)MathF.Ceiling(length / SegmentMetres));
        var edge = BridgeSpan.HalfWidth - 0.05f;
        for (var i = 0; i < segments; i++)
        {
            var (t0, t1) = (-overlap + ((1f + (2f * overlap)) * i / segments), -overlap + ((1f + (2f * overlap)) * (i + 1) / segments));
            b.Beam(Deck(t0, 0f, -0.1f), Deck(t1, 0f, -0.1f), BridgeSpan.HalfWidth * 2f, 0.2f, 0, wall);
            foreach (var side in new[] { 1f, -1f })
            {
                b.Beam(Deck(t0, side * (edge - 0.3f), -0.32f), Deck(t1, side * (edge - 0.3f), -0.32f), 0.22f, 0.26f, 0, trim);
                b.Beam(Deck(t0, side * edge, RailHeight), Deck(t1, side * edge, RailHeight), 0.12f, 0.1f, 0, trim);
                b.Beam(Deck(t0, side * edge, RailHeight * 0.5f), Deck(t1, side * edge, RailHeight * 0.5f), 0.07f, 0.07f, 0, trim);
            }
        }

        for (var along = 0f; along <= length + 0.01f; along += PostMetres)
        {
            foreach (var side in new[] { 1f, -1f })
            {
                b.Box(Deck(along / length, side * edge, RailHeight / 2f), new Vector3(0.13f, RailHeight, 0.13f), 0, trim);
            }
        }

        for (var along = PileMetres / 2f; along < length; along += PileMetres)
        {
            var t = along / length;
            var point = span.PointAt(t);
            var bed = terrain.SampleHeight(point.X, point.Y) - PileFooting - middle;
            var top = span.DeckAt(t) - middle - 0.2f;
            if (top - bed <= 0.3f)
            {
                continue;
            }

            foreach (var side in new[] { 1f, -1f })
            {
                b.Box(new Vector3(side * (edge - 0.3f), (top + bed) / 2f, (t - 0.5f) * length), new Vector3(0.22f, top - bed, 0.22f), 0, trim);
            }
        }

        return b.Finish();
    }
}
