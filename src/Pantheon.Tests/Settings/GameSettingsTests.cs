using Pantheon.Game.Settings;
using Xunit;

namespace Pantheon.Tests.Settings;

/// <summary>The player's settings file (ADR-021): forgiving where content is strict.</summary>
public sealed class GameSettingsTests
{
    [Fact]
    public void Settings_RoundTrip_ThroughTheirFile()
    {
        var settings = new GameSettings
        {
            Fullscreen = true, VSync = false, Shadows = false, LightingEffects = false, RenderScale = 0.75f, Citizens = 0.5f, Vegetation = 0.5f, Particles = 0.25f,
            TerrainQuality = 0.75f, SimulationBudget = 10f, MasterVolume = 0.3f, MusicVolume = 0f, EffectsVolume = 1f, AmbienceVolume = 0.2f,
        };

        Assert.Equal(settings, GameSettings.Parse(settings.Serialize()));
        Assert.True(new GameSettings().LightingEffects, "the lighting effects are on unless turned off");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    public void AMissingOrBrokenFile_GivesTheDefaults(string? json)
    {
        Assert.Equal(new GameSettings(), GameSettings.Parse(json));
    }

    [Fact]
    public void ValuesOutOfRange_AreBroughtIntoRange_AndUnknownMembersIgnored()
    {
        var settings = GameSettings.Parse("""{ "renderScale": 0.1, "citizens": 7, "masterVolume": -2, "musicVolume": "0.4", "bloom": true, "vegetation": 0, "particles": 3, "terrainQuality": 0.1, "simulationBudget": 100 }""");

        Assert.Equal(GameSettings.MinRenderScale, settings.RenderScale);
        Assert.Equal(GameSettings.MinVegetation, settings.Vegetation);
        Assert.Equal(1f, settings.Particles);
        Assert.Equal(GameSettings.MinTerrainQuality, settings.TerrainQuality);
        Assert.Equal(GameSettings.MaxSimulationBudget, settings.SimulationBudget);
        Assert.Equal(GameSettings.DefaultSimulationBudget, new GameSettings().SimulationBudget);
        Assert.Equal(1f, settings.Citizens);
        Assert.Equal(0f, settings.MasterVolume);
        Assert.Equal(0.4f, settings.MusicVolume);
        Assert.True(settings.VSync);
    }

    [Fact]
    public void TheGraphicsSettings_ScaleTheCitizens_TheVegetation_AndTheTerrainsDetail()
    {
        var shipped = Pantheon.Tests.Support.TestContent.ShippedPresentation;
        var view = shipped.SettlementView;

        var full = GraphicsScaling.Apply(shipped, new GameSettings()).SettlementView;
        var least = GraphicsScaling.Apply(shipped, new GameSettings { Citizens = 0.5f, Vegetation = 0.25f, TerrainQuality = 0.5f }).SettlementView;

        Assert.Equal(view.Citizens, full.Citizens);
        Assert.Equal(view.Streaming, full.Streaming);
        Assert.Equal(view.GroundCover.Radius, full.GroundCover.Radius);
        Assert.Equal((int)System.MathF.Round(view.Citizens.MaxRepresentatives * 0.5f), least.Citizens.MaxRepresentatives);
        Assert.Equal(view.GroundCover.Radius * 0.25f, least.GroundCover.Radius, 3);
        Assert.Equal(view.Streaming.ForestLoadRadius * 0.625f, least.Streaming.ForestLoadRadius, 2);
        Assert.Equal(view.Streaming.TerrainDetailRadius * 0.5f, least.Streaming.TerrainDetailRadius, 2);
        least.Streaming.Validate();
        Assert.Equal(48, GraphicsScaling.TerrainMeshSize(1f));
        Assert.Equal(32, GraphicsScaling.TerrainMeshSize(0.7f));
        Assert.Equal(24, GraphicsScaling.TerrainMeshSize(0.5f));
    }
}
