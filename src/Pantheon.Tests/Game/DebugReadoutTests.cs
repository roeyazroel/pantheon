using System.Linq;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Game.Camera;
using Pantheon.Game.Diagnostics;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The debug overlay text shows the required readouts.</summary>
public sealed class DebugReadoutTests
{
    private static readonly WorldState World = WorldGenerator.Generate(new WorldSeed(1234), TestContent.SmallWorldGen).State;

    [Fact]
    public void DebugReadout_ShowsCoreReadouts()
    {
        var text = DebugReadout.Build(Snapshot()).ToText();

        Assert.Contains("FPS 60", text);
        Assert.Contains("Tick 0", text);
        Assert.Contains("Seed 1234", text);
        Assert.Contains("Terrain Terrain3D", text);
        Assert.Contains("Camera Regional", text);
        Assert.Contains($"Settlement {World.Settlements[0].Name}", text);
        Assert.Contains("pop 40 / 60", text);
        Assert.Contains("Region boundaries OFF", text);
        Assert.Contains(DebugReadout.CameraHints, text);
        Assert.Contains(DebugReadout.KeyHints, text);
        Assert.DoesNotContain("FALLBACK", text);
    }

    [Fact]
    public void DebugReadout_ShowsRegionUnderCursor()
    {
        var settlement = World.Settlements[0];
        var cursor = new Vector3(settlement.Position.X, 12f, settlement.Position.Z);
        var region = World.Regions.GetRegion(settlement.RegionId);

        var lines = DebugReadout.Build(Snapshot() with { CursorWorldPosition = cursor }).Lines;

        Assert.Contains(lines, line => line.StartsWith($"Region #{settlement.RegionId} ", System.StringComparison.Ordinal) && line.EndsWith(region.Biome.ToString(), System.StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("fert ", System.StringComparison.Ordinal));
    }

    [Fact]
    public void DebugReadout_MarksPauseFallbackAndStatus()
    {
        var snapshot = Snapshot() with
        {
            IsPaused = true,
            TerrainBackend = "Fallback mesh",
            TerrainIsFallback = true,
            RegionBoundariesVisible = true,
            StatusMessage = "Saved",
        };

        var lines = DebugReadout.Build(snapshot).Lines;

        Assert.Contains("PAUSED", lines[0]);
        Assert.Contains(lines, line => line.Contains("FALLBACK RENDERER", System.StringComparison.Ordinal));
        Assert.Contains("Region boundaries ON", lines);
        Assert.Contains("Saved", lines);
        Assert.Equal("Cursor -", lines.Single(line => line.StartsWith("Cursor", System.StringComparison.Ordinal)));
    }

    /// <summary>A typical frame snapshot without a cursor position.</summary>
    private static DebugSnapshot Snapshot() => new()
    {
        FramesPerSecond = 60.2,
        World = World,
        SettlementRules = TestContent.Shipped.SettlementRules,
        IsPaused = false,
        TimeScale = 1.0,
        TicksPerSecond = 10.0,
        TerrainBackend = "Terrain3D",
        TerrainIsFallback = false,
        ZoomBand = CameraZoomBand.Regional,
        CameraDistance = 380f,
        RegionBoundariesVisible = false,
    };
}
