using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Disasters;
using Pantheon.Domain.Events;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Settlements;
using Pantheon.Domain.Society;
using Pantheon.Domain.Villagers;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.Villagers;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Villagers;

/// <summary>The villager a god follows, simulated in full (spec §80's selected villager, ADR-043).</summary>
public sealed class VillagerTests
{
    private const int Interval = 10;

    /// <summary>The shipped rules, with no deaths in danger.</summary>
    private static readonly VillagerRules Rules = TestContent.Shipped.SettlementRules.Villagers with { Mortality = 0f };

    private static readonly SettlementRules Settlements = SettlementWorlds.Rules with { Villagers = Rules };

    private static readonly VillagerAuthority Authority = new(Rules);

    [Fact]
    public void FollowingAFigure_MakesAVillagerOfTheWorld_NamedAgedAndBelievingAsTheirVillageDoes_TheSamePersonEachTime()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 1f, faith: 0.7f);
        var events = new List<WorldEvent>();

        Assert.Equal(VillagerRejection.None, Authority.Follow(world, Figure(village, slot: 3), events));

        var villager = Assert.Single(world.Villagers);
        Assert.Contains(villager.Name, Rules.Names);
        Assert.InRange(villager.AgeYears, Rules.MinAge, Rules.MaxAge);
        Assert.True(villager.LifeYears > villager.AgeYears);
        Assert.Equal((DivineWorlds.GodId, DivineWorlds.GodId, village.Id, 3, VillagerWork.Farming), (villager.FollowerGodId, villager.GodId!.Value, villager.SettlementId, villager.Slot!.Value, villager.Work));
        Assert.InRange(villager.Faith, 0.6f, 0.8f);
        Assert.Equal(VillagerDeed.Followed, Assert.Single(villager.Memories).Deed);
        Assert.Equal(villager.Name, Assert.Single(events.OfType<VillagerFollowed>()).Name);

        Authority.Follow(world, Figure(village, slot: 3), events);
        Assert.Equal((villager.Name, villager.AgeYears), (world.Villagers[0].Name, world.Villagers[0].AgeYears));
        Assert.Equal(2, world.Villagers[0].Id);
        Assert.Single(world.Villagers);
    }

    [Fact]
    public void NoVillagerIsFollowed_FromAnEmptyVillage_OrWithPlacesFarFromIt_OrWithoutNames_AndOneIsLetGoOnce()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        var (village, empty) = (world.Settlements[0], world.Settlements[1]);
        empty.Population = 0;

        Assert.Equal(VillagerRejection.TooFar, Authority.Follow(world, Figure(village) with { WorkPlace = new GroundPosition(800f, 512f) }, []));
        Assert.Equal(VillagerRejection.UnknownSettlement, Authority.Follow(world, Figure(empty), []));
        Assert.Equal(VillagerRejection.UnknownGod, Authority.Follow(world, Figure(village) with { GodId = 9 }, []));
        Assert.Equal(VillagerRejection.NoVillagers, new VillagerAuthority(VillagerRules.None).Follow(world, Figure(village), []));
        Assert.Empty(world.Villagers);

        Authority.Follow(world, Figure(village), []);
        Assert.Equal(VillagerRejection.None, Authority.Release(world, DivineWorlds.GodId));
        Assert.Equal(VillagerRejection.NotFollowing, Authority.Release(world, DivineWorlds.GodId));
    }

    [Fact]
    public void AVillager_Works_Eats_Rests_AndPrays_AsTheirNeedsAndFaithMoveThem_WalkingBetweenTheirPlaces()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        var village = world.Settlements[0];
        DivineWorlds.Believe(village, adherence: 1f, faith: 0.8f);
        Authority.Follow(world, Figure(village), []);
        var villager = world.Villagers[0];
        var seen = new HashSet<VillagerActivity>();
        var system = new VillagerSystem(Settlements, Interval);

        for (var i = 0; i < 900; i++)
        {
            Step(system, world, 1);
            if (villager.Path.Count == 0)
            {
                seen.Add(villager.Activity);
                Assert.Equal(VillagerMind.Place(villager, villager.Activity), villager.Position);
            }

            Assert.True(villager.Hunger < 0.9f && villager.Health > 0.9f);
        }

        Assert.Equal([VillagerActivity.Resting, VillagerActivity.Eating, VillagerActivity.Working, VillagerActivity.Worshipping], seen.Order());
        Assert.InRange(villager.Faith, 0.75f, 1f);
    }

    [Fact]
    public void WithNoFoodInStore_AVillagerGoesHungry_RemembersIt_AndStarvesToDeath()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        var village = world.Settlements[0];
        village.Food = 0f;
        Authority.Follow(world, Figure(village), []);
        var villager = world.Villagers[0];
        villager.Hunger = 0.95f;
        var system = new VillagerSystem(Settlements, Interval);

        var events = Step(system, world, 60);
        Assert.Contains(villager.Memories, m => m.Deed == VillagerDeed.Hungered);
        Assert.True(villager.Health < 1f);
        Assert.Equal(0f, VillagerMind.Options(Rules, world, villager).Single(o => o.Activity == VillagerActivity.Eating).Score);
        events.AddRange(Step(system, world, 300));

        var died = Assert.Single(events.OfType<VillagerDied>());
        Assert.Equal((VillagerFate.Hunger, village.Id), (died.Fate, died.SettlementId));
        Assert.Empty(world.Villagers);
        Assert.Equal(100, village.Population);
    }

    [Fact]
    public void AVillager_FleesAFire_ToASafeVillage_SheltersThere_AndComesHomeWhenItIsOut_WaryEverAfter()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        var (home, refuge) = (world.Settlements[0], world.Settlements[1]);
        Authority.Follow(world, Figure(home), []);
        var villager = world.Villagers[0];
        var system = new VillagerSystem(Settlements, Interval);
        world.Regions.Cells[home.RegionId].Fire = 1f;

        var fled = Assert.Single(Step(system, world, 1).OfType<VillagerFled>());
        Assert.Equal((VillagerFate.Fire, refuge.Id), (fled.Cause, fled.RefugeId!.Value));
        Step(system, world, 250);
        Assert.Equal(VillagerActivity.Sheltering, villager.Activity);
        Assert.True(Distance(villager.Position, refuge.Position) < 1f);
        Step(system, world, 300);
        Assert.Equal(VillagerActivity.Sheltering, villager.Activity);
        Assert.True(villager.Hunger < 0.5f && villager.Health > 0.9f);

        world.Regions.Cells[home.RegionId].Fire = 0f;
        Step(system, world, 1);
        Assert.Equal(VillagerDeed.Returned, villager.Memories[^1].Deed);
        Step(system, world, 400);

        Assert.Equal(home.Id, villager.SettlementId);
        Assert.True(Distance(villager.Position, home.Position) < 40f);
        Assert.True(VillagerModel.Wary(villager));
        Assert.Equal(Rules.FleeDanger * Rules.WaryFlee, VillagerModel.FleeAt(Rules, villager));
    }

    [Fact]
    public void AVillagerWhoRemembersDanger_FleesItSooner()
    {
        var fled = new List<bool>();
        foreach (var wary in new[] { false, true })
        {
            var world = DivineWorlds.Create(0f, 300f, 700f);
            var home = world.Settlements[0];
            Authority.Follow(world, Figure(home), []);
            if (wary)
            {
                world.Villagers[0].Memories.Add(new VillagerMemory { Tick = 0, Deed = VillagerDeed.Survived, SettlementId = home.Id, Detail = nameof(DisasterKind.Fire) });
            }

            world.Regions.Cells[home.RegionId].Fire = 0.2f;
            fled.Add(Step(new VillagerSystem(Settlements, Interval), world, 1).OfType<VillagerFled>().Any());
        }

        Assert.Equal([false, true], fled);
    }

    [Fact]
    public void AgeTakesAVillager_AtTheEndOfTheirLife()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var home = world.Settlements[0];
        world.Villagers.Add(Villager(home, age: 70f, life: 70.5f, god: null, faith: 0f));
        var system = new VillagerSystem(Settlements, Interval);

        Assert.Empty(Step(system, world, 40).OfType<VillagerDied>());
        var died = Assert.Single(Step(system, world, 10).OfType<VillagerDied>());

        Assert.Equal((VillagerFate.OldAge, 70), (died.Fate, died.Age));
    }

    [Fact]
    public void AVillagerWhoseFaithFails_TurnsToTheGodTheirVillageFollows_WhileStrongFaithHoldsOutUntilItWanes()
    {
        foreach (var faith in new[] { 0.25f, 0.8f })
        {
            var world = DivineWorlds.Create(0f, 300f);
            world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
            var home = world.Settlements[0];
            DivineWorlds.Believe(home, adherence: 0.2f, faith: 0.1f);
            DivineWorlds.Believe(home, adherence: 0.7f, faith: 0.6f, godId: 2);
            world.Villagers.Add(Villager(home, age: 30f, life: 80f, god: DivineWorlds.GodId, faith: faith));
            var villager = world.Villagers[0];
            var system = new VillagerSystem(Settlements, Interval);

            var events = Step(system, world, 1);
            Assert.Equal(faith < Rules.ConvertFaith ? 2 : DivineWorlds.GodId, villager.GodId);
            events.AddRange(Step(system, world, 300));

            var turned = Assert.Single(events.OfType<VillagerTurned>());
            Assert.Equal((DivineWorlds.GodId, 2), (turned.FromGodId!.Value, turned.ToGodId));
            Assert.Equal((VillagerDeed.Turned, (int?)DivineWorlds.GodId), (villager.Memories[^1].Deed, villager.Memories[^1].OtherId));
        }
    }

    [Fact]
    public void AVillagerWhoseVillageIsLost_SeeksANewHome_InAVillageOfTheirGod()
    {
        var world = DivineWorlds.Create(0f, 300f, 560f, 700f);
        var (home, other, theirs) = (world.Settlements[0], world.Settlements[1], world.Settlements[2]);
        DivineWorlds.Believe(theirs, adherence: 0.8f, faith: 0.5f);
        world.Villagers.Add(Villager(home, age: 30f, life: 80f, god: DivineWorlds.GodId, faith: 0.6f));
        var villager = world.Villagers[0];
        home.Population = 0;
        var system = new VillagerSystem(Settlements, Interval);

        Step(system, world, 1);
        Assert.Equal((VillagerActivity.Seeking, theirs.Id), (villager.Activity, villager.RefugeId!.Value));
        Step(system, world, 400);

        Assert.Equal((theirs.Id, (int?)null), (villager.SettlementId, villager.Slot));
        Assert.Contains(villager.Memories, m => m.Deed == VillagerDeed.Moved && m.SettlementId == theirs.Id);
        Assert.InRange(Distance(villager.Home, theirs.Position), 8f, 18f);
        Assert.NotEqual(other.Id, villager.SettlementId);
    }

    [Fact]
    public void AVillager_WitnessesMiraclesNearThem_AndWhatBefallsTheirVillage_AndTheirFaithIsMovedBySigns()
    {
        var world = DivineWorlds.Create(0f, 300f, 700f);
        world.Gods.Add(DivineWorlds.God(2, "Rivalis", GodController.Ai, GodPersonality.Tyrant, 0f));
        var home = world.Settlements[0];
        world.Villagers.Add(Villager(home, age: 30f, life: 80f, god: DivineWorlds.GodId, faith: 0.5f));
        var villager = world.Villagers[0];
        var system = new VillagerSystem(Settlements, Interval);
        List<WorldEvent> events =
        [
            new MiracleCast { Tick = 0, GodId = DivineWorlds.GodId, MiracleId = "rain", Target = home.Position, Cost = 1f, AffectedSettlementIds = [home.Id], Deaths = 0 },
            new MiracleCast { Tick = 0, GodId = 2, MiracleId = "lightning", Target = world.Settlements[1].Position, Cost = 1f, AffectedSettlementIds = [world.Settlements[1].Id], Deaths = 3 },
            new SettlementDamaged { Tick = 0, SettlementId = home.Id, Cause = DisasterKind.Earthquake, HousesLost = 2, Deaths = 4 },
            new SettlementDamaged { Tick = 0, SettlementId = home.Id, Cause = DisasterKind.Earthquake, HousesLost = 1, Deaths = 1 },
            new NotablePreached { Tick = 0, NotableId = 5, GodId = 2, SettlementId = home.Id, Converted = 6 },
            new BuildingCompleted { Tick = 0, SettlementId = home.Id, Kind = BuildingKind.Temple, GodId = DivineWorlds.GodId },
        ];

        system.Witness(world, events, 0);

        Assert.Equal(
            [VillagerDeed.SawMiracle, VillagerDeed.Survived, VillagerDeed.HeardPreaching, VillagerDeed.Built],
            villager.Memories.Select(m => m.Deed));
        Assert.Equal(("rain", 5), (villager.Memories[0].Detail, villager.Memories[1].Count));
        Assert.Equal(0.5f + Rules.WitnessFaith - (0.5f * Rules.WitnessFaith), villager.Faith, 4);
        Assert.Contains("hardship remembered", VillagerMind.Options(Rules, world, villager).Single(o => o.Activity == VillagerActivity.Worshipping).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void HardshipRemembered_DrivesAVillagerToPray_ButNotAgainSoonAfterPraying()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var home = world.Settlements[0];
        world.Villagers.Add(Villager(home, age: 30f, life: 80f, god: DivineWorlds.GodId, faith: 0.5f));
        var villager = world.Villagers[0];
        world.Tick = 5000;
        float Prayer() => VillagerMind.Options(Rules, world, villager).Single(o => o.Activity == VillagerActivity.Worshipping).Score;

        var calm = Prayer();
        villager.Memories.Add(new VillagerMemory { Tick = 4900, Deed = VillagerDeed.Fled, SettlementId = home.Id, Detail = nameof(VillagerFate.Fire) });
        var shaken = Prayer();
        villager.LastWorshipTick = 4990;

        Assert.Equal(Rules.WorshipDrive * 0.5f, calm, 4);
        Assert.Equal(calm + Rules.HardshipWorship, shaken, 4);
        Assert.True(Prayer() < Rules.HardshipWorship + 0.01f);
    }

    /// <summary>The figure at a village's slot, with its places about the village.</summary>
    private static FollowVillager Figure(SettlementState village, int slot = 0)
    {
        var at = village.Position;
        return new FollowVillager(DivineWorlds.GodId, village.Id, slot, VillagerWork.Farming, Near(at, 10f, 0f), Near(at, 30f, 0f), Near(at, 0f, 4f), Near(at, 10f, 0f));
    }

    /// <summary>A villager at home in a village, made directly.</summary>
    private static VillagerState Villager(SettlementState home, float age, float life, int? god, float faith) => new()
    {
        Id = 1, Name = "Mara", FollowerGodId = DivineWorlds.GodId, SettlementId = home.Id, Slot = 0, Work = VillagerWork.Farming, GodId = god, Faith = faith,
        Home = Near(home.Position, 10f, 0f), WorkPlace = Near(home.Position, 30f, 0f), Shrine = Near(home.Position, 0f, 4f), Position = Near(home.Position, 10f, 0f),
        AgeYears = age, LifeYears = life, FollowedTick = 0, Hunger = 0.2f, Fatigue = 0.2f,
    };

    /// <summary>A point beside another.</summary>
    private static GroundPosition Near(GroundPosition point, float x, float z) => new(point.X + x, point.Z + z);

    /// <summary>Metres between two points.</summary>
    private static float Distance(GroundPosition a, GroundPosition b) => MathF.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z)));

    /// <summary>Runs the system for steps of a second, collecting the events.</summary>
    private static List<WorldEvent> Step(VillagerSystem system, WorldState world, int steps)
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
