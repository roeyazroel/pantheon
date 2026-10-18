using System;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Simulation.Randomness;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>xoshiro256** generator: reference outputs, reproducibility, state restore and distributions.</summary>
public sealed class SimulationRandomTests
{
    [Fact]
    public void SimulationRandom_MatchesXoshiro256StarStarReference()
    {
        // Reference outputs of xoshiro256** for the state {1, 2, 3, 4}.
        var random = SimulationRandom.FromState(new RandomState(1, 2, 3, 4));

        Assert.Equal(11520UL, random.NextUInt64());
        Assert.Equal(0UL, random.NextUInt64());
        Assert.Equal(1509978240UL, random.NextUInt64());
    }

    [Fact]
    public void SimulationRandom_SameSeedGivesSameSequence()
    {
        var first = new SimulationRandom(123);
        var second = new SimulationRandom(123);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(first.NextUInt64(), second.NextUInt64());
        }
    }

    [Fact]
    public void SimulationRandom_DifferentSeedsGiveDifferentSequences()
    {
        var first = new SimulationRandom(1);
        var second = new SimulationRandom(2);

        Assert.NotEqual(
            Enumerable.Range(0, 8).Select(_ => first.NextUInt64()).ToArray(),
            Enumerable.Range(0, 8).Select(_ => second.NextUInt64()).ToArray());
    }

    [Fact]
    public void SimulationRandom_RestoredStateResumesSequence()
    {
        var original = new SimulationRandom(99);
        for (var i = 0; i < 17; i++)
        {
            original.NextUInt64();
        }

        var restored = SimulationRandom.FromState(original.GetState());
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(original.NextUInt64(), restored.NextUInt64());
        }
    }

    [Fact]
    public void SimulationRandom_ZeroSeedIsValid()
    {
        var random = new SimulationRandom(0);

        Assert.NotEqual(new RandomState(0, 0, 0, 0), random.GetState());
        Assert.NotEqual(random.NextUInt64(), random.NextUInt64());
    }

    [Fact]
    public void SimulationRandom_RejectsAllZeroState()
    {
        Assert.Throws<ArgumentException>(() => SimulationRandom.FromState(new RandomState(0, 0, 0, 0)));
    }

    [Fact]
    public void SimulationRandom_NextIntStaysInBoundsAndCoversRange()
    {
        var random = new SimulationRandom(7);
        var seen = new bool[10];

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextInt(10);
            Assert.InRange(value, 0, 9);
            seen[value] = true;

            Assert.InRange(random.NextInt(-5, 5), -5, 4);
        }

        Assert.All(seen, Assert.True);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(3, 3));
    }

    [Fact]
    public void SimulationRandom_UnitIntervalSamplesAreInRange()
    {
        var random = new SimulationRandom(11);

        for (var i = 0; i < 10_000; i++)
        {
            Assert.InRange(random.NextDouble(), 0.0, 0.9999999999999999);
            Assert.InRange(random.NextFloat(), 0f, 0.99999994f);
        }
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(3.0)]
    [InlineData(250.0)]
    public void SimulationRandom_PoissonMeanMatchesLambda(double mean)
    {
        const int Samples = 20_000;
        var random = new SimulationRandom(2026);

        var total = 0L;
        for (var i = 0; i < Samples; i++)
        {
            var value = random.NextPoisson(mean);
            Assert.True(value >= 0);
            total += value;
        }

        // Within five standard errors of the mean; the fixed seed makes this deterministic.
        var tolerance = 5.0 * Math.Sqrt(mean / Samples);
        Assert.InRange(total / (double)Samples, mean - tolerance, mean + tolerance);
    }

    [Fact]
    public void SimulationRandom_PoissonOfZeroOrLessIsZero()
    {
        var random = new SimulationRandom(1);

        Assert.Equal(0, random.NextPoisson(0.0));
        Assert.Equal(0, random.NextPoisson(-2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextPoisson(double.NaN));
    }

    [Fact]
    public void SeedMixer_DerivesIndependentStableStreams()
    {
        var seed = new WorldSeed(1234);

        Assert.Equal(SeedMixer.Derive(seed, "simulation"), SeedMixer.Derive(seed, "simulation"));
        Assert.NotEqual(SeedMixer.Derive(seed, "simulation"), SeedMixer.Derive(seed, "terrain.elevation"));
        Assert.NotEqual(SeedMixer.Derive(seed, "simulation"), SeedMixer.Derive(new WorldSeed(1235), "simulation"));
    }
}
