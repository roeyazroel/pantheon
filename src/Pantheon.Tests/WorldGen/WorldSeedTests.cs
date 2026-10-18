using System;
using Pantheon.Domain.World;
using Xunit;

namespace Pantheon.Tests.WorldGen;

/// <summary>Seeds parse from numbers or text and hash stably.</summary>
public sealed class WorldSeedTests
{
    [Fact]
    public void Parse_UsesDecimalNumbersVerbatim()
    {
        Assert.Equal(1234UL, WorldSeed.Parse("1234").Value);
        Assert.Equal(ulong.MaxValue, WorldSeed.Parse("18446744073709551615").Value);
        Assert.Equal("1234", new WorldSeed(1234).ToString());
    }

    [Fact]
    public void Parse_HashesTextWithStableFnv1a()
    {
        // FNV-1a 64-bit reference values.
        Assert.Equal(0xCBF29CE484222325UL, StableHash.Fnv1a64(string.Empty));
        Assert.Equal(0xAF63DC4C8601EC8CUL, StableHash.Fnv1a64("a"));
        Assert.Equal(StableHash.Fnv1a64("first light"), WorldSeed.Parse(" first light ").Value);
        Assert.NotEqual(WorldSeed.Parse("first light"), WorldSeed.Parse("First light"));
    }

    [Fact]
    public void Parse_RejectsEmptyText()
    {
        Assert.Throws<ArgumentException>(() => WorldSeed.Parse("   "));
    }

    [Fact]
    public void ToString_RoundTripsThroughParse()
    {
        var seed = WorldSeed.Parse("a phrase seed");
        Assert.Equal(seed, WorldSeed.Parse(seed.ToString()));
    }
}
