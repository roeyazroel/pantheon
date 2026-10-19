using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Creatures;
using Pantheon.Domain.Ecology;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Gods;
using Pantheon.Domain.World;
using Pantheon.Simulation.GreatEvents;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.GreatEvents;

/// <summary>The ancient beast and the magical anomaly of spec §52 (ADR-035).</summary>
public sealed class BeastTests
{
    private static readonly GreatEventRules Rules = TestContent.Shipped.Disasters.GreatEvents;
    private static readonly BeastRules Beast = Rules.Beast;

    [Fact]
    public void TheBeast_SleepsFarFromTheVillages_OnTheHighestLand_NamedByTheSeed()
    {
        var world = Mountain(DivineWorlds.Create(500f, 200f, 300f));

        var beast = BeastModel.Find(Beast, world)!;

        Assert.Equal(Peak(world), beast.LairRegionId);
        Assert.Equal((BeastPhase.Sleeping, Beast.Health, Beast.Health), (beast.Phase, beast.Health, beast.MaxHealth));
        Assert.Contains(beast.Name, Beast.Names);
        Assert.Same(beast, BeastModel.Find(Beast, world));
        Assert.Equal(beast.Name, BeastModel.Find(Beast, Mountain(DivineWorlds.Create(500f, 200f, 300f)))!.Name);
    }

    [Fact]
    public void ItWakes_WhenAGreatQuakeShakesItsLair_AfterItsFirstSleep_ButNotFromASmallOne()
    {
        var world = Mountain(DivineWorlds.Create(500f, 200f, 300f));
        var lair = world.Regions.GetRegionCenter(Peak(world));
        world.Disasters.Earthquakes.Add(Quake(lair, Beast.WakeMagnitude));
        Step(world, 1);
        Assert.Equal(BeastPhase.Sleeping, world.GreatEvents.Beast!.Phase);

        world.Tick = (long)(Beast.FirstSleepSeconds * 10);
        world.Disasters.Earthquakes.Clear();
        world.Disasters.Earthquakes.Add(Quake(lair, Beast.WakeMagnitude - 1f));
        Step(world, 1);
        Assert.Equal(BeastPhase.Sleeping, world.GreatEvents.Beast!.Phase);

        world.Disasters.Earthquakes.Add(Quake(lair, Beast.WakeMagnitude));
        var events = Step(world, 1);

        Assert.Equal(BeastPhase.Awake, world.GreatEvents.Beast.Phase);
        var began = Assert.Single(events.OfType<GreatEventBegan>());
        Assert.Equal((GreatEventKind.Beast, world.GreatEvents.Beast.Name), (began.Kind, began.Name));
    }

    [Fact]
    public void Awake_ItWalksToTheNearestVillage_RavagesIt_AndMovesOn()
    {
        var world = Mountain(DivineWorlds.Create(500f, 200f, 300f));
        var (near, far) = (world.Settlements[1], world.Settlements[0]);
        Wake(world);
        var (people, housing) = (near.Population, near.Housing);

        Step(world, 1);
        Assert.Equal(near.Id, world.GreatEvents.Beast!.TargetSettlementId);

        var events = Step(world, 600);

        Assert.True(near.Population < people || near.Housing < housing);
        var blows = events.OfType<SettlementDamaged>().Where(e => e.Cause == Pantheon.Domain.Disasters.DisasterKind.Beast).ToList();
        Assert.Equal(near.Id, blows[0].SettlementId);
        Assert.Contains(events, e => e is SettlementDamaged { Cause: Pantheon.Domain.Disasters.DisasterKind.Beast } struck && struck.SettlementId == far.Id);
    }

    [Fact]
    public void CreaturesFightIt_HarmingIt_UntilBeatenTheyFly()
    {
        var (world, creature) = CreatureWorlds.Create(300f);
        Mountain(world);
        Wake(world);
        var beast = world.GreatEvents.Beast!;
        creature.Position = beast.Position;

        Step(world, 5);

        Assert.True(beast.HarmByGod[DivineWorlds.GodId] > 0f);
        Assert.True(creature.Health < 1f);
        creature.Position = beast.Position;
        creature.Health = Beast.FleeHealth + 0.01f;
        Step(world, 2);
        Assert.Equal(CreaturePhase.Banished, creature.Activity.Phase);
    }

    [Fact]
    public void ASmitingMiracle_HarmsIt_AndWhenItFalls_TheGodsShareTheGlory()
    {
        var world = Mountain(DivineWorlds.Create(10_000f, 200f, 300f));
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 500f));
        var mine = DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.5f);
        var theirs = DivineWorlds.Believe(world.Settlements[1], adherence: 0.9f, faith: 0.5f, godId: 2);
        world.Gods[0].Rank = DivineRank.God;
        Wake(world);
        var beast = world.GreatEvents.Beast!;
        beast.Position = world.Settlements[0].Position;

        var cast = new MiracleCaster(DivineWorlds.Rules, SettlementWorlds.Rules, TestContent.Shipped.Miracles, null, WeatherWorlds.Rules, TestContent.Shipped.Disasters)
            .Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "lightning", beast.Position), DivineWorlds.GodId, []);
        Assert.True(cast.IsAccepted, cast.Rejection.ToString());
        var mineHarm = beast.HarmByGod[DivineWorlds.GodId];
        Assert.True(mineHarm > 0f);

        BeastModel.Harm(beast, 2, mineHarm / 3f);
        beast.Health = 0f;
        var (faith, rivalFaith) = (mine.Faith, theirs.Faith);
        var events = Step(world, 1);

        var slain = Assert.Single(events.OfType<BeastSlain>());
        Assert.Equal([DivineWorlds.GodId, 2], slain.SlayerGodIds);
        Assert.Equal(BeastPhase.Slain, beast.Phase);
        Assert.True(mine.Faith - faith > theirs.Faith - rivalFaith);
        Assert.True(theirs.Faith > rivalFaith);
        Assert.Single(events.OfType<GreatEventEnded>());
    }

    [Fact]
    public void Tired_ItGoesHomeToSleep_Whole()
    {
        var world = Mountain(DivineWorlds.Create(500f, 200f, 300f));
        Wake(world);
        var beast = world.GreatEvents.Beast!;
        beast.Health = 50f;
        world.Tick += (long)(Beast.AwakeSeconds * 10);

        Step(world, 1);
        Assert.Equal(BeastPhase.Returning, beast.Phase);
        var events = Step(world, 400);

        Assert.Equal((BeastPhase.Sleeping, Beast.Health), (beast.Phase, beast.Health));
        Assert.Single(events.OfType<GreatEventEnded>());
    }

    [Fact]
    public void TheAnomaly_ComesWhenTheSeedSays_FeedsTheGodsByInfluence_AndCheapensMiracles()
    {
        var world = DivineWorlds.Create(500f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.9f, faith: 0.9f);
        var system = new GreatEventSystem(Rules, ResourceWorlds.Roads, DivineWorlds.Rules, 10);
        Run(system, world, 1);
        var first = world.GreatEvents.NextAnomalyTick;
        Assert.InRange(first - 10, (long)(Rules.Anomaly.FirstMinSeconds * 10), (long)(Rules.Anomaly.FirstMaxSeconds * 10));

        var surge = AnomalyModel.Surge(Rules, world, world.Settlements[0].Position, []);
        var power = world.Gods[0].DivinePower;
        Run(system, world, 10);

        Assert.True(world.Gods[0].DivinePower > power);
        Assert.Equal(Rules.Anomaly.CostFactor, AnomalyModel.CostFactor(Rules, world, world.Settlements[0].Position));
        Assert.Equal(1f, AnomalyModel.CostFactor(Rules, world, new GroundPosition(900f, 900f)));
        world.Tick = surge.EndTick!.Value;
        Run(system, world, 1);
        Assert.Null(GreatEventModel.Active(world, GreatEventKind.Anomaly));
    }

    /// <summary>Raises a mountain far from the villages, at the grid's far corner.</summary>
    private static WorldState Mountain(WorldState world)
    {
        var peak = world.Regions.Cells[^1];
        (peak.Biome, peak.Elevation) = (Biome.Highland, 120f);
        return world;
    }

    /// <summary>The mountain's region.</summary>
    private static int Peak(WorldState world) => world.Regions.Cells[^1].Id;

    /// <summary>Finds and wakes the beast.</summary>
    private static void Wake(WorldState world) => BeastModel.Wake(Rules, world, BeastModel.Find(Beast, world)!, []);

    /// <summary>An earthquake at a point.</summary>
    private static Pantheon.Domain.Disasters.Earthquake Quake(GroundPosition at, float magnitude) => new()
    {
        Id = 1, X = at.X, Z = at.Z, Magnitude = magnitude, Intensity = 1f, Radius = 300f, EndsAtTick = 100_000, CausedByGodId = null,
    };

    /// <summary>Runs one-second beast steps, gathering the events.</summary>
    private static List<WorldEvent> Step(WorldState world, int steps)
    {
        var system = new BeastSystem(TestContent.Shipped.Disasters, SettlementWorlds.Rules, DivineWorlds.Rules, CreatureWorlds.Rules, null, 10);
        var events = new List<WorldEvent>();
        for (var i = 0; i < steps; i++)
        {
            world.Tick += 10;
            system.Update(new TickContext(world, new SimulationRandom(1), events));
        }

        return events;
    }

    /// <summary>Runs one-second great event steps.</summary>
    private static void Run(GreatEventSystem system, WorldState world, int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            world.Tick += 10;
            system.Update(new TickContext(world, new SimulationRandom(1), []));
        }
    }
}
