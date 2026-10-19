using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>An animal drawn (ADR-054).</summary>
public enum AnimalKind
{
    /// <summary>A deer of a herd.</summary>
    Deer,

    /// <summary>A bird of a flock.</summary>
    Bird,
}

/// <summary>What an animal is doing, for the animal shader.</summary>
public enum AnimalMotion
{
    /// <summary>Head down, grazing.</summary>
    Grazing,

    /// <summary>Walking on to fresh grazing.</summary>
    Walking,

    /// <summary>Flying.</summary>
    Flying,
}

/// <summary>An animal where it is now.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Position">Where its feet (or its body, flying) are.</param>
/// <param name="Yaw">Its facing; 0 faces +Z.</param>
/// <param name="Motion">What it is doing.</param>
/// <param name="Cycle">How far its motion has gone: strides, bites or wing beats.</param>
/// <param name="Size">Its size, around 1.</param>
/// <param name="Antlers">Whether it is a stag with antlers.</param>
public readonly record struct AnimalPose(AnimalKind Kind, Vector3 Position, float Yaw, AnimalMotion Motion, float Cycle, float Size, bool Antlers);

/// <summary>The wildlife drawn near the camera (spec §61, ADR-054), from <c>settlement_view.json</c>. Presentation only.</summary>
public sealed record WildlifeSettings
{
    /// <summary>How near the camera animals are drawn, in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>The biomes deer graze.</summary>
    public required IReadOnlyList<Biome> DeerBiomes { get; init; }

    /// <summary>The biomes birds fly over.</summary>
    public required IReadOnlyList<Biome> BirdBiomes { get; init; }

    /// <summary>A region's chance of a herd, at full wildlife.</summary>
    public required float HerdChance { get; init; }

    /// <summary>A region's chance of a flock, at full wildlife.</summary>
    public required float FlockChance { get; init; }

    /// <summary>The fewest and most deer in a herd; more where wildlife abounds.</summary>
    public required IReadOnlyList<int> HerdSize { get; init; }

    /// <summary>The fewest and most birds in a flock.</summary>
    public required IReadOnlyList<int> FlockSize { get; init; }

    /// <summary>Seconds a herd grazes in one place.</summary>
    public required float GrazeSeconds { get; init; }

    /// <summary>Seconds it walks to the next.</summary>
    public required float WalkSeconds { get; init; }

    /// <summary>A bird's speed, in metres a second.</summary>
    public required float BirdSpeed { get; init; }

    /// <summary>The most deer and the most birds drawn.</summary>
    public required int MaxAnimals { get; init; }

    /// <summary>Deer coats.</summary>
    public required IReadOnlyList<RgbColor> Coats { get; init; }

    /// <summary>Birds' plumage.</summary>
    public required IReadOnlyList<RgbColor> Plumage { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(Radius > 0f) || !(HerdChance is >= 0f and <= 1f) || !(FlockChance is >= 0f and <= 1f) || HerdSize.Count != 2 || FlockSize.Count != 2
            || HerdSize[0] < 1 || HerdSize[1] < HerdSize[0] || FlockSize[0] < 1 || FlockSize[1] < FlockSize[0]
            || !(GrazeSeconds > 0f) || !(WalkSeconds > 0f) || !(BirdSpeed > 0f) || MaxAnimals < 1 || Coats.Count == 0 || Plumage.Count == 0)
        {
            throw new ArgumentException("Wildlife needs a positive radius, chances in 0..1, sizes as [fewest, most] from 1, positive times, speed and cap, and coat and plumage colours.");
        }
    }
}

/// <summary>
/// Where the wildlife is (spec §61, ADR-054): herds of deer and flocks of birds in the regions near
/// the camera, as many as each region's wildlife (which hunting, fire and cleared forests lower)
/// lets live there. A herd grazes in one place, then walks on to the next; a flock wheels over its
/// ground. Closed-form in the crowds' time (ADR-049), so animals keep a natural pace and freeze
/// when the game is paused, and drawn from <see cref="PresentationHash"/>, so the same herd is in
/// the same place for the same time. Never in water or on a village's bare ground. Pure.
/// </summary>
public static class Wildlife
{
    private const ulong Salt = 54;
    private const float StrideMetres = 1.4f;

    /// <summary>The animals near the camera.</summary>
    /// <param name="world">The world, for its regions' wildlife and biomes.</param>
    /// <param name="terrain">Terrain, for the ground and water.</param>
    /// <param name="camera">The camera's place on the ground.</param>
    /// <param name="seconds">The crowds' time.</param>
    /// <param name="bare">Ground the villages keep bare.</param>
    /// <param name="settings">Wildlife settings.</param>
    /// <param name="into">Receives the animals.</param>
    public static void Pose(WorldState world, TerrainField terrain, Vector2 camera, double seconds, IReadOnlyList<Clearing> bare, WildlifeSettings settings, List<AnimalPose> into)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(into);
        var grid = world.Regions;
        var size = grid.RegionSize;
        var (deer, birds) = (0, 0);
        var reach = (int)MathF.Ceiling(settings.Radius / size);
        var (cx, cz) = ((int)MathF.Floor(camera.X / size), (int)MathF.Floor(camera.Y / size));
        for (var row = Math.Max(0, cz - reach); row <= Math.Min(grid.Rows - 1, cz + reach); row++)
        {
            for (var column = Math.Max(0, cx - reach); column <= Math.Min(grid.Columns - 1, cx + reach); column++)
            {
                var region = grid.Cells[(row * grid.Columns) + column];
                var wildlife = Math.Clamp(region.Wildlife, 0f, 1f);
                float U(ulong k) => PresentationHash.Unit(Salt, world.Seed.Value, (ulong)region.Id, k);
                if (deer < settings.MaxAnimals && settings.DeerBiomes.Contains(region.Biome) && U(0) < settings.HerdChance * wildlife)
                {
                    deer += Herd(world, terrain, region, column, row, seconds, bare, settings, wildlife, camera, Math.Min(settings.MaxAnimals - deer, int.MaxValue), into);
                }

                if (birds < settings.MaxAnimals && settings.BirdBiomes.Contains(region.Biome) && U(1) < settings.FlockChance * wildlife)
                {
                    birds += Flock(world, terrain, region, column, row, seconds, settings, wildlife, camera, settings.MaxAnimals - birds, into);
                }
            }
        }
    }

    /// <summary>The deer nearest a place, for the camera to look at (<c>--wildlife</c>).</summary>
    /// <param name="world">The world.</param>
    /// <param name="terrain">Terrain.</param>
    /// <param name="at">The place.</param>
    /// <param name="seconds">The crowds' time.</param>
    /// <param name="settings">Wildlife settings.</param>
    /// <returns>The deer's feet, or <c>null</c> when none is near.</returns>
    public static Vector3? NearestDeer(WorldState world, TerrainField terrain, Vector2 at, double seconds, WildlifeSettings settings)
    {
        var poses = new List<AnimalPose>();
        Pose(world, terrain, at, seconds, [], settings, poses);
        return poses.Where(p => p.Kind == AnimalKind.Deer).OrderBy(p => Vector2.Distance(new Vector2(p.Position.X, p.Position.Z), at)).Select(p => (Vector3?)p.Position).FirstOrDefault();
    }

    /// <summary>A herd: grazing about one spot, then walking on to the next, each deer about the herd's centre.</summary>
    private static int Herd(WorldState world, TerrainField terrain, RegionState region, int column, int row, double seconds, IReadOnlyList<Clearing> bare, WildlifeSettings settings, float wildlife, Vector2 camera, int room, List<AnimalPose> into)
    {
        float U(ulong a, ulong b = 0) => PresentationHash.Unit(Salt, world.Seed.Value, PresentationHash.Mix((ulong)region.Id, a), b);
        var period = settings.GrazeSeconds + settings.WalkSeconds;
        var shifted = seconds + (U(2) * period * 7.0);
        var visit = (long)Math.Floor(shifted / period);
        var into0 = (float)(shifted - (visit * period));
        var size = world.Regions.RegionSize;
        Vector2? Spot(long k)
        {
            // A few tries for a grazing spot on dry ground the villages leave alone.
            for (var attempt = 0UL; attempt < 4; attempt++)
            {
                var spot = new Vector2((column + 0.15f + (0.7f * U((ulong)k * 8 + attempt, 3))) * size, (row + 0.15f + (0.7f * U((ulong)k * 8 + attempt, 4))) * size);
                if (terrain.SampleHeight(spot.X, spot.Y) > 0.5f && !terrain.IsUnderFreshWater(spot.X, spot.Y) && !ForestLayout.InClearing(bare, spot.X, spot.Y))
                {
                    return spot;
                }
            }

            return null;
        }

        if (Spot(visit) is not { } here || Spot(visit + 1) is not { } next)
        {
            return 0;
        }

        var walking = into0 > settings.GrazeSeconds;
        var share = walking ? (into0 - settings.GrazeSeconds) / settings.WalkSeconds : 0f;
        var centre = Vector2.Lerp(here, next, share);
        if (Vector2.Distance(centre, camera) > settings.Radius)
        {
            return 0;
        }

        var heading = MathF.Atan2(next.X - here.X, next.Y - here.Y);
        var walked = share * Vector2.Distance(here, next);
        var count = Math.Min(room, settings.HerdSize[0] + (int)MathF.Round((settings.HerdSize[1] - settings.HerdSize[0]) * wildlife * U(5)));
        for (var i = 0; i < count; i++)
        {
            var angle = MathF.Tau * U((ulong)i, 6);
            var spread = 1.5f + (4.5f * U((ulong)i, 7));
            var at = centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread);
            var yaw = walking ? heading : MathF.Tau * U((ulong)(i + (visit * 31)), 8);
            var cycle = walking ? walked / StrideMetres : (float)(seconds * 0.4) + U((ulong)i, 9);
            into.Add(new AnimalPose(AnimalKind.Deer, new Vector3(at.X, terrain.SampleHeight(at.X, at.Y), at.Y), yaw, walking ? AnimalMotion.Walking : AnimalMotion.Grazing, cycle, 0.85f + (0.3f * U((ulong)i, 10)), U((ulong)i, 11) < 0.3f));
        }

        return count;
    }

    /// <summary>A flock wheeling round a point over its ground, each bird a little apart.</summary>
    private static int Flock(WorldState world, TerrainField terrain, RegionState region, int column, int row, double seconds, WildlifeSettings settings, float wildlife, Vector2 camera, int room, List<AnimalPose> into)
    {
        float U(ulong a, ulong b = 0) => PresentationHash.Unit(Salt + 1, world.Seed.Value, PresentationHash.Mix((ulong)region.Id, a), b);
        var size = world.Regions.RegionSize;
        var centre = new Vector2((column + U(1)) * size, (row + U(2)) * size);
        if (Vector2.Distance(centre, camera) > settings.Radius)
        {
            return 0;
        }

        var radius = 18f + (24f * U(3));
        var height = MathF.Max(terrain.SampleHeight(centre.X, centre.Y), 0f) + 22f + (20f * U(4));
        var turn = (U(5) < 0.5f ? 1f : -1f) * settings.BirdSpeed / radius;
        var count = Math.Min(room, settings.FlockSize[0] + (int)MathF.Round((settings.FlockSize[1] - settings.FlockSize[0]) * wildlife * U(6)));
        for (var i = 0; i < count; i++)
        {
            var angle = (float)(seconds * turn) + (i * 0.22f) + (MathF.Tau * U(7));
            var lift = 2.5f * MathF.Sin((float)(seconds * 0.7) + i);
            var wobble = 1f + (0.12f * MathF.Sin((float)(seconds * 0.9) + (i * 1.7f)));
            var at = centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * wobble);
            // Flying round the circle, a bird faces along it.
            var yaw = MathF.Atan2(-MathF.Sin(angle) * MathF.Sign(turn), MathF.Cos(angle) * MathF.Sign(turn));
            into.Add(new AnimalPose(AnimalKind.Bird, new Vector3(at.X, height + lift + (i % 3), at.Y), yaw, AnimalMotion.Flying, (float)(seconds * 2.6) + (i * 0.37f), 0.9f + (0.3f * U((ulong)i, 8)), false));
        }

        return count;
    }
}

/// <summary>Which part of an animal a vertex belongs to, for the animal shader.</summary>
public enum AnimalPart
{
    /// <summary>The body.</summary>
    Body,

    /// <summary>The neck and head, which lower to graze.</summary>
    Head,

    /// <summary>The front legs (left and right in turn by their side).</summary>
    FrontLegs,

    /// <summary>The hind legs.</summary>
    HindLegs,

    /// <summary>A stag's antlers.</summary>
    Antlers,

    /// <summary>The left wing.</summary>
    LeftWing,

    /// <summary>The right wing.</summary>
    RightWing,
}

/// <summary>Animals as meshes built in code (ADR-054): a deer and a bird, their parts tagged for the animal shader. Pure.</summary>
public static class AnimalMesh
{
    /// <summary>The height of a deer's hips and shoulders, about which its legs swing.</summary>
    public const float LegTop = 0.92f;

    /// <summary>An animal.</summary>
    /// <param name="kind">What.</param>
    /// <returns>The triangles: a deer standing at the origin facing +Z, a bird centred there.</returns>
    public static TaggedMesh Build(AnimalKind kind)
    {
        var b = new MeshBuilder(detailed: true);
        const int coat = 0;
        const int light = 1;
        const int dark = 2;
        if (kind == AnimalKind.Bird)
        {
            b.Ball(Vector3.Zero, new Vector3(0.08f, 0.07f, 0.2f), (int)AnimalPart.Body, coat);
            b.Ball(new Vector3(0f, 0.04f, 0.18f), new Vector3(0.05f, 0.05f, 0.06f), (int)AnimalPart.Body, coat);
            b.Tube(new Vector3(0f, 0.04f, 0.23f), new Vector3(0f, 0.03f, 0.3f), 0.02f, 0f, 1f, 4, (int)AnimalPart.Body, dark);
            foreach (var (side, part) in new[] { (1f, AnimalPart.LeftWing), (-1f, AnimalPart.RightWing) })
            {
                b.Tube(new Vector3(side * 0.05f, 0.02f, 0.02f), new Vector3(side * 0.48f, 0.02f, -0.06f), 0.12f, 0.04f, 0.15f, 4, (int)part, coat);
            }

            b.Tube(new Vector3(0f, 0f, -0.16f), new Vector3(0f, 0f, -0.32f), 0.06f, 0.1f, 0.2f, 4, (int)AnimalPart.Body, coat);
            return b.Finish();
        }

        b.Ball(new Vector3(0f, 1.05f, 0f), new Vector3(0.24f, 0.3f, 0.62f), (int)AnimalPart.Body, coat);
        b.Ball(new Vector3(0f, 1.15f, -0.62f), new Vector3(0.08f, 0.1f, 0.06f), (int)AnimalPart.Body, light);
        b.Tube(new Vector3(0f, 1.2f, 0.5f), new Vector3(0f, 1.6f, 0.75f), 0.12f, 0.08f, 1f, 6, (int)AnimalPart.Head, coat);
        b.Ball(new Vector3(0f, 1.68f, 0.88f), new Vector3(0.1f, 0.11f, 0.22f), (int)AnimalPart.Head, coat);
        b.Ball(new Vector3(0f, 1.64f, 1.08f), new Vector3(0.05f, 0.05f, 0.05f), (int)AnimalPart.Head, dark);
        foreach (var side in new[] { 1f, -1f })
        {
            b.Tube(new Vector3(side * 0.06f, 1.78f, 0.8f), new Vector3(side * 0.2f, 1.88f, 0.74f), 0.04f, 0.015f, 0.4f, 4, (int)AnimalPart.Head, coat);
            b.Tube(new Vector3(side * 0.05f, 1.8f, 0.82f), new Vector3(side * 0.28f, 2.2f, 0.7f), 0.025f, 0.015f, 1f, 4, (int)AnimalPart.Antlers, light);
            b.Tube(new Vector3(side * 0.18f, 2f, 0.76f), new Vector3(side * 0.12f, 2.3f, 0.95f), 0.02f, 0.01f, 1f, 4, (int)AnimalPart.Antlers, light);
            foreach (var (z, part) in new[] { (0.42f, AnimalPart.FrontLegs), (-0.42f, AnimalPart.HindLegs) })
            {
                b.Tube(new Vector3(side * 0.13f, LegTop + 0.05f, z), new Vector3(side * 0.13f, 0.06f, z), 0.06f, 0.035f, 1f, 5, (int)part, coat);
                b.Box(new Vector3(side * 0.13f, 0.04f, z + 0.02f), new Vector3(0.06f, 0.08f, 0.09f), (int)part, dark);
            }
        }

        return b.Finish();
    }
}

/// <summary>Where roads cross water (ADR-054): the spans a bridge carries them over, bank to bank. Pure.</summary>
public static class BridgeLayout
{
    /// <summary>The spans of a road's built part over fresh water or the sea.</summary>
    /// <param name="road">The road.</param>
    /// <param name="terrain">Terrain, for the ground and the water.</param>
    /// <returns>Each span's ends on the banks and its deck's height.</returns>
    public static List<(Vector3 From, Vector3 To)> Spans(Domain.Roads.RoadState road, TerrainField terrain)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(terrain);
        var ribbon = RoadLayout.Ribbon(road, terrain);
        var centre = ribbon.Select(edge => (edge.Left + edge.Right) / 2f).ToList();
        bool Wet(Vector3 p) => terrain.IsUnderFreshWater(p.X, p.Z) || terrain.SampleHeight(p.X, p.Z) < 0.2f;
        var spans = new List<(Vector3, Vector3)>();
        for (var i = 1; i < centre.Count; i++)
        {
            if (!Wet(centre[i]) || Wet(centre[i - 1]))
            {
                continue;
            }

            var end = i;
            while (end < centre.Count && Wet(centre[end]))
            {
                end++;
            }

            if (end >= centre.Count)
            {
                break;
            }

            var (start, finish) = (centre[i - 1], centre[end]);
            var deck = MathF.Max(MathF.Max(start.Y, finish.Y), terrain.SampleHeight((start.X + finish.X) / 2f, (start.Z + finish.Z) / 2f)) + 0.6f;
            spans.Add((start with { Y = deck }, finish with { Y = deck }));
            i = end;
        }

        return spans;
    }

    /// <summary>A bridge one metre long along +Z, centred at the origin, its deck's top at 0: plank deck, rails and posts, to be stretched along a span.</summary>
    /// <returns>The triangles, tagged as timber for the building shader.</returns>
    public static TaggedMesh Mesh()
    {
        var b = new MeshBuilder(detailed: true);
        var trim = (int)BuildingSlot.Trim;
        b.Box(new Vector3(0f, -0.12f, 0f), new Vector3(RoadLayout.Width + 0.4f, 0.24f, 1f), 0, (int)BuildingSlot.Wall);
        foreach (var side in new[] { 1f, -1f })
        {
            var x = side * ((RoadLayout.Width / 2f) + 0.12f);
            b.Box(new Vector3(x, 0.85f, 0f), new Vector3(0.12f, 0.1f, 1f), 0, trim);
            b.Box(new Vector3(x, 0.4f, 0f), new Vector3(0.08f, 0.08f, 1f), 0, trim);
        }

        return b.Finish();
    }
}
