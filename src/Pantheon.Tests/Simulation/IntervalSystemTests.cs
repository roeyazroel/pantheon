using System;
using System.Collections.Generic;
using Pantheon.Simulation.Systems;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The settlement model steps once every interval of ticks, decoupled from the tick rate.</summary>
public sealed class IntervalSystemTests
{
    [Fact]
    public void InnerSystem_RunsOnTheLastTickOfEachInterval()
    {
        var inner = new RecordingSystem();
        var engine = new SimulationEngine([new IntervalSystem(inner, 10)]);
        var world = SettlementWorlds.Create(population: 40, housing: 60, food: 100f, fertility: 0.5f);

        engine.Run(world, 9);
        var afterNineTicks = inner.Ticks.ToArray();
        engine.Run(world, 1);
        var afterTenTicks = inner.Ticks.ToArray();
        engine.Run(world, 25);

        Assert.Empty(afterNineTicks);
        Assert.Equal(new long[] { 9 }, afterTenTicks);
        Assert.Equal(new long[] { 9, 19, 29 }, inner.Ticks);
    }

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(9, 10, 1)]
    [InlineData(10, 10, 10)]
    [InlineData(23, 10, 7)]
    [InlineData(5, 1, 1)]
    public void TicksUntilDue_CountsToTheNextRun(long tick, int interval, int expected)
    {
        Assert.Equal(expected, IntervalSystem.TicksUntilDue(tick, interval));
        Assert.True(IntervalSystem.IsDue(tick + expected - 1, interval));
        for (var earlier = tick; earlier < tick + expected - 1; earlier++)
        {
            Assert.False(IntervalSystem.IsDue(earlier, interval));
        }
    }

    [Fact]
    public void Constructor_RejectsIntervalsBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalSystem(new RecordingSystem(), 0));
    }

    [Fact]
    public void SettlementModel_StepsOncePerStepInterval()
    {
        var rules = SettlementWorlds.NoBirthsOrDeaths;
        var world = SettlementWorlds.Create(population: 100, housing: 120, food: 0f, fertility: 0f);
        var engine = new SimulationEngine(rules);

        engine.Run(world, rules.StepIntervalTicks - 1);
        var beforeTheStep = SettlementWorlds.Settlement(world).Health;
        engine.Run(world, 1);
        var afterOneStep = SettlementWorlds.Settlement(world).Health;
        engine.Run(world, rules.StepIntervalTicks * 9);

        Assert.Equal(1f, beforeTheStep);
        Assert.Equal(1f - rules.StarvationHealthLossPerStep, afterOneStep, 6);
        Assert.Equal(1f - (10 * rules.StarvationHealthLossPerStep), SettlementWorlds.Settlement(world).Health, 5);
    }

    /// <summary>Records the ticks it ran on.</summary>
    private sealed class RecordingSystem : ISimulationSystem
    {
        /// <summary>Ticks during which the system ran.</summary>
        public List<long> Ticks { get; } = [];

        /// <inheritdoc />
        public void Update(TickContext context) => Ticks.Add(context.World.Tick);
    }
}
