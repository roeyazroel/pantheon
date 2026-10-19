using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Notables;
using Pantheon.Domain.Roads;
using Pantheon.Domain.World;
using Pantheon.Simulation.Notables;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Roads;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Resources;

/// <summary>Roads between villages (spec §29, ADR-033).</summary>
public sealed class RoadTests
{
    private static readonly RoadRules Rules = ResourceWorlds.Roads;

    [Fact]
    public void AVillage_PlansARoadToItsNearestNeighbourInRange_OneAtATime()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f, 950f);
        var (a, b) = (world.Settlements[0], world.Settlements[1]);
        var system = System();

        Step(system, world, 1);

        var road = Assert.Single(world.Roads);
        Assert.Equal((a.Id, b.Id, 0f), (road.FromSettlementId, road.ToSettlementId, road.Built));
        Assert.Equal((a.Position, b.Position), (road.Path[0], road.Path[^1]));
        Assert.Equal(300f, road.Length, 1);

        world.Tick = 0;
        system.Update(new TickContext(world, new SimulationRandom(1), []));
        Assert.Single(world.Roads, r => r.FromSettlementId == a.Id);
        Assert.Null(RoadModel.Plan(Rules, DivineWorlds.Create(500f, 100f, 900f), DivineWorlds.Create(500f, 100f, 900f).Settlements[0]));
    }

    [Fact]
    public void ARoad_GoesRoundALake()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        var grid = world.Regions;
        var lake = new List<int>();
        for (var row = 5; row <= 11; row++)
        {
            lake.Add((row * grid.Columns) + 7);
        }

        world.Water.Lakes.Add(new Pantheon.Domain.Water.Lake { Id = 1, Name = "Stillmere", Regions = lake, SurfaceHeight = 5f });

        var (_, path) = RoadModel.Plan(Rules, world, world.Settlements[0])!.Value;

        Assert.True(path.Count > 2);
        Assert.True(RoadModel.Length(path) > 300f * 1.2f);
        Assert.All(path, p => Assert.True(grid.TryGetRegionIdAt(p.X, p.Z, out var id) && !lake.Contains(id)));
    }

    [Fact]
    public void Builders_LayTheRoadWithStone_AndAWholeRoadIsTold()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        var a = world.Settlements[0];
        world.Settlements[1].Population = 10;
        a.Stone = 0f;
        var system = System();
        Step(system, world, 5);
        var road = Assert.Single(world.Roads);
        Assert.Equal(0f, road.Built);

        a.Stone = 100f;
        var events = Step(system, world, 10);
        var perSecond = a.Population * ResourceWorlds.Rules.WorkforceShare * Rules.WorkShare * Rules.MetresPerWorkerPerSecond;
        Assert.Equal(10f * perSecond, road.Built, 2);
        Assert.Equal(100f - (road.Built * Rules.StonePerMetre), a.Stone, 2);
        Assert.Empty(events.OfType<RoadBuilt>());

        events = Step(system, world, (int)(road.Length / perSecond) + 2);
        Assert.Equal(road.Length, road.Built);
        Assert.Equal(road.Id, Assert.Single(events.OfType<RoadBuilt>()).RoadId);
    }

    [Fact]
    public void AnOpenRoad_SpeedsWalkers_AndLeadsEitherWay()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        var road = Whole(world, new GroundPosition(450f, 640f));

        Assert.Equal(Rules.Speed, RoadModel.SpeedAt(Rules, world, new GroundPosition(450f, 630f)));
        Assert.Equal(1f, RoadModel.SpeedAt(Rules, world, new GroundPosition(450f, 512f)));
        Assert.Equal(road.Path, RoadModel.Way(Rules, world, road.FromSettlementId, road.ToSettlementId));
        Assert.Equal(Enumerable.Reverse(road.Path), RoadModel.Way(Rules, world, road.ToSettlementId, road.FromSettlementId));

        road.Built = road.Length - 1f;
        Assert.Equal(1f, RoadModel.SpeedAt(Rules, world, new GroundPosition(450f, 630f)));
        Assert.Null(RoadModel.Way(Rules, world, road.FromSettlementId, road.ToSettlementId));
    }

    [Fact]
    public void AProphet_TakesTheRoad_WhereThereIsOne()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        var bend = new GroundPosition(450f, 640f);
        Whole(world, bend);
        var prophet = NotableModel.Raise(TestContent.Shipped.DivineRules.Notables, world, NotableKind.Prophet, DivineWorlds.GodId, world.Settlements[0], anointed: false, []);

        NotableModel.SetOut(world, prophet, world.Settlements[1], Rules);

        Assert.Equal([bend, world.Settlements[1].Position], prophet.Path);
    }

    [Fact]
    public void AnOpenRoad_CarriesTrade_AndFoodToTheHungry()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        Whole(world, null);
        var (a, b) = (world.Settlements[0], world.Settlements[1]);
        (a.Food, b.Food) = (2000f, 0f);
        (a.Society.Wealth, b.Society.Wealth) = (0f, 0f);

        Step(System(), world, 10);

        Assert.Equal(10f * Rules.FoodReliefPerSecond, b.Food, 2);
        Assert.Equal(2000f - (10f * Rules.FoodReliefPerSecond), a.Food, 2);
        Assert.Equal(10f * Rules.TradeWealthPerSecond, a.Society.Wealth, 3);
        Assert.Equal(a.Society.Wealth, b.Society.Wealth);
    }

    [Fact]
    public void AFloodCutsTheRoad_AndItsVillagesMendIt()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        var road = Whole(world, null);
        world.Regions.TryGetRegionIdAt(450f, 512f, out var middle);
        world.Regions.Cells[middle].Flood = 1f;
        var system = System();

        var events = Step(system, world, (int)(Rules.CutWear / Rules.WearPerSecond) + 2);
        Assert.False(Assert.Single(events.OfType<RoadChanged>()).Open);
        Assert.False(RoadModel.IsOpen(Rules, road));

        world.Regions.Cells[middle].Flood = 0f;
        events = Step(system, world, (int)((road.Wear - Rules.CutWear) / Rules.RepairPerSecond) + 3);
        Assert.True(Assert.Single(events.OfType<RoadChanged>()).Open);
    }

    [Fact]
    public void ARoadToAVillageThatIsGone_DecaysAway()
    {
        var world = DivineWorlds.Create(500f, 300f, 600f);
        Whole(world, null);
        world.Settlements[1].Population = 0;

        Step(System(), world, (int)(1f / Rules.DecayPerSecond) + 2);

        Assert.Empty(world.Roads);
    }

    /// <summary>A whole road from the first village to the second, by a bend if one is given.</summary>
    private static RoadState Whole(WorldState world, GroundPosition? bend)
    {
        var (a, b) = (world.Settlements[0], world.Settlements[1]);
        List<GroundPosition> path = bend is { } turn ? [a.Position, turn, b.Position] : [a.Position, b.Position];
        var length = RoadModel.Length(path);
        var road = new RoadState { Id = world.NextRoadId++, FromSettlementId = a.Id, ToSettlementId = b.Id, Path = path, Length = length, Built = length };
        world.Roads.Add(road);
        return road;
    }

    /// <summary>A road system stepping once a second.</summary>
    private static RoadSystem System() => new(ResourceWorlds.Rules, 10);

    /// <summary>Runs one-second steps, gathering the events.</summary>
    private static List<WorldEvent> Step(RoadSystem system, WorldState world, int steps)
    {
        var events = new List<WorldEvent>();
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events));
            world.Tick += 10;
        }

        return events;
    }
}
