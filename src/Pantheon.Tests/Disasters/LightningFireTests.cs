using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.Miracles;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Disasters;

/// <summary>A god's lightning setting dry growth alight (spec §15, ADR-041).</summary>
public sealed class LightningFireTests
{
    [Fact]
    public void ABoltOnDryGrowth_SetsItAlight_ButNotOnWetLand()
    {
        var (dry, dryEvents) = Strike(soil: 0f);
        var (wet, wetEvents) = Strike(soil: 1f);

        Assert.True(dry.Fire > 0f);
        Assert.Equal(FireCause.Lightning, Assert.Single(dryEvents.OfType<WildfireStarted>()).Cause);
        Assert.Equal(0f, wet.Fire);
        Assert.Empty(wetEvents.OfType<WildfireStarted>());
    }

    /// <summary>Casts lightning on a lush region of the given soil water, away from any village.</summary>
    private static (RegionState Region, List<WorldEvent> Events) Strike(float soil)
    {
        var world = DivineWorlds.Create(10_000f, 300f);
        world.Gods[0].Rank = DivineRank.God;
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        var target = new GroundPosition(world.Settlements[0].Position.X + 40f, world.Settlements[0].Position.Z);
        world.Regions.TryGetRegionIdAt(target.X, target.Z, out var id);
        var region = world.Regions.Cells[id];
        (region.Vegetation, region.SoilMoisture) = (1f, soil);
        var events = new List<WorldEvent>();

        var cast = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.Shipped.Miracles, null, WeatherWorlds.Rules, TestContent.Shipped.Disasters)
            .Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "lightning", target), DivineWorlds.GodId, events);

        Assert.True(cast.IsAccepted, cast.Rejection.ToString());
        return (region, events);
    }
}
