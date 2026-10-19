using System;
using System.Linq;
using System.Numerics;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Game.Camera;
using Pantheon.Game.Launch;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Worlds;

/// <summary>World sizes (spec §6, ADR-027): the presets, the worlds they make, the camera and the options that choose them.</summary>
public sealed class WorldSizeTests
{
    /// <summary>The small test world, with a "Large" size of twice its width, more villages and two more gods.</summary>
    private static GameContent Scaled => TestContent.Small with
    {
        Sizes = TestContent.Shipped.Sizes with
        {
            Sizes =
            [
                Preset("Small", 8f, 16, 8, 4),
                Preset("Large", 16f, 32, 12, 6),
            ],
        },
    };

    [Fact]
    public void ShippedSizes_AreSmallLargeAndHuge_TheFirstBeingTheContentsOwnWorld()
    {
        var content = TestContent.Shipped;

        Assert.Equal(["Small", "Large", "Huge", "Vast"], content.Sizes.Sizes.Select(s => s.Name));
        Assert.Equal([2, 3, 4, 4], content.Sizes.Sizes.Select(s => s.PantheonMaxGods));
        Assert.Same(content.WorldGen, WorldMaker.AtSize(content, "small").WorldGen);
        Assert.Same(content.Pantheon, WorldMaker.AtSize(content, "small").Pantheon);
        Assert.Same(content.WorldGen, WorldMaker.AtSize(content, "Gigantic").WorldGen);
        Assert.Same(content.WorldGen, WorldMaker.AtSize(content, null).WorldGen);
        Assert.Equal("Huge", WorldMaker.Find(content, "HUGE")?.Name);
    }

    [Fact]
    public void ALargerSize_AddsLand_Villages_Rivers_AndGods()
    {
        var content = TestContent.Shipped;
        var (worldGen, pantheon) = WorldMaker.AtSize(content, "Huge");
        var names = worldGen.StartingSettlement.Names;

        Assert.Equal(8192f, worldGen.HeightmapResolution * worldGen.VertexSpacing);
        Assert.Equal(128, worldGen.RegionsPerSide);
        Assert.Equal(40, worldGen.StartingSettlement.Count);
        Assert.True(names.Count >= 40);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(24, worldGen.Hydrology!.MaxRivers);
        Assert.Equal(["Ilyra", "Vael", "Seren", "Kaio", "Aurel", "Maren", "Thorn", "Ysolde"], pantheon.Gods.Select(g => g.Name));
        Assert.Equal(6, WorldMaker.AtSize(content, "Large").Pantheon.Gods.Count);
    }

    [Fact]
    public void Describe_TellsTheIslandsWidth_VillagesAndGods() =>
        Assert.Equal(
            ["2 km across, 8 villages and 4 gods", "4 km across, 20 villages and 6 gods", "8 km across, 40 villages and 8 gods", "12 km across, 48 villages and 8 gods"],
            TestContent.Shipped.Sizes.Sizes.Select(WorldMaker.Describe));

    [Fact]
    public void ALargeWorld_IsMadeWithItsVillages_Gods_Species_AndVictories()
    {
        var content = Scaled;
        var species = content.Creatures.Species[^1].Name;
        WorldMaker.Validate(content.Sizes, content.WorldGen, content.Pantheon);

        var small = GameSession.CreateNew(content, new WorldSeed(5)).World;
        var world = GameSession.CreateNew(content, new WorldSeed(5), new NewWorldOptions(species, "large", VictorySettings.All)).World;

        Assert.Equal(32, world.Regions.Columns);
        Assert.InRange(world.Settlements.Count, small.Settlements.Count + 1, 12);
        Assert.Equal(world.Settlements.Count, world.Settlements.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(6, world.Gods.Count);
        Assert.Equal(6, world.Gods.Select(g => g.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(species, world.Creatures.Single(c => c.GodId == 1).Species);
        Assert.Equal(VictorySettings.All with { PantheonMaxGods = 3 }, world.Victory);
        Assert.Equal(2, GameSession.CreateNew(content, new WorldSeed(5)).World.Victory.PantheonMaxGods);
    }

    [Fact]
    public void TheSameSeedAndSize_MakeTheSameWorld()
    {
        var options = new NewWorldOptions(Size: "Large");

        var first = GameSession.CreateNew(Scaled, new WorldSeed(9), options).World;
        var second = GameSession.CreateNew(Scaled, new WorldSeed(9), options).World;

        WorldAssert.Equal(first, second);
    }

    [Fact]
    public void Validate_RefusesSizesThatCannotBeMade()
    {
        var content = Scaled;
        var sizes = content.Sizes;

        Assert.Contains("own name", Refusal(sizes with { Sizes = [sizes.Sizes[0], sizes.Sizes[1] with { Name = "SMALL" }] }), StringComparison.Ordinal);
        Assert.Contains("between 2 and 8 gods", Refusal(sizes with { Sizes = [sizes.Sizes[0], sizes.Sizes[1] with { Gods = 9 }] }), StringComparison.Ordinal);
        Assert.Contains("must describe the world", Refusal(sizes with { Sizes = [sizes.Sizes[0] with { Settlements = 5 }] }), StringComparison.Ordinal);
        Assert.Contains("Names", Refusal(sizes with { Sizes = [sizes.Sizes[0], sizes.Sizes[1] with { Settlements = 200 }] }), StringComparison.Ordinal);
        Assert.Contains("win together", Refusal(sizes with { Sizes = [sizes.Sizes[0], sizes.Sizes[1] with { PantheonMaxGods = 7 }] }), StringComparison.Ordinal);

        string Refusal(WorldSizes bad) => Assert.Throws<ArgumentException>(() => WorldMaker.Validate(bad, content.WorldGen, content.Pantheon)).Message;
    }

    [Fact]
    public void TheCamera_ZoomsFurtherOut_OverALargerIsland()
    {
        var settings = GodCameraSettings.Default;
        var camera = new GodCameraModel(settings);

        camera.SetBounds(Vector2.Zero, new Vector2(2048f, 2048f));
        Assert.Equal(settings.MaxDistance, camera.MaxDistance);
        camera.SetBounds(Vector2.Zero, new Vector2(8192f, 8192f));
        Assert.Equal(4f * settings.MaxDistance, camera.MaxDistance, 3);
        camera.SetBounds(Vector2.Zero, new Vector2(100f, 100f));
        Assert.Equal(settings.MaxDistance, camera.MaxDistance);
    }

    [Fact]
    public void LaunchOptions_ChooseASize_AndTheVictories()
    {
        var options = LaunchOptions.Parse(["--world-size=huge", "--victory=conquest, cultural"]);

        Assert.Empty(options.Warnings);
        Assert.Equal(new VictorySettings { Ascendancy = false, Conquest = true, Cultural = true }, options.Victory);
        Assert.Equal(new NewWorldOptions(null, "Huge", options.Victory), options.NewWorld(TestContent.Shipped));
        Assert.Equal(VictorySettings.All, LaunchOptions.Parse(["--victory=all"]).Victory);
        Assert.Equal(VictorySettings.Sandbox, LaunchOptions.Parse(["--victory=Sandbox"]).Victory);
        Assert.Equal(VictorySettings.Default, LaunchOptions.Parse(["--victory=ascendancy"]).Victory);
    }

    [Fact]
    public void LaunchOptions_WarnAboutBadVictories_AndIgnoreUnknownSizes()
    {
        var options = LaunchOptions.Parse(["--victory=glory", "--victory=", "--world-size=", "--species=Dragon", "--world-size=gigantic"]);

        Assert.Equal(3, options.Warnings.Count);
        Assert.Null(options.Victory);
        Assert.Equal(new NewWorldOptions(), options.NewWorld(TestContent.Shipped));
    }

    [Fact]
    public void TheStatusLine_NamesASizeOtherThanTheFirst()
    {
        var content = TestContent.Shipped;

        Assert.Equal("New world from seed 42", SessionCommands.Describe(content, new WorldSeed(42), new NewWorldOptions(Size: "Small")));
        Assert.Equal("New Large world from seed 42", SessionCommands.Describe(content, new WorldSeed(42), new NewWorldOptions(Size: "large")));
    }

    /// <summary>A size of the 256-sample test heightfield.</summary>
    private static WorldSizePreset Preset(string name, float spacing, int regions, int settlements, int gods) => new()
    {
        Name = name,
        HeightmapResolution = 256,
        VertexSpacing = spacing,
        RegionsPerSide = regions,
        Settlements = settlements,
        Gods = gods,
        Rivers = 7,
        MainRivers = 4,
        HydrologyCellSize = 8f,
        PantheonMaxGods = gods / 2,
    };
}
