using System.Collections.Generic;
using System.Linq;
using Pantheon.Domain.Events;
using Pantheon.Domain.Society;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Pantheon.Simulation.Society;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Society;

/// <summary>The society step (ADR-023): learning and technology, buildings rising under a priority, and new villages founded.</summary>
public sealed class SocietySystemTests
{
    private static readonly SocietyRules Rules = SocietyWorlds.Rules;

    [Fact]
    public void Technology_GrowsWithLearning_FasterInTheFieldOfFocus_AndIsReportedAtEachLevel()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var society = world.Settlements[0].Society;
        (society.Education, society.Wealth, society.Priority) = (0.9f, 100000f, SettlementPriority.Worship);
        society.Technology.Religion = 0.999f;
        var events = new List<WorldEvent>();

        Step(world, steps: 60, events: events);

        Assert.True(society.Technology.Religion - 0.999f > 2f * society.Technology.Agriculture);
        Assert.Contains(events.OfType<TechnologyAdvanced>(), advanced => (advanced.Field, advanced.Level) == (TechnologyField.Religion, 1));
    }

    [Fact]
    public void Knowledge_SpreadsFromTheMostAdvancedNeighbour()
    {
        var world = DivineWorlds.Create(0f, 300f, 500f);
        world.Settlements[1].Society.Technology.Medicine = 4f;
        world.Settlements[0].Society.Education = 0f;

        Step(world, steps: 100);

        Assert.True(world.Settlements[0].Society.Technology.Medicine > 0.5f);
    }

    [Fact]
    public void ATemple_IsPaidFor_Built_AndRaisedToTheGodThatAskedForIt()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var village = world.Settlements[0];
        var society = village.Society;
        (society.Priority, society.PriorityGodId, society.Wealth) = (SettlementPriority.Temple, 1, 70f);
        var events = new List<WorldEvent>();

        Step(world, steps: 1, events: events);
        var paid = society.Wealth;
        Step(world, steps: 120, events: events);

        Assert.True(paid < 15f);
        Assert.Equal(BuildingKind.Temple, society.Buildings.Single().Kind);
        Assert.Equal(1, society.Buildings.Single().GodId);
        Assert.Null(society.Project);
        Assert.Equal((BuildingKind.Temple, (int?)1), events.OfType<BuildingCompleted>().Select(b => (b.Kind, b.GodId)).Single());
    }

    [Fact]
    public void Expansion_SendsSettlersToFoundAVillageOnFreeLand_WithTheirBeliefs_AndThenWaits()
    {
        var world = DivineWorlds.Create(0f, 300f);
        var home = world.Settlements[0];
        DivineWorlds.Believe(home, adherence: 0.7f, faith: 0.6f, love: 0.4f);
        home.Population = 150;
        home.Society.Priority = SettlementPriority.Expansion;
        world.Tick = 100;
        var events = new List<WorldEvent>();

        Step(world, steps: 1, events: events);
        Step(world, steps: 10, events: events);

        var founded = events.OfType<SettlementFounded>().Single();
        var village = world.Settlements.Single(s => s.Id == founded.SettlementId);
        Assert.Equal((home.Id, Rules.Expansion.Settlers), (founded.FromSettlementId, founded.Settlers));
        Assert.Equal(150 - Rules.Expansion.Settlers, home.Population);
        Assert.Equal(Rules.Expansion.Settlers, village.Population);
        Assert.Equal(0.7f, village.Beliefs.Single().Adherence);
        Assert.NotEqual(home.Name, village.Name);
        var (hc, hr) = world.Regions.GetCoordinates(home.RegionId);
        var (vc, vr) = world.Regions.GetCoordinates(village.RegionId);
        Assert.InRange(System.Math.Max(System.Math.Abs(hc - vc), System.Math.Abs(hr - vr)), Rules.Expansion.Spacing, Rules.Expansion.Range);
    }

    private static void Step(WorldState world, int steps, List<WorldEvent>? events = null)
    {
        var system = new SocietySystem(Rules, SocietyWorlds.Civic, DivineWorlds.Rules);
        for (var i = 0; i < steps; i++)
        {
            system.Update(new TickContext(world, new SimulationRandom(1), events ?? []));
            world.Tick += Rules.StepIntervalTicks;
        }
    }
}
