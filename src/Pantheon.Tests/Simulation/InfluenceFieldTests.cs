using Pantheon.Domain.World;
using Pantheon.Simulation.Belief;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Simulation;

/// <summary>A god's influence spreads from its followers and fades with distance (spec §10).</summary>
public sealed class InfluenceFieldTests
{
    [Fact]
    public void Influence_FadesWithDistance_AndEndsAtTheReach()
    {
        var world = DivineWorlds.Create(0f, 200f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0.25f, faith: 0.8f);
        var field = InfluenceField.Build(DivineWorlds.Rules, world, DivineWorlds.GodId);
        var source = Assert.Single(field.Sources);

        // 25 followers: reach 400 + 20 × 5 = 500 m; strength 2.5 × 0.25 × 0.8 = 0.5.
        Assert.Equal(500f, source.Reach, 3);
        Assert.Equal(0.5f, field.At(new GroundPosition(200f, 512f)), 4);
        Assert.Equal(0.25f, field.At(new GroundPosition(450f, 512f)), 4);
        Assert.Equal(0f, field.At(new GroundPosition(700f, 512f)));
    }

    [Fact]
    public void Influence_GrowsWithFollowers()
    {
        var few = DivineWorlds.Create(0f, 200f);
        var many = DivineWorlds.Create(0f, 200f);
        DivineWorlds.Believe(few.Settlements[0], adherence: 0.1f, faith: 0.8f);
        DivineWorlds.Believe(many.Settlements[0], adherence: 0.4f, faith: 0.8f);
        var point = new GroundPosition(600f, 512f);

        Assert.True(InfluenceField.Build(DivineWorlds.Rules, many, 1).At(point) > InfluenceField.Build(DivineWorlds.Rules, few, 1).At(point));
    }

    [Fact]
    public void OverlappingSources_AddUpButNeverPassOne()
    {
        var world = DivineWorlds.Create(0f, 200f, 400f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 1f, faith: 1f);
        DivineWorlds.Believe(world.Settlements[1], adherence: 1f, faith: 1f);
        var single = DivineWorlds.Create(0f, 200f);
        DivineWorlds.Believe(single.Settlements[0], adherence: 0.2f, faith: 1f);
        var pair = DivineWorlds.Create(0f, 200f, 400f);
        DivineWorlds.Believe(pair.Settlements[0], adherence: 0.2f, faith: 1f);
        DivineWorlds.Believe(pair.Settlements[1], adherence: 0.2f, faith: 1f);
        var between = new GroundPosition(300f, 512f);

        // Each full source gives 1 - 100 / 600 = 0.83 there; together 1 - 0.17^2 = 0.97, and 1 at a centre.
        Assert.Equal(0.9722f, InfluenceField.Build(DivineWorlds.Rules, world, 1).At(between), 3);
        Assert.Equal(1f, InfluenceField.Build(DivineWorlds.Rules, world, 1).At(world.Settlements[0].Position));
        Assert.True(InfluenceField.Build(DivineWorlds.Rules, pair, 1).At(between) > InfluenceField.Build(DivineWorlds.Rules, single, 1).At(between));
    }

    [Fact]
    public void GodWithoutFollowers_HasNoInfluence()
    {
        var world = DivineWorlds.Create(0f, 200f);
        DivineWorlds.Believe(world.Settlements[0], adherence: 0f, faith: 0.9f);

        var field = InfluenceField.Build(DivineWorlds.Rules, world, DivineWorlds.GodId);

        Assert.Empty(field.Sources);
        Assert.All(field.SampleRegions(world.Regions), value => Assert.Equal(0f, value));
    }
}
