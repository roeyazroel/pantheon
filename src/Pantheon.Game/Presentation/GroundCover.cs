using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;

namespace Pantheon.Game.Presentation;

/// <summary>The small things growing or lying on the ground (ADR-053).</summary>
public enum GroundCoverKind
{
    /// <summary>A tuft of grass.</summary>
    Grass,

    /// <summary>A few flowers.</summary>
    Flowers,

    /// <summary>A low bush or fern.</summary>
    Bush,

    /// <summary>A stone.</summary>
    Rock,

    /// <summary>Reeds with seed heads, in swamps.</summary>
    Reeds,
}

/// <summary>One thing on the ground.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Position">Where it stands.</param>
/// <param name="Scale">Its size, around 1.</param>
/// <param name="Yaw">Its turn about the vertical.</param>
/// <param name="Tint">Its colour: of grass, bush, petals, stone or reeds.</param>
public readonly record struct GroundCoverPlacement(GroundCoverKind Kind, Vector3 Position, float Scale, float Yaw, RgbColor Tint);

/// <summary>The ground cover (spec §61, ADR-053), from <c>settlement_view.json</c>. Presentation only.</summary>
public sealed record GroundCoverSettings
{
    /// <summary>The side of the cells, in metres; each may hold one thing.</summary>
    public required float CellMetres { get; init; }

    /// <summary>The side of the chunks it streams in, in metres.</summary>
    public required float ChunkMetres { get; init; }

    /// <summary>How near the camera it is drawn, in metres.</summary>
    public required float Radius { get; init; }

    /// <summary>For each biome, the chance a cell holds each kind, at full vegetation for grass and flowers.</summary>
    public required IReadOnlyDictionary<Biome, IReadOnlyDictionary<GroundCoverKind, float>> Biomes { get; init; }

    /// <summary>Grass colours.</summary>
    public required IReadOnlyList<RgbColor> Grass { get; init; }

    /// <summary>Dry grass, in deserts.</summary>
    public required RgbColor DryGrass { get; init; }

    /// <summary>Flowers' colours.</summary>
    public required IReadOnlyList<RgbColor> Petals { get; init; }

    /// <summary>Bushes.</summary>
    public required RgbColor Bush { get; init; }

    /// <summary>Stones.</summary>
    public required RgbColor Stone { get; init; }

    /// <summary>Reeds.</summary>
    public required RgbColor Reeds { get; init; }

    /// <summary>Throws when a value is out of range.</summary>
    /// <exception cref="ArgumentException">A value is invalid.</exception>
    public void Validate()
    {
        if (!(CellMetres >= 0.5f) || !(ChunkMetres >= CellMetres) || !(Radius > 0f) || Grass.Count == 0 || Petals.Count == 0
            || Biomes.Values.Any(chances => chances.Values.Any(chance => !(chance is >= 0f and <= 1f))) || Biomes.Values.Any(chances => chances.Values.Sum() > 1f))
        {
            throw new ArgumentException("GroundCover needs cells of at least 0.5 m, chunks no smaller than a cell, a positive radius, grass and petal colours, and chances in 0..1 adding up to at most 1 for each biome.");
        }
    }
}

/// <summary>
/// Where the ground cover lies (spec §61, ADR-053): one thing at most in each small cell, drawn by
/// <see cref="PresentationHash"/> from its place, by the biome's chances: grass and flowers as
/// thick as the land's vegetation, bushes, stones, and reeds in swamps. Nothing in water, on bare
/// rock, on a road, by a house, on a field or on a village's square; snow pales the grass. Pure.
/// </summary>
public static class GroundCoverLayout
{
    private const ulong Salt = 53;

    /// <summary>The ground cover of a square of land.</summary>
    /// <param name="world">The world, for its regions and roads.</param>
    /// <param name="terrain">Terrain, for heights, slopes and fresh water.</param>
    /// <param name="minX">The square's west edge, in metres.</param>
    /// <param name="minZ">Its north edge.</param>
    /// <param name="size">Its side.</param>
    /// <param name="clearings">The ground kept bare in the villages: by their houses, on their fields and on their squares.</param>
    /// <param name="settings">Ground cover settings.</param>
    /// <param name="rockSlope">The slope at which the ground is bare rock (the palette's).</param>
    /// <param name="into">Receives the things.</param>
    public static void Place(WorldState world, TerrainField terrain, float minX, float minZ, float size, IReadOnlyList<Clearing> clearings, GroundCoverSettings settings, float rockSlope, List<GroundCoverPlacement> into)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(into);
        var cell = settings.CellMetres;
        var cells = (int)MathF.Round(size / cell);
        var seed = world.Seed.Value;
        for (var row = 0; row < cells; row++)
        {
            for (var column = 0; column < cells; column++)
            {
                var (cx, cz) = ((ulong)(uint)(int)MathF.Floor((minX / cell) + column), (ulong)(uint)(int)MathF.Floor((minZ / cell) + row));
                float U(ulong k) => PresentationHash.Unit(Salt, seed, PresentationHash.Mix(cx, cz), k);
                var (x, z) = (minX + ((column + U(0)) * cell), minZ + ((row + U(1)) * cell));
                if (!world.Regions.TryGetRegionIdAt(x, z, out var regionId) || world.Regions.Cells[regionId] is not { Biome: not Biome.Sea } region
                    || !settings.Biomes.TryGetValue(region.Biome, out var chances))
                {
                    continue;
                }

                var height = terrain.SampleHeight(x, z);
                if (height < 0.3f || terrain.IsUnderFreshWater(x, z) || ForestLayout.InClearing(clearings, x, z) || ForestLayout.OnRoad(world.Roads, x, z))
                {
                    continue;
                }

                var kind = Choose(chances, region, U(2));
                var steep = SlopeAt(terrain, x, z) > rockSlope;
                if (kind is not { } chosen || (steep && chosen != GroundCoverKind.Rock))
                {
                    continue;
                }

                into.Add(new GroundCoverPlacement(chosen, new Vector3(x, height, z), 0.75f + (0.5f * U(3)), MathF.Tau * U(4), Tint(chosen, region, settings, U(5))));
            }
        }
    }

    /// <summary>The ground a village keeps bare: by each house, building site and ruin, on each field, and on the square by the shrine.</summary>
    /// <param name="site">The village's layout.</param>
    /// <param name="fieldSize">A field's side, in metres.</param>
    /// <param name="square">The square's radius, in metres.</param>
    /// <param name="into">Receives the bare patches.</param>
    public static void BareGround(SettlementSite site, float fieldSize, float square, List<Clearing> into)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(into);
        into.Add(new Clearing(new Vector2(site.Center.X, site.Center.Z), square));
        foreach (var house in site.Houses.Concat(site.Ruins).Concat(site.Construction is { } building ? [building.House] : []))
        {
            into.Add(new Clearing(new Vector2(house.Position.X, house.Position.Z), 3.2f * house.Scale));
        }

        foreach (var field in site.Fields)
        {
            into.Add(new Clearing(new Vector2(field.Position.X, field.Position.Z), fieldSize * 0.72f));
        }
    }

    /// <summary>
    /// Which thing a cell holds, if any: grass and flowers thin out with the land's vegetation. Stones,
    /// bushes and reeds take their shares of the cell's roll first, so they stay where they are as
    /// the grass grows and withers.
    /// </summary>
    private static GroundCoverKind? Choose(IReadOnlyDictionary<GroundCoverKind, float> chances, RegionState region, float unit)
    {
        var at = unit;
        foreach (var (kind, chance) in chances.OrderBy(pair => pair.Key is GroundCoverKind.Grass or GroundCoverKind.Flowers).ThenBy(pair => pair.Key))
        {
            var thinned = kind is GroundCoverKind.Grass or GroundCoverKind.Flowers ? chance * Math.Clamp(region.Vegetation, 0f, 1f) : chance;
            if ((at -= thinned) < 0f)
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>A thing's colour: a grass of the list (dry in deserts), paled by snow; a flower's petals; a bush, stone or reeds.</summary>
    private static RgbColor Tint(GroundCoverKind kind, RegionState region, GroundCoverSettings settings, float unit)
    {
        var snow = Math.Clamp(region.Snow, 0f, 1f);
        var white = RgbColor.ParseHex("#eef1f4");
        return kind switch
        {
            GroundCoverKind.Grass => RgbColor.Lerp(region.Biome == Biome.Desert ? settings.DryGrass : settings.Grass[Math.Min(settings.Grass.Count - 1, (int)(unit * settings.Grass.Count))], white, snow * 0.7f),
            GroundCoverKind.Flowers => settings.Petals[Math.Min(settings.Petals.Count - 1, (int)(unit * settings.Petals.Count))],
            GroundCoverKind.Bush => RgbColor.Lerp(settings.Bush, white, snow * 0.6f),
            GroundCoverKind.Rock => Shade(RgbColor.Lerp(settings.Stone, white, snow * 0.8f), 0.85f + (0.3f * unit)),
            GroundCoverKind.Reeds => settings.Reeds,
        };
    }

    /// <summary>A colour made lighter or darker.</summary>
    private static RgbColor Shade(RgbColor color, float factor) =>
        new(Math.Clamp(color.R * factor, 0f, 1f), Math.Clamp(color.G * factor, 0f, 1f), Math.Clamp(color.B * factor, 0f, 1f));

    /// <summary>The ground's slope at a point, rise over run.</summary>
    private static float SlopeAt(TerrainField terrain, float x, float z)
    {
        const float step = 1f;
        var dx = (terrain.SampleHeight(x + step, z) - terrain.SampleHeight(x - step, z)) / (2f * step);
        var dz = (terrain.SampleHeight(x, z + step) - terrain.SampleHeight(x, z - step)) / (2f * step);
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }
}

/// <summary>The ground cover as meshes built in code (ADR-053), tagged for the tree shader: stems in the trunk's slot, leaves, petals and stone in the leaves'. Pure.</summary>
public static class GroundCoverMesh
{
    /// <summary>A thing on the ground.</summary>
    /// <param name="kind">What.</param>
    /// <returns>The triangles: standing on the ground at the origin.</returns>
    public static TaggedMesh Build(GroundCoverKind kind)
    {
        var b = new MeshBuilder(detailed: false);
        var (stem, leaf) = ((int)TreeSlot.Trunk, (int)TreeSlot.Leaves);
        switch (kind)
        {
            case GroundCoverKind.Grass:
                for (var blade = 0; blade < 9; blade++)
                {
                    var angle = MathF.Tau * (blade + (0.37f * (blade % 3))) / 9f;
                    var (dx, dz) = (MathF.Cos(angle), MathF.Sin(angle));
                    var height = 0.4f + (0.3f * ((blade * 3) % 5) / 4f);
                    var foot = 0.06f + (0.08f * (blade % 2));
                    b.Tube(new Vector3(dx * foot, -0.05f, dz * foot), new Vector3(dx * (foot + 0.24f), height, dz * (foot + 0.24f)), 0.04f, 0.004f, 0.25f, 3, 0, leaf);
                }

                break;
            case GroundCoverKind.Flowers:
                foreach (var (dx, dz, height) in new[] { (0.08f, 0.02f, 0.42f), (-0.07f, 0.06f, 0.36f), (0.01f, -0.08f, 0.48f) })
                {
                    b.Tube(new Vector3(dx * 0.3f, -0.05f, dz * 0.3f), new Vector3(dx, height, dz), 0.012f, 0.01f, 1f, 3, 0, stem);
                    b.Ball(new Vector3(dx, height + 0.03f, dz), new Vector3(0.07f, 0.04f, 0.07f), 0, leaf);
                }

                break;
            case GroundCoverKind.Bush:
                foreach (var (centre, radius) in new[] { (new Vector3(0f, 0.35f, 0f), 0.5f), (new Vector3(0.35f, 0.25f, 0.15f), 0.36f), (new Vector3(-0.3f, 0.22f, -0.2f), 0.34f) })
                {
                    b.Ball(centre, new Vector3(radius, radius * 0.8f, radius), 0, leaf);
                }

                break;
            case GroundCoverKind.Rock:
                b.Ball(new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.3f, 0.35f), 0, leaf);
                break;
            case GroundCoverKind.Reeds:
                for (var reed = 0; reed < 6; reed++)
                {
                    var angle = MathF.Tau * reed / 6f;
                    var (dx, dz) = (MathF.Cos(angle) * 0.12f, MathF.Sin(angle) * 0.12f);
                    var height = 1.1f + (0.3f * (reed % 3) / 2f);
                    b.Tube(new Vector3(dx, -0.05f, dz), new Vector3(dx * 1.6f, height, dz * 1.6f), 0.02f, 0.01f, 1f, 3, 0, leaf);
                    if (reed % 2 == 0)
                    {
                        b.Tube(new Vector3(dx * 1.6f, height - 0.18f, dz * 1.6f), new Vector3(dx * 1.6f, height, dz * 1.6f), 0.035f, 0.03f, 1f, 4, 0, stem, capped: true);
                    }
                }

                break;
        }

        return b.Finish();
    }
}
