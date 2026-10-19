using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Game.Alerts;
using Pantheon.Game.Weather;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Weather;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Nature;

/// <summary>A hurricane announced as it forms, before landfall (spec §59's example, ADR-041).</summary>
public sealed class HurricaneWarningTests
{
    [Fact]
    public void AHurricane_IsAnnouncedAsItForms_AtTheEdgeOfTheWorld()
    {
        var rules = NatureWorlds.Rules with
        {
            CellsPerSecond = 5f,
            Extremes = NatureWorlds.Rules.Extremes with { HurricaneSeaTemperature = 0f },
            Seasons = NatureWorlds.Rules.Seasons with { Steady = NatureWorlds.Rules.Seasons.Steady with { HurricaneShare = 1f } },
        };
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        world.Weather.SpellEndsTick = 1_000_000;
        var system = new WeatherSystem(rules, SettlementWorlds.Rules);
        var events = new List<WorldEvent>();

        for (var step = 0; step < 2000 && !events.OfType<HurricaneApproaching>().Any(); step++)
        {
            world.Tick += rules.StepIntervalTicks;
            system.Update(new TickContext(world, new SimulationRandom(1), events));
        }

        var warning = Assert.Single(events.OfType<HurricaneApproaching>().Take(1));
        var cell = world.Weather.Cells.Single(c => c.Id == warning.CellId);
        Assert.Equal(WeatherKind.Hurricane, WeatherModel.Classify(rules, cell));
    }

    [Fact]
    public void TheWarning_NamesTheCoastItComesTowards()
    {
        var world = SettlementWorlds.Create(100, 200, 100f, 0.5f);
        var side = world.Regions.Columns * world.Regions.RegionSize;

        var alert = Assert.Single(new AlertFeed().Push([new HurricaneApproaching { Tick = 1, CellId = 7, X = -100f, Z = side / 2f }], world, 1, 1.0));

        Assert.Equal("A hurricane is approaching the western coast: its winds tear down houses and ruin stores.", alert.Text);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal("the north-eastern coast", WeatherText.Coast(world.Regions, side + 50f, -50f));
        Assert.Equal("the southern coast", WeatherText.Coast(world.Regions, side / 2f, side + 80f));
    }
}
