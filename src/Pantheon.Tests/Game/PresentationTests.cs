using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Terrain;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Engine-agnostic presentation data shared by both terrain renderers.</summary>
public sealed class PresentationTests
{
    private static readonly GeneratedWorld World = WorldGenerator.Generate(new WorldSeed(1234), TestContent.SmallWorldGen);
    private static readonly TerrainField Terrain = World.Terrain;

    [Fact]
    public void TerrainColorizer_ProducesOneDeterministicPixelPerSample()
    {
        var first = TerrainColorizer.ComputeRgba(Terrain, TestContent.Palette);
        var second = TerrainColorizer.ComputeRgba(Terrain, TestContent.Palette);

        Assert.Equal(Terrain.Resolution * Terrain.Resolution * TerrainColorizer.BytesPerPixel, first.Length);
        Assert.Equal(first, second);
        Assert.All(Enumerable.Range(0, Terrain.Resolution * Terrain.Resolution), i => Assert.Equal(TerrainColorizer.NeutralAlpha, first[(i * 4) + 3]));
    }

    [Fact]
    public void TerrainColorizer_DeepSeaFloorUsesSeaFloorColour()
    {
        var palette = TestContent.Palette with { ColorVariation = 0f };

        var corner = TerrainColorizer.ComputeColor(Terrain, palette, 0, 0);

        Assert.True(Terrain.GetHeight(0, 0) < -palette.DeepWaterDepth);
        Assert.Equal(palette.SeaFloor.R, corner.R, 4);
        Assert.Equal(palette.SeaFloor.G, corner.G, 4);
        Assert.Equal(palette.SeaFloor.B, corner.B, 4);
    }

    [Fact]
    public void TerrainMeshBuilder_ChunksCoverTheWholeTerrain()
    {
        const int Cells = 32;
        const int Stride = 2;
        var rgba = TerrainColorizer.ComputeRgba(Terrain, TestContent.Palette);
        var chunks = TerrainMeshBuilder.ChunksPerSide(Terrain, Cells, Stride);

        var maxX = 0f;
        var triangles = 0;
        for (var z = 0; z < chunks; z++)
        {
            for (var x = 0; x < chunks; x++)
            {
                var mesh = TerrainMeshBuilder.BuildChunk(Terrain, rgba, x, z, Cells, Stride);
                Assert.Equal(mesh.Positions.Length, mesh.Normals.Length);
                Assert.Equal(mesh.Positions.Length, mesh.Colors.Length);
                Assert.All(mesh.Indices, index => Assert.InRange(index, 0, mesh.Positions.Length - 1));
                maxX = MathF.Max(maxX, mesh.Positions.Max(position => position.X));
                triangles += mesh.Indices.Length / 3;
            }
        }

        var cellsPerSide = (Terrain.Resolution - 1 + Stride - 1) / Stride;
        Assert.Equal(cellsPerSide * cellsPerSide * 2, triangles);
        Assert.Equal(Terrain.MaxCoordinate, maxX);
    }

    [Fact]
    public void TerrainMeshBuilder_FrontFacesPointUp()
    {
        var rgba = TerrainColorizer.ComputeRgba(Terrain, TestContent.Palette);
        var mesh = TerrainMeshBuilder.BuildChunk(Terrain, rgba, 0, 0, 8, 1);

        var a = mesh.Positions[mesh.Indices[0]];
        var b = mesh.Positions[mesh.Indices[1]];
        var c = mesh.Positions[mesh.Indices[2]];

        // Godot treats clockwise triangles as front faces, so the right-hand-rule normal of an
        // upward-facing triangle points down.
        Assert.True(Vector3.Cross(b - a, c - a).Y < 0f);
        Assert.All(mesh.Normals, normal => Assert.True(normal.Y > 0f));
    }

    [Fact]
    public void RegionBoundaryBuilder_DrapesLinesAboveTheSurface()
    {
        const float Lift = 0.5f;
        var lines = RegionBoundaryBuilder.BuildGridLines(World.State.Regions, Terrain, Lift, 16f);

        Assert.NotEmpty(lines);
        Assert.Equal(0, lines.Length % 2);
        Assert.All(lines, point => Assert.Equal(MathF.Max(Terrain.SampleHeight(point.X, point.Z), 0f) + Lift, point.Y, 3));
    }

    [Fact]
    public void RegionBoundaryBuilder_OutlineClosesAroundRegion()
    {
        var grid = World.State.Regions;
        var outline = RegionBoundaryBuilder.BuildOutline(grid, 17, Terrain, 0.5f, 8f);
        var (column, row) = grid.GetCoordinates(17);

        Assert.Equal(0, outline.Length % 2);
        Assert.Equal(column * grid.RegionSize, outline[0].X);
        Assert.Equal(row * grid.RegionSize, outline[0].Z);
        Assert.Equal(outline[0].X, outline[^1].X, 3);
        Assert.Equal(outline[0].Z, outline[^1].Z, 3);
        Assert.All(outline, point => Assert.InRange(point.X, column * grid.RegionSize, (column + 1) * grid.RegionSize));
    }

    [Fact]
    public void WaterDepthMap_IsZeroOnLandAndFullInDeepWater()
    {
        const int Resolution = 64;
        const float DeepWater = 18f;
        var texel = Terrain.WorldSize / Resolution;

        var depths = WaterDepthMap.Compute(Terrain, Resolution, DeepWater);

        Assert.Equal(Resolution * Resolution, depths.Length);
        Assert.Contains(depths, depth => depth == 0);
        Assert.Contains(depths, depth => depth == 255);
        Assert.All(Enumerable.Range(0, depths.Length), i =>
        {
            var height = Terrain.SampleHeight(((i % Resolution) + 0.5f) * texel, ((i / Resolution) + 0.5f) * texel);
            Assert.Equal(RgbColor.ToByte(-height / DeepWater), depths[i]);
        });
    }

    [Fact]
    public void SettlementLabel_ShowsNameAndPopulation()
    {
        var settlement = new SettlementState
        {
            Id = 1,
            Name = "Mossgate",
            RegionId = 0,
            Position = new GroundPosition(0f, 0f),
            Population = 41,
            Food = 0f,
            Housing = 60,
            Health = 1f,
            Beliefs = [],
            ConstructionProgress = 0f,
            Ruins = 0,
            ShieldedUntilTick = 0,
            Emigration = 0f,
            OldFaith = null,
            SanctuaryGodId = null,
            SanctuaryUntilTick = 0,
        };

        Assert.Equal("Mossgate\n41 people", SettlementLabel.Format(settlement));
        settlement.Population = 1;
        Assert.Equal("Mossgate\n1 person", SettlementLabel.Format(settlement));
    }

    [Fact]
    public void PresentationHash_IsDeterministicAndSpreadsOverTheUnitInterval()
    {
        var values = Enumerable.Range(0, 4096).Select(i => PresentationHash.Unit(7, (ulong)i)).ToArray();

        Assert.Equal(values, Enumerable.Range(0, 4096).Select(i => PresentationHash.Unit(7, (ulong)i)));
        Assert.All(values, value => Assert.InRange(value, 0f, 0.99999994f));
        Assert.InRange(values.Average(), 0.47, 0.53);
        Assert.NotEqual(PresentationHash.Mix(1, 2), PresentationHash.Mix(2, 1));
    }
}
