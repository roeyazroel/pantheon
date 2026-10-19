using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.Ecology;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Simulation.Terrain;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>World streaming (spec §84, ADR-050): the land near the camera carries its detail.</summary>
public sealed class StreamingTests
{
    private static readonly StreamingSettings Settings = TestContent.SettlementView.Streaming;

    [Fact]
    public void ChunksNearTheCamera_Load_NearestFirst_AFewAnUpdate_AndTheFirstUpdateMayTakeThemAll()
    {
        var streamer = new ChunkStreamer(10, 100f, 250f, 400f);
        var camera = new Vector3(450f, 0f, 450f);

        var first = streamer.Update(camera, budget: 3);
        var rest = streamer.Update(camera, budget: int.MaxValue);
        var again = streamer.Update(camera, budget: int.MaxValue);

        Assert.Equal(3, first.Load.Count);
        Assert.Equal(44, first.Load[0]);
        Assert.All(first.Load.Skip(1), chunk => Assert.Contains(chunk, new[] { 34, 43, 45, 54 }));
        Assert.Equal(first.Load.Count + rest.Load.Count, streamer.Loaded.Count);
        Assert.All(streamer.Loaded, chunk => Assert.InRange(Math.Abs((chunk % 10 * 100f) + 50f - 450f), 0f, 350f));
        Assert.Empty(again.Load);
        Assert.Empty(again.Unload);
    }

    [Fact]
    public void LoadedChunks_StayUntilTheCameraIsWellAway_ThenGo_AndAHighCameraLoadsNothingBeneath()
    {
        var streamer = new ChunkStreamer(10, 100f, 250f, 400f);
        streamer.Update(new Vector3(450f, 0f, 450f), int.MaxValue);
        var loaded = streamer.Loaded.ToHashSet();

        var nudged = streamer.Update(new Vector3(600f, 0f, 450f), int.MaxValue);
        var moved = streamer.Update(new Vector3(950f, 0f, 950f), int.MaxValue);
        var high = new ChunkStreamer(10, 100f, 250f, 400f).Update(new Vector3(450f, 300f, 450f), int.MaxValue);

        Assert.Empty(nudged.Unload);
        Assert.Contains(44, moved.Unload);
        Assert.DoesNotContain(99, moved.Unload);
        Assert.Contains(99, streamer.Loaded);
        Assert.True(loaded.Count > 10);
        Assert.Empty(high.Load);
    }

    [Fact]
    public void ADetailedTerrainChunk_MeetsItsCoarseNeighbour_WithoutACrack()
    {
        const int side = 257;
        var heights = Enumerable.Range(0, side * side).Select(i => 20f + (8f * MathF.Sin(i % side * 0.37f)) + (5f * MathF.Cos(i / side * 0.53f))).ToArray();
        var field = new TerrainField(side, 2f, heights, new float[side * side], new float[side * side], Enumerable.Repeat(Biome.Grassland, side * side).ToArray());
        var rgba = new byte[side * side * TerrainColorizer.BytesPerPixel];

        var coarse = TerrainMeshBuilder.BuildChunk(field, rgba, 1, 0, 64, 2);
        var detailed = TerrainMeshBuilder.BuildChunk(field, rgba, 0, 0, 128, 1, edgeStride: 2);
        var raw = TerrainMeshBuilder.BuildChunk(field, rgba, 0, 0, 128, 1);
        var x = 128 * 2f;
        float CoarseEdge(float z)
        {
            var edge = coarse.Positions.Where(p => p.X == x).OrderBy(p => p.Z).ToList();
            var below = edge.Last(p => p.Z <= z);
            var above = edge.First(p => p.Z >= z);
            return above.Z == below.Z ? below.Y : below.Y + ((above.Y - below.Y) * (z - below.Z) / (above.Z - below.Z));
        }

        var shared = detailed.Positions.Where(p => p.X == x).ToList();
        Assert.Equal(129, shared.Count);
        Assert.All(shared, p => Assert.Equal(CoarseEdge(p.Z), p.Y, 4));
        Assert.Contains(raw.Positions.Where(p => p.X == x), p => MathF.Abs(p.Y - CoarseEdge(p.Z)) > 0.01f);
        Assert.Equal(raw.Positions.Single(p => p.X == 64f && p.Z == 64f).Y, detailed.Positions.Single(p => p.X == 64f && p.Z == 64f).Y);
    }

    [Fact]
    public void F1_ShowsWhatStreams_AndBadSettingsAreRejected()
    {
        var status = new StreamingStatus(192, 2209, (6, 2209), 3, 5, Settings);

        Assert.Equal(
            "Streaming: forests planted in 192 of 2209 chunks within 2100 m; terrain in detail in 6 of 2209 chunks within 650 m; fires drawn 3 of 5 within 2500 m",
            StreamingReadout.Describe(status));
        Assert.Contains("terrain in detail by Terrain3D's own distance", StreamingReadout.Describe(status with { Terrain = null }), StringComparison.Ordinal);
        var json = TestContent.ReadText(SettlementViewSettings.FileName).Replace("\"ForestUnloadRadius\": 2500.0", "\"ForestUnloadRadius\": 1000.0", StringComparison.Ordinal);
        var exception = Assert.Throws<ContentFormatException>(() => SettlementViewSettings.Parse(json));
        Assert.Contains("Streaming: Streaming needs positive radii", exception.Message, StringComparison.Ordinal);
    }
}
