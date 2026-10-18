using System;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Simulation.Terrain;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>Cursor picking against the heightfield, independent of the terrain renderer.</summary>
public sealed class TerrainRaycasterTests
{
    private const float Tolerance = 0.05f;
    private static readonly TerrainField Terrain = WorldGenerator.Generate(new WorldSeed(1234), TestContent.SmallWorldGen).Terrain;

    [Theory]
    [InlineData(1024f, 1024f)]
    [InlineData(700f, 1300f)]
    [InlineData(5f, 5f)]
    [InlineData(1500f, 400f)]
    public void TerrainRaycaster_DownwardRayHitsSurface(float x, float z)
    {
        var hit = Assert.IsType<Vector3>(Raycast(new Vector3(x, 500f, z), -Vector3.UnitY));

        Assert.Equal(x, hit.X, 3);
        Assert.Equal(z, hit.Z, 3);
        Assert.Equal(MathF.Max(Terrain.SampleHeight(x, z), 0f), hit.Y, Tolerance);
    }

    [Fact]
    public void TerrainRaycaster_ObliqueRayHitsVisibleSurface()
    {
        var origin = new Vector3(-300f, 400f, -250f);
        var direction = new Vector3(1024f, 0f, 1024f) - origin;

        var hit = Assert.IsType<Vector3>(Raycast(origin, direction));

        Assert.Equal(MathF.Max(Terrain.SampleHeight(hit.X, hit.Z), 0f), hit.Y, Tolerance);
        Assert.InRange(hit.X, 0f, Terrain.MaxCoordinate);
        Assert.InRange(hit.Z, 0f, Terrain.MaxCoordinate);
    }

    [Fact]
    public void TerrainRaycaster_RaysAwayFromTheWorldMiss()
    {
        Assert.Null(Raycast(new Vector3(1024f, 500f, 1024f), Vector3.UnitY));
        Assert.Null(Raycast(new Vector3(-500f, 100f, -500f), -Vector3.UnitX));
        Assert.Null(Raycast(new Vector3(5000f, 500f, 5000f), -Vector3.UnitY));
        Assert.Null(Raycast(new Vector3(1024f, 500f, 1024f), Vector3.Zero));
    }

    [Fact]
    public void TerrainRaycaster_RespectsMaxDistance()
    {
        Assert.False(TerrainRaycaster.TryRaycast(Terrain, new Vector3(1024f, 500f, 1024f), -Vector3.UnitY, 100f, 0f, out _));
    }

    /// <summary>Raycasts against the test terrain with the sea surface as floor; null on a miss.</summary>
    private static Vector3? Raycast(Vector3 origin, Vector3 direction) =>
        TerrainRaycaster.TryRaycast(Terrain, origin, direction, 10_000f, 0f, out var hit) ? hit : null;
}
