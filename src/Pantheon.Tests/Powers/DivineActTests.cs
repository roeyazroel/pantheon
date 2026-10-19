using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Weather;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Disasters;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Powers;

/// <summary>The Divine Acts (spec §14, ADR-030): hallowing the land, waking the volcano and stilling every disaster.</summary>
public sealed class DivineActTests
{
    private static readonly MiracleCaster Caster = new(DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.Shipped.Miracles, null, WeatherWorlds.Rules, HazardWorlds.Rules);

    [Fact]
    public void HallowingTheLand_BlessesItForever()
    {
        var world = Mighty(300f);
        var village = world.Settlements[0];
        var events = new List<WorldEvent>();

        var check = Cast(world, "hallow", village.Position, events);

        Assert.True(check.IsAccepted, check.Rejection.ToString());
        var hallowed = world.Regions.Cells[village.RegionId];
        Assert.Equal((0.6f, 0.6f), (hallowed.Hallowed, hallowed.Blessing));
        Assert.Equal(DivineActKind.Hallowing, Assert.Single(events.OfType<DivineActDone>()).Act);

        var belief = new BeliefSystem(DivineWorlds.Rules, SettlementWorlds.Rules);
        for (var i = 0; i < 300; i++)
        {
            belief.Update(new TickContext(world, new SimulationRandom(1), []));
        }

        Assert.Equal(0.6f, hallowed.Blessing, 4);
        Assert.Equal(0f, world.Regions.Cells[0].Hallowed);
    }

    [Fact]
    public void WakingTheVolcano_NeedsASleepingOne_AndSetsItRumbling()
    {
        var flat = Mighty(300f);
        Assert.Equal(CastRejection.NothingThere, Cast(flat, "awaken", flat.Settlements[0].Position, []).Rejection);

        var world = Mighty(300f);
        HazardWorlds.Mountain(world, 100f);
        var events = new List<WorldEvent>();

        Assert.True(Cast(world, "awaken", world.Settlements[0].Position, events).IsAccepted);

        Assert.Equal(VolcanoPhase.Rumbling, world.Disasters.Volcano!.Phase);
        Assert.Contains(events, e => e is VolcanoChanged { Phase: VolcanoPhase.Rumbling });
        Assert.Equal(DivineActKind.Awakening, Assert.Single(events.OfType<DivineActDone>()).Act);
        Assert.Equal(CastRejection.OnCooldown, Cast(world, "awaken", world.Settlements[0].Position, []).Rejection);
    }

    [Fact]
    public void StillingTheWorld_EndsEveryDisasterAtOnce()
    {
        var world = Mighty(300f);
        HazardWorlds.Mountain(world, 100f);
        HazardModel.Wake(HazardWorlds.Hazards.Volcano, world);
        DisasterModel.StartQuake(HazardWorlds.Rules, world.Disasters, new GroundPosition(500f, 500f), 6f, 0, null);
        HazardModel.SpawnTornado(HazardWorlds.Hazards.Tornado, world, new GroundPosition(100f, 100f), 0f, 30f);
        HazardModel.SpawnSwarm(world, new GroundPosition(200f, 200f), 60f);
        (world.Regions.Cells[3].Fire, world.Regions.Cells[4].Flood, world.Regions.Cells[5].Blight) = (0.8f, 0.5f, 0.4f);
        WeatherWorlds.AddCell(world, new GroundPosition(700f, 700f), 200f, humidity: 0.7f, pressure: 1f);
        WeatherWorlds.AddCell(world, new GroundPosition(300f, 300f), 200f, humidity: 0.2f, pressure: 0.3f);
        world.Weather.Spell = WeatherSpell.HeatWave;
        var events = new List<WorldEvent>();

        Assert.True(Cast(world, "still", world.Settlements[0].Position, events).IsAccepted);

        var disasters = world.Disasters;
        Assert.Empty(disasters.Earthquakes);
        Assert.Empty(disasters.Tornadoes);
        Assert.Empty(disasters.Swarms);
        Assert.Equal(VolcanoPhase.Dormant, disasters.Volcano!.Phase);
        Assert.DoesNotContain(world.Regions.Cells, r => r.Fire > 0f || r.Flood > 0f || r.Blight > 0f);
        Assert.Single(world.Weather.Cells);
        Assert.Equal(WeatherSpell.Normal, world.Weather.Spell);
        Assert.Equal(DivineActKind.Stillness, Assert.Single(events.OfType<DivineActDone>()).Act);

        Assert.Equal(CastRejection.OnCooldown, Cast(world, "still", world.Settlements[0].Position, []).Rejection);
        var calm = Mighty(300f);
        Assert.Equal(CastRejection.NothingThere, Cast(calm, "still", calm.Settlements[0].Position, []).Rejection);
    }

    [Fact]
    public void DivineActs_NeedAGreaterGod_AndAGreatDealOfPower()
    {
        var world = Mighty(300f);
        world.Gods[0].Rank = DivineRank.God;
        Assert.Equal(CastRejection.RankTooLow, Cast(world, "hallow", world.Settlements[0].Position, []).Rejection);

        world.Gods[0].Rank = DivineRank.GreaterGod;
        world.Gods[0].DivinePower = 1000f;
        Assert.Equal(CastRejection.NotEnoughPower, Cast(world, "hallow", world.Settlements[0].Position, []).Rejection);
        Assert.All(TestContent.Shipped.Miracles.Miracles.Where(m => m.Scale == Pantheon.Domain.Miracles.MiracleScale.DivineAct), m => Assert.True(m.PowerCost >= 1500f));
    }

    /// <summary>A world whose god is a Greater God with power to spare, followed in its village.</summary>
    private static WorldState Mighty(float x)
    {
        var world = DivineWorlds.Create(10_000f, x);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        world.Gods[0].Rank = DivineRank.GreaterGod;
        return world;
    }

    /// <summary>The god casts a miracle.</summary>
    private static CastCheck Cast(WorldState world, string miracleId, GroundPosition target, List<WorldEvent> events) =>
        Caster.Cast(world, new CastMiracleRequest(DivineWorlds.GodId, miracleId, target), DivineWorlds.GodId, events);
}
