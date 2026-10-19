using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Conflict;
using Pantheon.Domain.Events;
using Pantheon.Domain.GreatEvents;
using Pantheon.Domain.Gods;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.GreatEvents;
using Pantheon.Simulation.Politics;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.GreatEvents;

/// <summary>The great events of spec §52 (ADR-034): the plague, eclipses, reformations, and world wars, great droughts and great migrations.</summary>
public sealed class GreatEventTests
{
    private static readonly GreatEventRules Rules = TestContent.Shipped.Disasters.GreatEvents;
    private static readonly PlagueRules Plague = Rules.Plague;

    [Fact]
    public void AVillageIsRipeForPlague_WhenCrowded_Sick_AndNotImmune()
    {
        var world = DivineWorlds.Create(500f, 300f);
        var village = world.Settlements[0];
        (village.Population, village.Housing, village.Health) = (100, 120, 1f);
        Assert.Equal(0f, PlagueModel.Ripeness(Plague, world, village));

        (village.Population, village.Health) = (150, 0.5f);
        var ripe = PlagueModel.Ripeness(Plague, world, village);
        Assert.True(ripe > 0.5f);

        village.Immunity = 0.5f;
        Assert.Equal(ripe * 0.5f, PlagueModel.Ripeness(Plague, world, village), 4);
    }

    [Fact]
    public void ThePlague_Grows_ThenTheSickRecoverImmune_AndItEnds()
    {
        var world = DivineWorlds.Create(500f, 300f);
        var village = world.Settlements[0];
        village.Plague = PlagueModel.Onset;
        var (peak, immune) = (0f, 0f);
        var ended = false;

        for (var i = 0; i < 1200 && !ended; i++)
        {
            ended = PlagueModel.Advance(Plague, village, 1f);
            (peak, immune) = (System.Math.Max(peak, village.Plague), System.Math.Max(immune, village.Immunity));
        }

        Assert.True(ended);
        Assert.True(peak > 0.2f);
        Assert.True(immune > 0.5f);
        Assert.True(village.Immunity < immune);
        Assert.True(village.Health < 1f);
        Assert.Equal(0f, village.Plague);
    }

    [Fact]
    public void AHospital_AndMedicine_ShieldAVillage_AndHealingCuresIt()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        var (sick, healthy) = (world.Settlements[0], world.Settlements[1]);
        sick.Plague = 0.5f;
        var bare = PlagueModel.CatchPerSecond(Plague, sick, healthy, byRoad: false);
        Assert.Equal(Plague.ContactPerSecond * 0.5f, bare, 5);
        Assert.Equal(Plague.RoadPerSecond * 0.5f, PlagueModel.CatchPerSecond(Plague, sick, healthy, byRoad: true), 5);

        healthy.Society.Buildings.Add(new Building { Kind = BuildingKind.Hospital, GodId = null });
        healthy.Society.Technology.Medicine = 2f;
        Assert.Equal(Plague.HospitalShield + (2f * Plague.MedicinePerLevel), PlagueModel.Shield(Plague, healthy), 4);
        Assert.Equal(bare * (1f - PlagueModel.Shield(Plague, healthy)), PlagueModel.CatchPerSecond(Plague, sick, healthy, byRoad: false), 5);
        Assert.Equal(0f, PlagueModel.CatchPerSecond(Plague, sick, DivineWorlds.Create(500f, 300f, 1000f).Settlements[1], byRoad: false));

        PlagueModel.Cure(Plague, sick, 0.2f);
        Assert.Equal(0.5f * (1f - (Plague.HealCure * 0.2f)), sick.Plague, 4);
    }

    [Fact]
    public void ThePlagueSpreading_ToEnoughVillages_IsAGreatEvent_ThatTheCouncilAnswers()
    {
        var world = DivineWorlds.Create(500f, 200f, 450f, 700f, 950f);
        foreach (var village in world.Settlements.Take(Plague.EventVillages))
        {
            village.Plague = 0.3f;
        }

        var events = Step(world, 1);

        var began = Assert.Single(events.OfType<GreatEventBegan>());
        Assert.Equal(GreatEventKind.Plague, began.Kind);
        Assert.Equal(Plague.EventVillages, GreatEventModel.Active(world, GreatEventKind.Plague)!.Peak);
        Assert.True(CouncilModel.Struck(world, world.Settlements[0], world.Tick));

        foreach (var village in world.Settlements)
        {
            village.Plague = 0f;
        }

        Assert.Single(Step(world, 1).OfType<GreatEventEnded>());
        Assert.Null(GreatEventModel.Active(world, GreatEventKind.Plague));
    }

    [Fact]
    public void TheSunGoesDark_WhenTheSeedSays_FrighteningEveryone_AndAMiracleThenAwes()
    {
        var world = DivineWorlds.Create(500f, 300f);
        var belief = DivineWorlds.Believe(world.Settlements[0], adherence: 0.8f, faith: 0.5f);
        Step(world, 1);
        var first = world.GreatEvents.NextEclipseTick;
        Assert.InRange(first - 10, (long)(Rules.Eclipse.FirstMinSeconds * 10), (long)(Rules.Eclipse.FirstMaxSeconds * 10));
        Assert.Equal(1f, GreatEventModel.Awe(Rules, world));

        world.Tick = first;
        var events = Step(world, 1);
        Assert.Equal(GreatEventKind.Eclipse, Assert.Single(events.OfType<GreatEventBegan>()).Kind);
        Assert.True(belief.Fear > 0f);
        Assert.Equal(Rules.Eclipse.Awe, GreatEventModel.Awe(Rules, world));
        var next = world.GreatEvents.NextEclipseTick;
        Assert.InRange(next - first, (long)((Rules.Eclipse.Seconds + Rules.Eclipse.IntervalMinSeconds) * 10) - 10, (long)((Rules.Eclipse.Seconds + Rules.Eclipse.IntervalMaxSeconds) * 10) + 10);

        events = Step(world, (int)Rules.Eclipse.Seconds + 1);
        Assert.Single(events.OfType<GreatEventEnded>());
        Assert.Equal(1f, GreatEventModel.Awe(Rules, world));
    }

    [Fact]
    public void ADoubtingLearnedFaith_Reforms_ThenWaits()
    {
        var world = DivineWorlds.Create(500f, 200f, 450f, 700f, 950f);
        var beliefs = world.Settlements.Select(s => DivineWorlds.Believe(s, adherence: 0.8f, faith: 0.4f, doubt: 0.6f)).ToList();
        Assert.False(ReformationModel.Ripe(Rules.Reformation, world, DivineWorlds.GodId));

        world.Settlements.ForEach(s => s.Society.Education = 0.5f);
        Assert.True(ReformationModel.Ripe(Rules.Reformation, world, DivineWorlds.GodId));
        var events = Step(world, 1);

        Assert.Equal(GreatEventKind.Reformation, Assert.Single(events.OfType<GreatEventBegan>()).Kind);
        Assert.All(beliefs, b => Assert.Equal(0.6f * Rules.Reformation.DoubtKept, b.Doubt, 4));
        Assert.All(beliefs, b => Assert.Equal(0.8f - (Rules.Reformation.Leaving * 0.6f * 0.8f), b.Adherence, 4));
        Assert.All(beliefs, b => Assert.True(b.Faith > 0.4f));
        beliefs.ForEach(b => b.Doubt = 0.6f);
        Assert.False(ReformationModel.Ripe(Rules.Reformation, world, DivineWorlds.GodId));
    }

    [Fact]
    public void MostGodsAtWar_IsAWorldWar_InWhichEveryoneGrowsAfraid()
    {
        var world = PantheonWorlds.Create(0.8f, 0.8f, 0.8f, 0.8f);
        world.Diplomacy.Wars.Add(War(1, 2));
        Step(world, 1);
        Assert.Null(GreatEventModel.Active(world, GreatEventKind.WorldWar));

        world.Diplomacy.Wars.Add(War(3, 4));
        Step(world, 10);
        Assert.Equal(4, GreatEventModel.Active(world, GreatEventKind.WorldWar)!.Peak);
        Assert.All(world.Settlements.SelectMany(s => s.Beliefs), b => Assert.True(b.Fear > 0f));

        world.Diplomacy.Wars.Clear();
        Assert.Single(Step(world, 1).OfType<GreatEventEnded>());
    }

    [Fact]
    public void ALongDryLand_IsAGreatDrought_AndManyOnTheRoad_AGreatMigration()
    {
        var world = DivineWorlds.Create(500f, 300f);
        world.Regions.Cells.ForEach(r => r.SoilMoisture = Rules.DroughtSoil - 0.05f);
        Step(world, (int)Rules.DroughtSeconds - 2);
        Assert.Null(GreatEventModel.Active(world, GreatEventKind.GreatDrought));
        Step(world, 3);
        Assert.NotNull(GreatEventModel.Active(world, GreatEventKind.GreatDrought));

        world.Migration.Groups.Add(MigrationWorldsGroup(world, 40));
        Step(world, 1);
        Assert.Equal(40, GreatEventModel.Active(world, GreatEventKind.GreatMigration)!.Peak);
    }

    [Fact]
    public void AWorldWithThePlague_PlaysTheSameAcrossASave()
    {
        var engine = TestContent.Shipped.CreateEngine();
        var straight = Pantheon.Game.GameSession.CreateNew(TestContent.Shipped, new WorldSeed(5)).World;
        var resumed = Pantheon.Game.GameSession.CreateNew(TestContent.Shipped, new WorldSeed(5)).World;
        foreach (var world in new[] { straight, resumed })
        {
            world.Settlements[0].Plague = 0.3f;
            world.GreatEvents.NextEclipseTick = 500;
        }

        engine.Run(straight, 1600);
        engine.Run(resumed, 800);
        resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(resumed));
        engine.Run(resumed, 800);

        Assert.Contains(straight.History, r => r.Kind == Pantheon.Domain.History.HistoryKind.Eclipse);
        WorldAssert.Equal(straight, resumed);
        Assert.Equal(straight.Settlements.Select(s => (s.Plague, s.Immunity)), resumed.Settlements.Select(s => (s.Plague, s.Immunity)));
    }

    /// <summary>A war between two gods.</summary>
    private static WarRecord War(int aggressor, int defender) => new()
    {
        AggressorGodId = aggressor, DefenderGodId = defender, Goal = WarGoal.Conquest, SiteId = null, StartedTick = 0, AggressorScore = 0f, DefenderScore = 0f, GoalAnnounced = false,
    };

    /// <summary>A group of people on the road from the first village.</summary>
    private static Pantheon.Domain.Migration.MigrationGroup MigrationWorldsGroup(WorldState world, int people) => new()
    {
        Id = world.Migration.NextGroupId++,
        OriginSettlementId = world.Settlements[0].Id,
        DestinationSettlementId = world.Settlements[0].Id,
        Population = people,
        Food = 10f,
        Health = 1f,
        Position = world.Settlements[0].Position,
        Path = [],
        Beliefs = [],
        Cause = Pantheon.Domain.Migration.MigrationCause.Famine,
        DepartedTick = world.Tick,
        TurnedAwayFrom = [],
    };

    /// <summary>Runs one-second steps of the great events, gathering the events.</summary>
    private static List<WorldEvent> Step(WorldState world, int steps)
    {
        var system = new GreatEventSystem(Rules, ResourceWorlds.Roads, 10);
        var events = new List<WorldEvent>();
        for (var i = 0; i < steps; i++)
        {
            world.Tick += 10;
            system.Update(new TickContext(world, new SimulationRandom(1), events));
        }

        return events;
    }
}
