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
        Assert.Empty(options.Warnings);
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
}
