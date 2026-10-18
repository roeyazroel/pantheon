using Pantheon.Domain.World;
using Pantheon.Persistence;
using Pantheon.Simulation.Systems;
using Pantheon.Simulation.WorldGen;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>The tick loop is a pure function of the world state, including after a save and load.</summary>
public sealed class DeterminismTests
{
    private const int TicksBeforeSave = 400;
    private const int TicksAfterSave = 600;

    [Fact]
    public void ContinuingAfterLoad_MatchesUninterruptedRun()
    {
        var engine = new SimulationEngine(TestContent.BusyRules);
        var uninterrupted = WorldGenerator.Generate(new WorldSeed(2024), TestContent.SmallWorldGen).State;
        engine.Run(uninterrupted, TicksBeforeSave);

        var resumed = WorldSaveSerializer.Deserialize(WorldSaveSerializer.Serialize(uninterrupted));
        engine.Run(uninterrupted, TicksAfterSave);
        engine.Run(resumed, TicksAfterSave);

        WorldAssert.Equal(uninterrupted, resumed);
        Assert.Equal(WorldSaveSerializer.Serialize(uninterrupted), WorldSaveSerializer.Serialize(resumed));
    }

    [Fact]
    public void SameSeedAndTicks_ProduceSameState()
    {
        var engine = new SimulationEngine(TestContent.BusyRules);
        var first = WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State;
        var second = WorldGenerator.Generate(new WorldSeed(5), TestContent.SmallWorldGen).State;

        engine.Run(first, 1000);
        for (var i = 0; i < 1000; i++)
        {
            engine.Step(second);
        }

        WorldAssert.Equal(first, second);
    }

    [Fact]
    public void SettlementStep_AdvancesTheRandomState_OtherTicksOnlyTheTick()
    {
        var world = WorldGenerator.Generate(new WorldSeed(9), TestContent.SmallWorldGen).State;
        var engine = new SimulationEngine(TestContent.BusyRules);
        var before = world.Random;

        engine.Step(world);
        var afterOneTick = (world.Tick, world.Random);
        engine.Run(world, TestContent.BusyRules.StepIntervalTicks - 1);

        Assert.Equal((1L, before), afterOneTick);
        Assert.Equal(TestContent.BusyRules.StepIntervalTicks, world.Tick);
        Assert.NotEqual(before, world.Random);
    }

    [Fact]
    public void BusyRules_ActuallyChangePopulation()
    {
        var world = WorldGenerator.Generate(new WorldSeed(2024), TestContent.SmallWorldGen).State;
        var population = world.Settlements[0].Population;
        var changed = false;
        var engine = new SimulationEngine(TestContent.BusyRules);

        for (var i = 0; i < TicksBeforeSave && !changed; i++)
        {
            engine.Step(world);
            changed = world.Settlements[0].Population != population;
        }

        Assert.True(changed, "The determinism tests need births or deaths to exercise the random stream.");
    }
}
