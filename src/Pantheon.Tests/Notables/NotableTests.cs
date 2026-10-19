using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Belief;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Notables;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Simulation.Miracles;
using Pantheon.Simulation.Notables;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Notables;

/// <summary>Prophets and elders (spec §80's Tier 3, ADR-032) and the influence of temples (spec §10).</summary>
public sealed class NotableTests
{
    private const int Interval = 10;

    /// <summary>The shipped rules with no prophets called by chance and no deaths in danger.</summary>
    private static readonly NotableRules Quiet = TestContent.Shipped.DivineRules.Notables with { ProphetsPerHour = 0f, Mortality = 0f };

    [Fact]
    public void ASizeableVillage_ChoosesOneElder_OfTheGodItFollowsMost()
    {
        var world = DivineWorlds.Create(500f, 300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.3f);
        world.Settlements[1].Population = Quiet.ElderPopulation - 1;
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.9f, faith: 0.9f);

        var events = Step(new NotableSystem(Quiet, SettlementWorlds.Rules, Interval), world, 3);

        var elder = Assert.Single(world.Notables);
        Assert.Equal((NotableKind.Elder, DivineWorlds.GodId, world.Settlements[0].Id), (elder.Kind, elder.GodId, elder.SettlementId));
        Assert.Equal(NotableKind.Elder, Assert.Single(events.OfType<NotableRose>()).Kind);
        Assert.Contains(elder.Name, Quiet.Names);
        Assert.InRange(elder.DiesAtTick - elder.BornTick, (long)Quiet.ElderMinSeconds * 10, (long)Quiet.ElderMaxSeconds * 10);
    }

    [Fact]
    public void AnElder_KeepsTheVillagesFaith()
    {
        var world = DivineWorlds.Create(500f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.5f, faith: 0.3f, doubt: 0.3f);

        Step(new NotableSystem(Quiet, SettlementWorlds.Rules, Interval), world, 100);

        // Each change eases as a feeling nears its bound.
        var kept = System.MathF.Pow(1f - Quiet.ElderDevotionPerSecond, 100);
        Assert.Equal(1f - (0.7f * kept), belief.Faith, 3);
        Assert.Equal(0.3f * kept, belief.Doubt, 3);
    }

    [Fact]
    public void AnElder_TurnsFromAGodTheVillageResents_ToTheNextItFollows()
    {
        var world = DivineWorlds.Create(500f, 300f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 500f));
        var village = world.Settlements[0];
        var resented = DivineWorlds.Believe(village, adherence: 0.5f, faith: 0.5f);
        resented.Resentment = Quiet.ElderTurnResentment + 0.1f;
        DivineWorlds.Believe(village, adherence: 0.3f, faith: 0.5f, godId: 2);

        var events = Step(new NotableSystem(Quiet, SettlementWorlds.Rules, Interval), world, 2);

        var turned = Assert.Single(events.OfType<ElderTurned>());
        Assert.Equal((DivineWorlds.GodId, 2, true), (turned.FromGodId, turned.ToGodId, turned.Resented));
        var elder = Assert.Single(world.Notables);
        Assert.Equal(2, elder.GodId);
        Assert.Equal(Quiet.TurnDoubt, resented.Doubt, 4);
        Assert.Equal(NotableDeed.Turned, elder.Memories[^1].Deed);
    }

    [Fact]
    public void AnElder_FollowsAVillageThatHasLeftTheirGod_WithoutLeavingDoubt()
    {
        var world = DivineWorlds.Create(500f, 300f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 500f));
        var village = world.Settlements[0];
        var left = DivineWorlds.Believe(village, adherence: 0.6f, faith: 0.5f);
        var rival = DivineWorlds.Believe(village, adherence: 0.3f, faith: 0.5f, godId: 2);
        var system = new NotableSystem(Quiet, SettlementWorlds.Rules, Interval);
        Step(system, world, 1);
        var elder = Assert.Single(world.Notables);

        (left.Adherence, rival.Adherence) = (0.35f, 0.6f);
        Assert.Empty(Step(system, world, 1).OfType<ElderTurned>());

        left.Adherence = 0.25f;
        var turned = Assert.Single(Step(system, world, 1).OfType<ElderTurned>());

        Assert.Equal((DivineWorlds.GodId, 2, false), (turned.FromGodId, turned.ToGodId, turned.Resented));
        Assert.Equal(2, elder.GodId);
        Assert.Equal(0f, left.Doubt);
    }

    [Fact]
    public void DevoutVillages_CallProphets_UpToEachGodsLimit()
    {
        var rules = Quiet with { ProphetsPerHour = 3_600_000f };
        var world = DivineWorlds.Create(500f, 300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.8f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: rules.Devotion - 0.1f);

        var events = Step(new NotableSystem(rules, SettlementWorlds.Rules, Interval), world, 4);

        var prophets = world.Notables.Where(n => n.Kind == NotableKind.Prophet).ToList();
        Assert.Equal(rules.MaxProphetsPerGod, prophets.Count);
        Assert.All(prophets, p => Assert.Equal(world.Settlements[0].Id, p.Memories[0].SettlementId));
        Assert.Equal(rules.MaxProphetsPerGod, events.OfType<NotableRose>().Count(e => e.Kind == NotableKind.Prophet && !e.Anointed));
        Assert.Null(NotableModel.DevoutTo(rules, world.Settlements[1]));
    }

    [Fact]
    public void AProphet_PreachesWhereTheyAre_ThenWalksToTheNextVillage_AndPreachesThere()
    {
        var rules = Quiet with { PreachSeconds = 2f, ProphetSpeed = 40f, PreachConversion = 0.1f };
        var world = DivineWorlds.Create(500f, 300f, 700f);
        var (home, next) = (world.Settlements[0], world.Settlements[1]);
        DivineWorlds.Believe(home, adherence: 0.8f, faith: 0.8f);
        var prophet = NotableModel.Raise(rules, world, NotableKind.Prophet, DivineWorlds.GodId, home, anointed: false, []);
        var system = new NotableSystem(rules, SettlementWorlds.Rules, Interval);

        var events = Step(system, world, 6);

        Assert.Equal(2, Assert.Single(events.OfType<NotablePreached>()).Converted);
        Assert.Equal(next.Id, prophet.TargetSettlementId);
        Assert.InRange(prophet.Position.X, 350f, 650f);

        events = Step(system, world, 14);

        var sermon = Assert.Single(events.OfType<NotablePreached>(), e => e.SettlementId == next.Id);
        Assert.Equal(10, sermon.Converted);
        Assert.Equal(0.1f, BeliefModel.Find(next, DivineWorlds.GodId)!.Adherence, 4);
        Assert.Contains(prophet.Memories, m => m.Deed == NotableDeed.Preached && m.SettlementId == next.Id && m.Count == 10);
    }

    [Fact]
    public void ASermon_WinsTheGodless_AndSomeOfEachRivalsFollowers()
    {
        var world = DivineWorlds.Create(500f, 300f);
        var village = world.Settlements[0];
        village.Population = 200;
        var rival = DivineWorlds.Believe(village, adherence: 0.5f, faith: 0.5f, godId: 2);

        var won = NotableModel.Preach(Quiet with { PreachConversion = 0.1f }, village, DivineWorlds.GodId);

        Assert.Equal(15, won);
        Assert.Equal(0.475f, rival.Adherence, 4);
        var mine = BeliefModel.Find(village, DivineWorlds.GodId)!;
        Assert.Equal((0.075f, Quiet.PreachFaith), (mine.Adherence, mine.Faith));
        Assert.Equal([DivineWorlds.GodId, 2], village.Beliefs.Select(b => b.GodId));
    }

    [Fact]
    public void AProphetWhoDiesInDanger_IsAMartyr_AndTheFaithfulNearbyHoldHarder()
    {
        var world = DivineWorlds.Create(500f, 300f, 700f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.5f, doubt: 0.3f);
        var prophet = NotableModel.Raise(Quiet, world, NotableKind.Prophet, DivineWorlds.GodId, world.Settlements[0], anointed: false, []);
        NotableModel.SetOut(world, prophet, world.Settlements[1]);
        prophet.Position = new GroundPosition(500f, 512f);
        world.Regions.TryGetRegionIdAt(500f, 512f, out var road);
        world.Regions.Cells[road].Fire = 1f;

        var events = Step(new NotableSystem(Quiet with { Mortality = 1000f }, SettlementWorlds.Rules, Interval), world, 1);

        var died = Assert.Single(events.OfType<NotableDied>());
        Assert.True(died.Martyred);
        Assert.Equal(prophet.Name, died.Name);
        Assert.DoesNotContain(world.Notables, n => n.Kind == NotableKind.Prophet);
        Assert.True(belief.Faith > 0.5f + (Quiet.ElderDevotionPerSecond * 2));
        Assert.True(belief.Doubt < 0.3f - (Quiet.ElderDevotionPerSecond * 2));
    }

    [Fact]
    public void ANotableWhoLivesThroughDanger_RemembersItOnceForEachPlace()
    {
        var world = DivineWorlds.Create(500f, 300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.5f);
        var prophet = NotableModel.Raise(Quiet, world, NotableKind.Prophet, DivineWorlds.GodId, world.Settlements[0], anointed: false, []);
        NotableModel.SetOut(world, prophet, world.Settlements[1]);
        prophet.Position = new GroundPosition(500f, 512f);
        world.Regions.TryGetRegionIdAt(500f, 512f, out var road);
        world.Regions.Cells[road].Fire = 1f;

        Step(new NotableSystem(Quiet with { ProphetSpeed = 0f }, SettlementWorlds.Rules, Interval), world, 5);

        var endured = Assert.Single(prophet.Memories, m => m.Deed == NotableDeed.Endured);
        Assert.Null(endured.SettlementId);
        Assert.Equal("lived through danger in the wilds", Pantheon.Game.Notables.NotableText.Memory(endured, world));
    }

    [Fact]
    public void NotablesDieOfAge_OrWithTheirVillage_WithoutMartyrdom()
    {
        var world = DivineWorlds.Create(500f, 300f, 700f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.5f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 0.8f, faith: 0.5f);
        var old = NotableModel.Raise(Quiet, world, NotableKind.Elder, DivineWorlds.GodId, world.Settlements[0], anointed: false, []);
        world.Tick = old.DiesAtTick - 5;
        NotableModel.Raise(Quiet, world, NotableKind.Elder, DivineWorlds.GodId, world.Settlements[1], anointed: false, []);
        world.Settlements[1].Population = 0;

        var system = new NotableSystem(Quiet, SettlementWorlds.Rules, Interval);
        var events = Step(system, world, 1);

        Assert.Equal(2, events.OfType<NotableDied>().Count(e => !e.Martyred));
        Assert.Empty(world.Notables);

        Step(system, world, 1);
        Assert.Equal(world.Settlements[0].Id, Assert.Single(world.Notables).SettlementId);
    }

    [Fact]
    public void Anointing_RaisesAProphet_InTheDevoutVillageBeneath_WithinTheGodsLimit()
    {
        var world = Anointing(adherence: 0.6f);
        var events = new List<WorldEvent>();

        Assert.True(Cast(world, events).IsAccepted);

        var prophet = Assert.Single(world.Notables);
        Assert.Equal((NotableKind.Prophet, world.Settlements[0].Id), (prophet.Kind, prophet.SettlementId));
        Assert.True(Assert.Single(events.OfType<NotableRose>()).Anointed);

        Assert.Equal(CastRejection.NothingThere, Cast(Anointing(adherence: 0.2f), []).Rejection);
        var full = Anointing(adherence: 0.6f);
        for (var i = 0; i < TestContent.Shipped.DivineRules.Notables.MaxProphetsPerGod; i++)
        {
            NotableModel.Raise(Quiet, full, NotableKind.Prophet, DivineWorlds.GodId, full.Settlements[0], anointed: false, []);
        }

        Assert.Equal(CastRejection.NothingThere, Cast(full, []).Rejection);
    }

    [Fact]
    public void TemplesAndProphets_CarryTheirGodsInfluence_BeyondItsFollowers()
    {
        var rules = DivineWorlds.Rules with { TempleInfluenceStrength = 0.4f, TempleInfluenceReach = 250f, Notables = Quiet };
        var world = DivineWorlds.Create(500f, 300f, 800f);
        var (village, far) = (world.Settlements[0], new GroundPosition(900f, 512f));
        Assert.Equal(0f, InfluenceField.Build(rules, world, DivineWorlds.GodId).At(village.Position));

        village.Society.Buildings.Add(new Building { Kind = BuildingKind.Temple, GodId = DivineWorlds.GodId });
        var temple = InfluenceField.Build(rules, world, DivineWorlds.GodId);
        Assert.Equal(0.4f, temple.At(village.Position), 3);
        Assert.Equal(0f, temple.At(far));

        NotableModel.Raise(rules.Notables, world, NotableKind.Prophet, DivineWorlds.GodId, world.Settlements[1], anointed: false, []);
        Assert.Equal(rules.Notables.ProphetInfluence, InfluenceField.Build(rules, world, DivineWorlds.GodId).At(world.Settlements[1].Position), 3);
        Assert.Equal(0f, InfluenceField.Build(rules with { TempleInfluenceStrength = 0f }, world, DivineWorlds.GodId).At(village.Position));
    }

    [Fact]
    public void TheSameWorld_RaisesTheSameNotables()
    {
        static string Run()
        {
            var rules = Quiet with { ProphetsPerHour = 600f, Mortality = 0.5f };
            var world = DivineWorlds.Create(500f, 200f, 500f, 800f);
            foreach (var village in world.Settlements)
            {
                DivineWorlds.Believe(village, adherence: 0.8f, faith: 0.8f);
            }

            world.Regions.Cells[world.Settlements[2].RegionId].Fire = 0.5f;
            Step(new NotableSystem(rules, SettlementWorlds.Rules, Interval), world, 400);
            return Pantheon.Persistence.WorldSaveSerializer.Serialize(world);
        }

        Assert.Equal(Run(), Run());
    }

    /// <summary>A world whose Lesser God is followed in its one village by the share given.</summary>
    private static WorldState Anointing(float adherence)
    {
        var world = DivineWorlds.Create(1000f, 300f);
        DivineWorlds.Believe(world.Settlements[0], adherence, faith: 0.9f);
        world.Gods[0].Rank = DivineRank.LesserGod;
        return world;
    }

    /// <summary>The god anoints at its village.</summary>
    private static CastCheck Cast(WorldState world, List<WorldEvent> events) =>
        new MiracleCaster(DivineWorlds.Rules with { Notables = TestContent.Shipped.DivineRules.Notables }, SettlementWorlds.Rules, TestContent.Shipped.Miracles, null, WeatherWorlds.Rules, HazardWorlds.Rules)
            .Cast(world, new CastMiracleRequest(DivineWorlds.GodId, "anoint", world.Settlements[0].Position), DivineWorlds.GodId, events);

    /// <summary>Runs the system for a number of one-second steps, gathering the events.</summary>
    private static List<WorldEvent> Step(NotableSystem system, WorldState world, int steps)
    {
        var events = new List<WorldEvent>();
        for (var i = 0; i < steps; i++)
        {
            world.Tick += Interval;
            system.Update(new TickContext(world, new SimulationRandom(1), events));
        }

        return events;
    }
}
