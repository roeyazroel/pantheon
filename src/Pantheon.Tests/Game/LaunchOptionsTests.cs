using Pantheon.Domain.World;
using Pantheon.Game.Launch;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Command-line options after Godot's <c>--</c> separator.</summary>
public sealed class LaunchOptionsTests
{
    [Fact]
    public void LaunchOptions_DefaultsWithoutArguments()
    {
        var options = LaunchOptions.Parse([]);

        Assert.Null(options.Seed);
        Assert.Null(options.LoadPath);
        Assert.Equal(TerrainBackendPreference.Auto, options.Terrain);
        Assert.Null(options.QuitAfterFrames);
        Assert.Null(options.AdvanceTicks);
        Assert.Null(options.CameraDistance);
        Assert.Empty(options.Warnings);
    }

    [Fact]
    public void LaunchOptions_ParsesCaptureOptions()
    {
        var options = LaunchOptions.Parse(["--advance-ticks=6000", "--camera-distance", "95.5"]);

        Assert.Equal(6000, options.AdvanceTicks);
        Assert.Equal(95.5f, options.CameraDistance);
        Assert.Empty(options.Warnings);
    }

    [Fact]
    public void LaunchOptions_WarnsAboutInvalidCaptureOptions()
    {
        var options = LaunchOptions.Parse(
            ["--advance-ticks=0", "--advance-ticks=1.5", "--camera-distance=-5", "--camera-distance=NaN", "--camera-distance=Infinity", "--camera-distance=1,5"]);

        Assert.Null(options.AdvanceTicks);
        Assert.Null(options.CameraDistance);
        Assert.Equal(6, options.Warnings.Count);
        Assert.Contains("'--advance-ticks' needs a positive integer, got '0'.", options.Warnings);
        Assert.Contains("'--camera-distance' needs a positive number of metres, got '-5'.", options.Warnings);
    }

    [Fact]
    public void LaunchOptions_ParsesEqualsAndSpaceSeparatedValues()
    {
        var options = LaunchOptions.Parse(["--seed=1234", "--load", "user://saves/a.json", "--terrain", "FALLBACK"]);

        Assert.Equal(new WorldSeed(1234), options.Seed);
        Assert.Equal("user://saves/a.json", options.LoadPath);
        Assert.Equal(TerrainBackendPreference.Fallback, options.Terrain);
        Assert.Empty(options.Warnings);
    }

    [Fact]
    public void LaunchOptions_AcceptsTextSeeds()
    {
        var options = LaunchOptions.Parse(["--seed", "first light"]);

        Assert.Equal(WorldSeed.Parse("first light"), options.Seed);
    }

    [Fact]
    public void LaunchOptions_ParsesSmokeTestOptions()
    {
        var options = LaunchOptions.Parse(["--quit-after-frames=90", "--screenshot=/tmp/shot.png", "--terrain=terrain3d"]);

        Assert.Equal(90, options.QuitAfterFrames);
        Assert.Equal("/tmp/shot.png", options.ScreenshotPath);
        Assert.Equal(TerrainBackendPreference.Terrain3D, options.Terrain);
    }

    [Fact]
    public void LaunchOptions_WarnsAboutInvalidInput()
    {
        var options = LaunchOptions.Parse(["stray", "--terrain=voxels", "--quit-after-frames=-3", "--seed", "--colour=red"]);

        Assert.Equal(TerrainBackendPreference.Auto, options.Terrain);
        Assert.Null(options.QuitAfterFrames);
        Assert.Null(options.Seed);
        Assert.Equal(5, options.Warnings.Count);
    }

    [Fact]
    public void LaunchOptions_ReportsUnknownOptionsBeforeMissingValues()
    {
        var options = LaunchOptions.Parse(["--wat"]);

        Assert.Equal("Ignoring unknown option '--wat'.", Assert.Single(options.Warnings));
    }

    [Fact]
    public void LaunchOptions_ParsesDivineDebugOptions()
    {
        var options = LaunchOptions.Parse(["--divine-power=900", "--cast=food, lightning", "--overlay=Faith", "--focus=3", "--debug-panel=on", "--select=heal"]);

        Assert.Equal(900f, options.DivinePower);
        Assert.Equal(["food", "lightning"], options.CastAtStart);
        Assert.Equal(Pantheon.Game.Presentation.OverlayKind.Faith, options.Overlay);
        Assert.Equal(3, options.FocusSettlement);
        Assert.True(options.DebugPanel);
        Assert.Equal("heal", options.SelectMiracle);
        Assert.Empty(options.Warnings);
    }

    [Fact]
    public void LaunchOptions_WarnsAboutInvalidDivineOptions()
    {
        var options = LaunchOptions.Parse(["--divine-power=-1", "--overlay=weather", "--focus=0", "--debug-panel=maybe"]);

        Assert.Null(options.DivinePower);
        Assert.Null(options.Overlay);
        Assert.Null(options.FocusSettlement);
        Assert.Null(options.DebugPanel);
        Assert.Equal(4, options.Warnings.Count);
    }
}
