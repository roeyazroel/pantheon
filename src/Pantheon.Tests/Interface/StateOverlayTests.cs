using System.Linq;
using Pantheon.Domain.Diplomacy;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.World;
using Pantheon.Game.Presentation;
using Pantheon.Simulation.Diplomacy;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Interface;

/// <summary>The population, disaster risk and political control overlays (spec §58, ADR-031).</summary>
public sealed class StateOverlayTests
{
    [Fact]
    public void TheOverlays_RunOnToPopulation_Risk_AndPolitics()
    {
        Assert.Equal(OverlayKind.Population, OverlayMap.Next(OverlayKind.Nature));
        Assert.Equal(OverlayKind.Risk, OverlayMap.Next(OverlayKind.Population));
        Assert.Equal(OverlayKind.Politics, OverlayMap.Next(OverlayKind.Risk));
        Assert.Null(OverlayMap.Next(OverlayKind.Politics));
    }

    [Fact]
    public void ThePopulationOverlay_ColoursTheLargestVillageGold_AndTheRoadWhite()
    {
        var world = PantheonWorlds.Create(0.8f);
        world.Settlements[0].Population = 400;

        var image = Build(world, OverlayKind.Population);

        Assert.StartsWith("Population overlay", image.Legend, System.StringComparison.Ordinal);
        Assert.Equal(new byte[] { 0xf2, 0xc1, 0x4e }, Pixel(image, world.Settlements[0].RegionId)[..3]);
        Assert.True(Pixel(image, world.Settlements[1].RegionId)[1] < 0xc1);
    }

    [Fact]
    public void Risk_IsHighestOnDryWoods_LowLandByTheWater_SteepWetSlopes_AndNearTheVolcano()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var (woods, safe) = (world.Regions.Cells[0], world.Regions.Cells[1]);
        (woods.Vegetation, woods.SoilMoisture, woods.Elevation) = (1f, 0f, 20f);
        (safe.Vegetation, safe.SoilMoisture, safe.Elevation) = (0.2f, 0.5f, 20f);
        world.Regions.Cells.Where(r => r.Id is not (0 or 1)).ToList().ForEach(r => r.Elevation = 20f);

        Assert.Equal(1f, StateOverlays.Risk(world, woods), 3);
        Assert.True(StateOverlays.Risk(world, safe) < 0.4f);
        world.Regions.Cells[24].Biome = Biome.Sea;
        Assert.Equal(0f, StateOverlays.Risk(world, world.Regions.Cells[24]));

        safe.Elevation = 2f;
        world.Regions.Cells[2].Biome = Biome.Sea;
        Assert.True(StateOverlays.Risk(world, safe) > 0.5f);
    }

    [Fact]
    public void ThePoliticsOverlay_GivesAlliesTheirLeadersColour()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f, 0.8f);
        DiplomacyModel.EnsureStance(world, 2, 3).Status = DiplomaticStatus.Alliance;
        world.Settlements[2].Population = 10;

        var image = Build(world, OverlayKind.Politics);
        var view = TestContent.DivineView;

        Assert.StartsWith("Politics overlay", image.Legend, System.StringComparison.Ordinal);
        Assert.Equal(Pixel(image, world.Settlements[1].RegionId)[..3], Pixel(image, world.Settlements[2].RegionId)[..3]);
        Assert.Equal(new[] { RgbColor.ToByte(view.GodColor(2).R), RgbColor.ToByte(view.GodColor(2).G), RgbColor.ToByte(view.GodColor(2).B) }, Pixel(image, world.Settlements[1].RegionId)[..3]);
    }

    /// <summary>The overlay for the local god 1.</summary>
    private static OverlayImage Build(WorldState world, OverlayKind kind) =>
        OverlayMap.Build(world, kind, 1, DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.DivineView);

    /// <summary>A region's pixel.</summary>
    private static byte[] Pixel(OverlayImage image, int regionId) => image.Rgba[(regionId * 4)..((regionId * 4) + 4)];
}
