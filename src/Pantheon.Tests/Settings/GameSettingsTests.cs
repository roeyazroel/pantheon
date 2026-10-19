using Pantheon.Game.Settings;
using Xunit;

namespace Pantheon.Tests.Settings;

/// <summary>The player's settings file (ADR-021): forgiving where content is strict.</summary>
public sealed class GameSettingsTests
{
    [Fact]
    public void Settings_RoundTrip_ThroughTheirFile()
    {
        var settings = new GameSettings { Fullscreen = true, VSync = false, Shadows = false, RenderScale = 0.75f, Citizens = 0.5f, MasterVolume = 0.3f, MusicVolume = 0f, EffectsVolume = 1f, AmbienceVolume = 0.2f };

        Assert.Equal(settings, GameSettings.Parse(settings.Serialize()));
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
        var settings = GameSettings.Parse("""{ "renderScale": 0.1, "citizens": 7, "masterVolume": -2, "musicVolume": "0.4", "bloom": true }""");

        Assert.Equal(GameSettings.MinRenderScale, settings.RenderScale);
        Assert.Equal(1f, settings.Citizens);
        Assert.Equal(0f, settings.MasterVolume);
        Assert.Equal(0.4f, settings.MusicVolume);
        Assert.True(settings.VSync);
    }
}
