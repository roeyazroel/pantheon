using System;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Launch;
using Pantheon.Game.Presentation;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>How the day passes (ADR-079): the light as a function of the world's clock.</summary>
public sealed class DayNightTests
{
    private static readonly DayNightSettings Settings = TestContent.DayNight;

    [Fact]
    public void AWorld_BeginsAtItsStartHour_AndADayLatersIsTheSameHour()
    {
        var ticksInDay = (long)(Settings.DaySeconds * SimulationTime.TicksPerSecond);

        Assert.Equal(Settings.StartHour, DayNight.HourAt(Settings, 0), 3);
        Assert.Equal(Settings.StartHour, DayNight.HourAt(Settings, ticksInDay), 3);
        Assert.Equal((Settings.StartHour + 12f) % 24f, DayNight.HourAt(Settings, ticksInDay / 2), 3);
    }

    [Fact]
    public void TheClock_ComesFromTheTickAlone_SoSharedWorldsAgreeAndSavesNeedNothing()
    {
        Assert.Equal(DayNight.HourAt(Settings, 123456), DayNight.HourAt(Settings, 123456));
        Assert.Equal(DayNight.At(Settings, 7.5f), DayNight.At(Settings, 7.5f));
    }

    [Fact]
    public void ADebugShift_MovesTheHour_AndWrapsRoundTheDay()
    {
        Assert.Equal((Settings.StartHour + 6f) % 24f, DayNight.HourAt(Settings, 0, 6f), 3);
        Assert.InRange(DayNight.HourAt(Settings, 0, 30f), 0f, 24f);
    }

    [Fact]
    public void TheSun_RisesInTheEast_StandsHighAtNoon_AndSetsInTheWest()
    {
        var noon = (Settings.Sunrise + Settings.Sunset) / 2f;
        var morning = DayNight.At(Settings, Settings.Sunrise + 1f).ToLight;
        var high = DayNight.At(Settings, noon).ToLight;
        var evening = DayNight.At(Settings, Settings.Sunset - 1f).ToLight;

        Assert.True(morning.X > 0.5f, "the morning sun is in the east");
        Assert.True(evening.X < -0.5f, "the evening sun is in the west");
        Assert.True(high.Y > morning.Y && high.Y > evening.Y);
        Assert.Equal(1f, high.Length(), 3);
        Assert.Equal(MathF.Sin(Settings.HighestSun * MathF.PI / 180f), high.Y, 3);
    }

    [Fact]
    public void TheLight_NeverFallsToTheHorizon_SoShadowsStayOnTheLand()
    {
        var lowest = MathF.Sin(Settings.LowestLight * MathF.PI / 180f) * 0.98f;

        Assert.All(Enumerable.Range(0, 480), i => Assert.True(DayNight.At(Settings, i / 20f).ToLight.Y >= lowest));
    }

    [Fact]
    public void TheLight_IsDark_WhereItChangesFromMoonToSun_SoTheSwapDoesNotShow()
    {
        Assert.Equal(0f, DayNight.At(Settings, Settings.Sunrise).Energy, 3);
        Assert.Equal(0f, DayNight.At(Settings, Settings.Sunset).Energy, 3);
    }

    [Fact]
    public void Night_IsDarkerThanDay_ButNeverSoDarkThatTheWorldCannotBeRead()
    {
        var noon = DayNight.At(Settings, 13f);
        var midnight = DayNight.At(Settings, 0.5f);

        Assert.Equal(0f, noon.Night, 3);
        Assert.Equal(1f, midnight.Night, 3);
        Assert.True(midnight.Energy < noon.Energy && midnight.Ambient < noon.Ambient);
        Assert.True(midnight.Ambient >= 0.4f, "a god must still see the land at night");
    }

    [Fact]
    public void TheHours_BlendWithoutAJump_RoundTheWholeDay()
    {
        var hours = Enumerable.Range(0, 480).Select(i => DayNight.At(Settings, i / 20f)).ToList();

        Assert.All(hours.Zip(hours.Skip(1).Append(hours[0])), pair =>
        {
            Assert.InRange(MathF.Abs(pair.First.Ambient - pair.Second.Ambient), 0f, 0.05f);
            Assert.InRange(MathF.Abs(pair.First.Night - pair.Second.Night), 0f, 0.06f);
        });
    }

    [Theory]
    [InlineData(3f, DayPart.Night)]
    [InlineData(6.5f, DayPart.Dawn)]
    [InlineData(9f, DayPart.Morning)]
    [InlineData(13f, DayPart.Noon)]
    [InlineData(16f, DayPart.Afternoon)]
    [InlineData(19.5f, DayPart.Dusk)]
    [InlineData(23f, DayPart.Night)]
    public void AnHour_FallsInAPartOfTheDay(float hour, DayPart part)
    {
        Assert.Equal(part, DayNight.PartOf(Settings, hour));
    }

    [Fact]
    public void Lamps_GoOutAsTheVillageSleeps_AndHearthsBurnAtBreakfastAndSupper()
    {
        Assert.True(DayNight.At(Settings, 21f).Lamps > DayNight.At(Settings, 3f).Lamps, "most lamps are out by the small hours");
        Assert.Equal(0f, DayNight.At(Settings, 13f).Lamps, 3);
        Assert.True(DayNight.At(Settings, 7f).Hearths > DayNight.At(Settings, 13f).Hearths);
        Assert.True(DayNight.At(Settings, 18.5f).Hearths > DayNight.At(Settings, 13f).Hearths);
        Assert.True(DayNight.At(Settings, 6.5f).Mist > DayNight.At(Settings, 13f).Mist);
        Assert.True(DayNight.At(Settings, 6.5f).Birds > DayNight.At(Settings, 13f).Birds);
        Assert.Equal(0f, DayNight.At(Settings, 23f).Birds, 3);
    }

    [Fact]
    public void EveryPartOfTheDay_HasItsWord()
    {
        Assert.Equal(6, Enum.GetValues<DayPart>().Select(DayNight.WordOf).Distinct().Count());
    }

    [Fact]
    public void TheDays_KeysMustBeInOrder_AndBeginAtMidnight()
    {
        var text = TestContent.ReadText(DayNightSettings.FileName);

        Assert.Throws<ContentFormatException>(() => DayNightSettings.Parse(text.Replace("\"Hour\": 0.0,", "\"Hour\": 1.0,", StringComparison.Ordinal)));
        Assert.Throws<ContentFormatException>(() => DayNightSettings.Parse(text.Replace("\"Hour\": 13.0,", "\"Hour\": 2.0,", StringComparison.Ordinal)));
        Assert.Throws<ContentFormatException>(() => DayNightSettings.Parse(text.Replace("\"Sunset\": 20.0,", "\"Sunset\": 5.0,", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheHour_CanBeHeldForACapture_AndTheDebugPanelShowsIt()
    {
        Assert.Equal(21.5f, LaunchOptions.Parse(["--hour=21.5"]).Hour);
        Assert.Null(LaunchOptions.Parse(["--hour=30"]).Hour);
        Assert.Equal("Day: 21:30.", WeatherReadout.DescribeDay(21.5f));
    }
}
