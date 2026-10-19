using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Presentation;
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
    public void EveryTree_StandsOnItsRoots_AtATreesHeight_WithLeavesButTheSnag()
    {
        foreach (var kind in Enum.GetValues<TreeKind>())
        {
            var tree = TreeMesh.Build(kind, detailed: true);
            var leaves = tree.Positions.Where((_, i) => (int)tree.Tags[i].Y == (int)TreeSlot.Leaves).ToList();
            Assert.InRange(tree.Positions.Min(p => p.Y), -0.4f, 0f);
            Assert.InRange(tree.Positions.Max(p => p.Y), 4.5f, 9.5f);
            Assert.True(kind == TreeKind.Snag ? leaves.Count == 0 : leaves.Count > 20, $"{kind}'s leaves");
            Assert.True(TreeMesh.Build(kind, detailed: false).Indices.Length <= tree.Indices.Length);
        }
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
        Assert.All(Enum.GetValues<GroundCoverKind>(), kind => Assert.NotEmpty(GroundCoverMesh.Build(kind).Indices));
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
        var json = TestContent.ReadText(SettlementViewSettings.FileName).Replace("\"CellMetres\": 1.2", "\"CellMetres\": 0.2", StringComparison.Ordinal);

        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));

        Assert.Contains("GroundCover needs cells of at least 0.5 m", exception.Message, StringComparison.Ordinal);
    }

    private static WeatherCell Cell(int id, float x, float z, float radius, float wind) => new()
    {
        Id = id, X = x, Z = z, VelocityX = 0f, VelocityZ = 0f, Radius = radius, Pressure = 0.5f, Temperature = 0.5f, Humidity = 0.5f, Precipitation = 0f, Wind = wind, ElectricalActivity = 0f, SummonedByGodId = null,
    };
}
