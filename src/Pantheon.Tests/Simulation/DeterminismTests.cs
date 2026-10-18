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
    public void Step_AdvancesTickAndRandomState()
    {
        var world = WorldGenerator.Generate(new WorldSeed(9), TestContent.SmallWorldGen).State;
        var before = world.Random;

        new SimulationEngine(TestContent.BusyRules).Step(world);

        Assert.Equal(1, world.Tick);
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
