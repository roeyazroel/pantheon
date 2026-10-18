using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Weather;

/// <summary>The rules of weather cells and soil water (spec §16, ADR-013) as pure functions.</summary>
public sealed class WeatherModelTests
{
    private static readonly WeatherRules Rules = WeatherWorlds.Rules;

    [Fact]
    public void Precipitation_NeedsWaterAboveTheThreshold_AndGrowsWithItAndTheLow()
    {
        Assert.Equal(0f, WeatherModel.ComputePrecipitation(Rules, Rules.RainHumidity, 1f));
        var shower = WeatherModel.ComputePrecipitation(Rules, 0.7f, 0.2f);
        var wetter = WeatherModel.ComputePrecipitation(Rules, 0.9f, 0.2f);
        var deeper = WeatherModel.ComputePrecipitation(Rules, 0.7f, 0.8f);

        Assert.InRange(shower, 0.01f, 0.99f);
        Assert.True(wetter > shower);
        Assert.True(deeper > shower);
        Assert.Equal(1f, WeatherModel.ComputePrecipitation(Rules, 1f, 1f));
    }

    [Fact]
    public void Classify_TellsStormsFromShowersAndClouds()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var thunder = WeatherWorlds.AddCell(world, new GroundPosition(100f, 100f), 200f, humidity: 1f, pressure: 0.95f);
        var windstorm = WeatherWorlds.AddCell(world, new GroundPosition(100f, 100f), 200f, humidity: 0.36f, pressure: 0.9f);
        var shower = WeatherWorlds.AddCell(world, new GroundPosition(100f, 100f), 200f, humidity: 0.8f, pressure: 0.2f);
        var cloud = WeatherWorlds.AddCell(world, new GroundPosition(100f, 100f), 200f, humidity: 0.3f, pressure: 0.2f);

        Assert.Equal(WeatherKind.Thunderstorm, WeatherModel.Classify(Rules, thunder));
        Assert.Equal(WeatherKind.Windstorm, WeatherModel.Classify(Rules, windstorm));
        Assert.Equal(WeatherKind.Rain, WeatherModel.Classify(Rules, shower));
        Assert.Equal(WeatherKind.Clear, WeatherModel.Classify(Rules, cloud));
        Assert.True(thunder.Wind >= Rules.StormWind);
        Assert.True(shower.Wind < Rules.StormWind);
    }

    [Fact]
    public void Coverage_IsFullInside_FadesOverTheEdge_AndEndsAtTheRadius()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var cell = WeatherWorlds.AddCell(world, new GroundPosition(500f, 500f), 200f, humidity: 0.8f, pressure: 0.3f);

        Assert.Equal(1f, WeatherModel.Coverage(Rules, cell, new GroundPosition(500f, 500f)));
        Assert.Equal(1f, WeatherModel.Coverage(Rules, cell, new GroundPosition(600f, 500f)));
        Assert.InRange(WeatherModel.Coverage(Rules, cell, new GroundPosition(670f, 500f)), 0.01f, 0.99f);
        Assert.Equal(0f, WeatherModel.Coverage(Rules, cell, new GroundPosition(701f, 500f)));
    }

    [Fact]
    public void At_AddsTheRainOfEveryCell_AndTakesTheStrongestWind()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var shower = WeatherWorlds.AddCell(world, new GroundPosition(500f, 500f), 200f, humidity: 0.7f, pressure: 0.2f);
        var storm = WeatherWorlds.AddCell(world, new GroundPosition(520f, 500f), 200f, humidity: 0.9f, pressure: 0.9f);

        var here = WeatherModel.At(Rules, world.Weather, new GroundPosition(510f, 500f));
        var nowhere = WeatherModel.At(Rules, world.Weather, new GroundPosition(100f, 100f));

        Assert.Equal(System.Math.Min(1f, shower.Precipitation + storm.Precipitation), here.Rain, 4);
        Assert.Equal(storm.Wind, here.Wind, 4);
        Assert.Equal(storm.Id, here.CellId);
        Assert.Equal(new PointWeather(0f, 0f, 0f, null), nowhere);
    }

    [Fact]
    public void Soil_RainWetsIt_WarmthDriesIt_AndADrySpellDriesItFaster()
    {
        var region = DivineWorlds.Create(0f, 300f).Regions.Cells[0];
        region.SoilMoisture = 0.4f;

        var rained = WeatherModel.NextSoilMoisture(Rules, region, rain: 0.8f, WeatherSpell.Normal, 60f);
        var dried = WeatherModel.NextSoilMoisture(Rules, region, rain: 0f, WeatherSpell.Normal, 60f);
        var parched = WeatherModel.NextSoilMoisture(Rules, region, rain: 0f, WeatherSpell.Dry, 60f);
        region.Temperature = 0.9f;
        var hot = WeatherModel.NextSoilMoisture(Rules, region, rain: 0f, WeatherSpell.Normal, 60f);

        Assert.True(rained > 0.4f);
        Assert.True(dried < 0.4f);
        Assert.True(parched < dried);
        Assert.True(hot < dried);
    }

    [Fact]
    public void Soil_WithoutRain_SettlesWhereSpringsMeetEvaporation()
    {
        var region = DivineWorlds.Create(0f, 300f).Regions.Cells[0];
        region.SoilMoisture = 0.6f;
        for (var i = 0; i < 2000; i++)
        {
            region.SoilMoisture = WeatherModel.NextSoilMoisture(Rules, region, rain: 0f, WeatherSpell.Normal, 1f);
        }

        // Springs give GroundwaterPerSecond x moisture x (1 - soil); evaporation takes EvaporationPerSecond x (0.5 + temperature) x soil.
        var springs = Rules.GroundwaterPerSecond * region.Moisture;
        var settled = springs / (springs + (Rules.EvaporationPerSecond * (0.5f + region.Temperature)));
        Assert.Equal(settled, region.SoilMoisture, 3);
    }

    [Fact]
    public void Spells_ChangeHowOftenWeatherComesAndHowStormyItIs()
    {
        Assert.True(WeatherModel.CellFactor(Rules, WeatherSpell.Dry) < WeatherModel.CellFactor(Rules, WeatherSpell.Normal));
        Assert.True(WeatherModel.CellFactor(Rules, WeatherSpell.Wet) > WeatherModel.CellFactor(Rules, WeatherSpell.Normal));
        Assert.True(WeatherModel.StormShare(Rules, WeatherSpell.Wet) > WeatherModel.StormShare(Rules, WeatherSpell.Normal));
        Assert.Equal((Rules.DrySpellMinSeconds, Rules.DrySpellMaxSeconds), WeatherModel.SpellSeconds(Rules, WeatherSpell.Dry));
    }

    [Fact]
    public void Summon_CallsAGentleRainCellForTheGod_ThatDriftsSlowlyDownwind()
    {
        var world = DivineWorlds.Create(0f, 300f);
        world.Weather.WindDirection = 0f;

        var cell = WeatherModel.Summon(Rules, world, new GroundPosition(300f, 512f), 220f, 1f, godId: DivineWorlds.GodId);

        Assert.Same(cell, Assert.Single(world.Weather.Cells));
        Assert.Equal(DivineWorlds.GodId, cell.SummonedByGodId);
        Assert.Equal((300f, 512f, 220f), (cell.X, cell.Z, cell.Radius));
        Assert.Equal(Rules.CellSpeed * Rules.SummonedSpeedFactor, cell.VelocityX, 4);
        Assert.Equal(WeatherKind.Rain, WeatherModel.Classify(Rules, cell));
        Assert.Equal(2, world.Weather.NextCellId);
    }
}
