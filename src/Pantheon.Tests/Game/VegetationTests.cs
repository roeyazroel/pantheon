using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Presentation;
using Pantheon.Game.Presentation.Nature;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Trees, ground cover and the wind (spec §61, §93, ADR-053).</summary>
public sealed class VegetationTests
{
    private static readonly TreeSettings Trees = TestContent.SettlementView.Trees;
    private static readonly GroundCoverSettings Cover = TestContent.SettlementView.GroundCover;

    [Fact]
    public void EveryTree_StandsOnItsRoots_AtATreesHeight_WithLeavesButTheSnag_AndIsLighterFar()
    {
        foreach (var kind in Enum.GetValues<TreeKind>())
        {
            for (var variant = 0; variant < TreeGrowth.Variants; variant++)
            {
                var tree = TreeGrowth.Grow(kind, variant, PlantDetail.Near);
                var leaves = Enumerable.Range(0, tree.Positions.Length).Count(i => (int)tree.Detail[i].X == (int)PlantPart.Leaves);
                Assert.InRange(tree.Positions.Min(p => p.Y), -0.4f, 0f);
                Assert.InRange(tree.Height, 4.5f, 11f);
                Assert.True(kind == TreeKind.Snag ? leaves == 0 : leaves > 40, $"{kind} {variant}'s leaves: {leaves}");
                Assert.True(TreeGrowth.Grow(kind, variant, PlantDetail.Far).Triangles < tree.Triangles, $"{kind} {variant} far");
                Assert.InRange(tree.Triangles, 50, 2500);
            }
        }
    }

    [Fact]
    public void ATree_IsGrownTheSameEveryTime_AndItsVariantsDiffer()
    {
        var once = TreeGrowth.Grow(TreeKind.Oak, 1, PlantDetail.Near);
        var again = TreeGrowth.Grow(TreeKind.Oak, 1, PlantDetail.Near);
        var other = TreeGrowth.Grow(TreeKind.Oak, 2, PlantDetail.Near);

        Assert.Equal(once.Positions, again.Positions);
        Assert.Equal(once.Indices, again.Indices);
        Assert.NotEqual(once.Positions, other.Positions);
    }

    [Fact]
    public void LeafCards_ShowTheirCellOfTheAtlas_AndAreLitAsTheirCrown()
    {
        var oak = TreeGrowth.Grow(TreeKind.Oak, 0, PlantDetail.Near);
        var (u0, v0, u1, v1) = LeafAtlas.Bounds(LeafCell.Broad);
        var cards = Enumerable.Range(0, oak.Positions.Length).Where(i => (int)oak.Detail[i].X == (int)PlantPart.Leaves).ToList();

        Assert.All(cards, i => Assert.True(oak.Uv[i].X >= u0 - 1e-4f && oak.Uv[i].X <= u1 + 1e-4f && oak.Uv[i].Y >= v0 - 1e-4f && oak.Uv[i].Y <= v1 + 1e-4f));
        Assert.All(cards, i => Assert.InRange(oak.Detail[i].Y, 0.3f, 1f));
        // The crown's top is lit, so its cards' normals there point up.
        var top = cards.OrderByDescending(i => oak.Positions[i].Y).Take(8);
        Assert.All(top, i => Assert.True(oak.Normals[i].Y > 0.2f));
        Assert.All(oak.Indices, i => Assert.InRange(i, 0, oak.Positions.Length - 1));
    }

    [Fact]
    public void TheLeafAtlas_HasLeavesInEveryCell_AndItsSmallMipmapsKeepThem()
    {
        var (rgba, mipmaps) = LeafAtlas.Leaves();
        var full = LeafAtlas.Width * LeafAtlas.Height * 4;
        float Coverage(int offset, int pixels) => Enumerable.Range(0, pixels).Count(p => rgba[offset + (p * 4) + 3] > 127) / (float)pixels;

        Assert.Equal(9, mipmaps);
        foreach (var cell in Enum.GetValues<LeafCell>())
        {
            var (u0, v0, _, _) = LeafAtlas.Bounds(cell);
            var (left, top) = ((int)(u0 * LeafAtlas.Width), (int)(v0 * LeafAtlas.Height));
            var covered = 0;
            for (var y = top; y < top + LeafAtlas.CellSize; y += 4)
            {
                for (var x = left; x < left + LeafAtlas.CellSize; x += 4)
                {
                    covered += rgba[(((y * LeafAtlas.Width) + x) * 4) + 3] > 127 ? 1 : 0;
                }
            }

            Assert.True(covered > 300, $"{cell}: {covered}");
        }

        var largest = Coverage(0, LeafAtlas.Width * LeafAtlas.Height);
        var quarter = full + (LeafAtlas.Width * LeafAtlas.Height);
        Assert.InRange(Coverage(quarter, LeafAtlas.Width * LeafAtlas.Height / 16), largest * 0.85f, largest * 1.15f);
        Assert.Equal(LeafAtlas.Leaves().Rgba, rgba);
    }

    [Fact]
    public void EveryGroundPlant_IsGrown_InEachOfItsVariants()
    {
        foreach (var kind in Enum.GetValues<GroundCoverKind>())
        {
            for (var variant = 0; variant < GroundPlants.Variants(kind); variant++)
            {
                var plant = GroundPlants.Grow(kind, variant);
                Assert.NotEmpty(plant.Indices);
                Assert.InRange(plant.Height, 0.1f, 1.8f);
                Assert.InRange(plant.Triangles, 10, 600);
            }
        }

        var stone = GroundPlants.Grow(GroundCoverKind.Rock, 0);
        Assert.All(stone.Detail, d => Assert.Equal((int)PlantPart.Stone, (int)d.X));
        Assert.NotEqual(stone.Positions, GroundPlants.Grow(GroundCoverKind.Rock, 1).Positions);
    }

    [Fact]
    public void ARoad_RoundsItsCorners_AndWandersALittleInWidth()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(77));
        var a = session.World.Settlements[0].Position;
        var corner = new GroundPosition(a.X + 120f, a.Z);
        var end = new GroundPosition(a.X + 120f, a.Z + 120f);
        var road = new Pantheon.Domain.Roads.RoadState { Id = 3, FromSettlementId = 1, ToSettlementId = 2, Path = [a, corner, end], Length = 240f, Built = 240f };

        var ribbon = RoadLayout.Ribbon(road, session.Terrain);
        var nearest = ribbon.Min(s => Vector2.Distance(new Vector2(s.Centre.X, s.Centre.Z), new Vector2(corner.X, corner.Z)));

        Assert.True(nearest > 5f, $"the corner is cut by {nearest} m");
        Assert.All(Enumerable.Range(0, 400), i => Assert.InRange(RoadLayout.WidthAt(3, i * 2.5f), RoadLayout.Width * 0.9f, RoadLayout.Width * 1.1f));
        Assert.True(Enumerable.Range(0, 400).Select(i => RoadLayout.WidthAt(3, i * 2.5f)).Distinct().Count() > 100);
        Assert.All(ribbon.Zip(ribbon.Skip(1)), pair => Assert.InRange(pair.Second.Along - pair.First.Along, 0.01f, RoadLayout.Step + 0.01f));
    }

    [Fact]
    public void ABridge_IsBuiltToItsSpan_ItsPostsKeepingTheirSize()
    {
        var short_ = BridgeLayout.Mesh(8f);
        var long_ = BridgeLayout.Mesh(40f);

        Assert.InRange(long_.Positions.Max(p => p.Z) - long_.Positions.Min(p => p.Z), 39.9f, 40.3f);
        Assert.True(long_.Indices.Length > short_.Indices.Length * 3);
        Assert.Equal(short_.Positions.Max(p => p.Y), long_.Positions.Max(p => p.Y), 3);
    }

    [Fact]
    public void EachBiome_GrowsItsSpecies_InTheirShares_AndAnUnlistedBiomeGrowsGrasslands()
    {
        var island = Enumerable.Range(0, 1000).Select(i => Trees.Pick(Biome.Island, i / 1000f)).ToList();
        var tundra = Enumerable.Range(0, 1000).Select(i => Trees.Pick(Biome.Tundra, i / 1000f)).ToHashSet();

        Assert.InRange(island.Count(k => k == TreeKind.Palm) / 1000f, 0.65f, 0.75f);
        Assert.Equal(new HashSet<TreeKind> { TreeKind.Fir, TreeKind.Birch }, tundra);
        Assert.Equal(Trees.Pick(Biome.Grassland, 0.3f), Trees.Pick(Biome.Sea, 0.3f));
        var json = TestContent.ReadText(SettlementViewSettings.FileName).Replace("\"Snag\": { \"Trunk\"", "\"Stump\": { \"Trunk\"", StringComparison.Ordinal);
        Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
    }

    [Fact]
    public void TheWind_IsTheStrongestCellsOverAPlace_FadingToItsEdge_AndBlowsTheWeathersWay()
    {
        var weather = new WeatherState { Spell = WeatherSpell.Normal, SpellEndsTick = 0, NextCellId = 3, WindDirection = MathF.PI / 2f, Cells = [Cell(1, 0f, 0f, 400f, 0.8f), Cell(2, 1000f, 0f, 400f, 0.3f)] };

        var centre = Breeze.At(weather, new GroundPosition(0f, 0f));
        var edge = Breeze.At(weather, new GroundPosition(300f, 0f));
        var calm = Breeze.At(weather, new GroundPosition(0f, 3000f));

        Assert.Equal(0.8f, centre.Strength, 3);
        Assert.Equal(0.2f, edge.Strength, 3);
        Assert.Equal(0f, calm.Strength);
        Assert.Equal((0f, 1f), (MathF.Round(centre.X, 3), MathF.Round(centre.Z, 3)));
    }

    [Fact]
    public void GroundCover_KeepsOffWaterAndBareGround_ThinsWithVegetation_AndIsTheSameEachTime()
    {
        var session = GameSession.CreateNew(TestContent.Small, new WorldSeed(1234));
        var world = session.World;
        var palette = TestContent.Palette;
        var village = world.Settlements[0];
        var site = SettlementLayout.ComputeSite(village, session.Content.SettlementRules, session.Terrain, TestContent.SettlementView.Layout);
        var bare = new List<Clearing>();
        GroundCoverLayout.BareGround(site, TestContent.SettlementView.Layout.FieldSize, TestContent.SettlementView.Layout.ShrineClearance, bare);
        var (minX, minZ) = (village.Position.X - 64f, village.Position.Z - 64f);
        List<GroundCoverPlacement> Lay()
        {
            var into = new List<GroundCoverPlacement>();
            for (var x = 0; x < 4; x++)
            {
                for (var z = 0; z < 4; z++)
                {
                    GroundCoverLayout.Place(world, session.Terrain, minX + (x * 32f), minZ + (z * 32f), 32f, bare, Cover, palette.RockSlopeStart, into);
                }
            }

            return into;
        }

        var lush = Lay();
        var again = Lay();
        world.Regions.Cells.ForEach(r => r.Vegetation *= 0.25f);
        var thin = Lay();

        Assert.NotEmpty(lush);
        Assert.Equal(lush, again);
        Assert.All(lush, p => Assert.False(session.Terrain.IsUnderFreshWater(p.Position.X, p.Position.Z) || p.Position.Y < 0.3f));
        Assert.All(lush, p => Assert.False(ForestLayout.InClearing(bare, p.Position.X, p.Position.Z)));
        Assert.True(thin.Count(p => p.Kind == GroundCoverKind.Grass) < lush.Count(p => p.Kind == GroundCoverKind.Grass) * 0.5f);
        Assert.Equal(thin.Count(p => p.Kind == GroundCoverKind.Rock), lush.Count(p => p.Kind == GroundCoverKind.Rock));
        Assert.Contains(bare, c => c.Center == new Vector2(site.Houses[0].Position.X, site.Houses[0].Position.Z));
    }

    [Fact]
    public void TheStreamer_LooksOnlyNearTheCamera_AndLoadsJustWhatAFullLookWould()
    {
        var streamer = new ChunkStreamer(400, 32f, 100f, 132f);
        var camera = new Vector3(6400f, 20f, 3300f);

        var step = streamer.Update(camera, int.MaxValue);

        var expected = Enumerable.Range(0, 400 * 400).Where(chunk =>
        {
            var (minX, minZ) = (chunk % 400 * 32f, chunk / 400 * 32f);
            var dx = MathF.Max(0f, MathF.Max(minX - camera.X, camera.X - (minX + 32f)));
            var dz = MathF.Max(0f, MathF.Max(minZ - camera.Z, camera.Z - (minZ + 32f)));
            return MathF.Sqrt((dx * dx) + (dz * dz) + (camera.Y * camera.Y)) <= 100f;
        }).ToHashSet();
        Assert.Equal(expected, step.Load.ToHashSet());
        Assert.InRange(expected.Count, 30, 60);
    }

    [Fact]
    public void GroundCoverSettings_OutOfRange_AreRejected()
    {
        var json = TestContent.ReadText(SettlementViewSettings.FileName).Replace("\"CellMetres\": 0.8", "\"CellMetres\": 0.2", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));

        Assert.Contains("GroundCover needs cells of at least 0.5 m", exception.Message, StringComparison.Ordinal);
    }

    private static WeatherCell Cell(int id, float x, float z, float radius, float wind) => new()
    {
        Id = id, X = x, Z = z, VelocityX = 0f, VelocityZ = 0f, Radius = radius, Pressure = 0.5f, Temperature = 0.5f, Humidity = 0.5f, Precipitation = 0f, Wind = wind, ElectricalActivity = 0f, SummonedByGodId = null,
    };
}
