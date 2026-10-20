using System;
using System.Collections.Generic;
using System.Linq;
using Pantheon.Game.Presentation.Animals;
using Xunit;

namespace Pantheon.Tests.Animals;

/// <summary>Feet planted on uneven ground in every pose (ADR-069): no foot sinks into the ground, and on flat ground nothing changes.</summary>
public sealed class AnimalFootingTests
{
    [Theory]
    [MemberData(nameof(Bodies))]
    public void OnFlatGround_EveryPoseIsLeftAsItIs(string name)
    {
        var b = AnimalPlans.For(name).Body;
        foreach (var pose in Poses(b))
        {
            var shift = AnimalFooting.HipShift(pose, b, default, default);
            var planted = AnimalFooting.Plant(pose, b, default, default, shift);

            Assert.Equal(0f, shift, 3);
            Assert.Equal(AnimalFooting.Ankle(b, pose.LeftLeg).Down, AnimalFooting.Ankle(b, planted.LeftLeg).Down, 3);
            Assert.Equal(AnimalFooting.Ankle(b, pose.RightLeg).Forward, AnimalFooting.Ankle(b, planted.RightLeg).Forward, 3);
            Assert.Equal(pose.LeftLeg.Upper + pose.LeftLeg.Lower + pose.LeftLeg.End, planted.LeftLeg.Upper + planted.LeftLeg.Lower + planted.LeftLeg.End, 3);
        }
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void OnUnevenGround_NoFootSinks_AndAFootWithinReachStandsOnItsGround(string name)
    {
        var b = AnimalPlans.For(name).Body;
        var legLength = b.Thigh + b.Shin;
        var reached = 0;
        var cases = 0;
        foreach (var pose in Poses(b))
        {
            foreach (var (left, right) in Grounds(legLength))
            {
                var shift = AnimalFooting.HipShift(pose, b, left, right);
                var planted = AnimalFooting.Plant(pose, b, left, right, shift);
                foreach (var (before, after, ground) in new[] { (pose.LeftLeg, planted.LeftLeg, left), (pose.RightLeg, planted.RightLeg, right) })
                {
                    // The ankle's height above its own ground: what it was above flat ground, never less.
                    var wanted = pose.HipHeight - AnimalFooting.Ankle(b, before).Down;
                    var got = planted.HipHeight - AnimalFooting.Ankle(b, after).Down - ground.Height;
                    Assert.True(got >= wanted - 0.02f || shift >= (AnimalFooting.MaxRise * legLength) - 1e-3f, $"{name}'s foot sank {wanted - got:0.000} on ground {ground.Height:0.00}");
                    cases++;
                    reached += MathF.Abs(got - wanted) < 0.02f ? 1 : 0;
                }
            }
        }

        Assert.True(reached > cases * 0.9, $"{name}'s feet stood on their ground in {reached} of {cases} cases");
    }

    [Fact]
    public void LowGroundDropsTheHips_HighGroundRaisesThem_AndTheFootLiesAlongTheSlope()
    {
        var b = AnimalPlans.Names.Select(AnimalPlans.For).First().Body;
        var legLength = b.Thigh + b.Shin;
        var stand = AnimalMotion.Stand(b, 0f);

        var downhill = AnimalFooting.HipShift(stand, b, new FootGround(-0.3f * legLength, 0f), default);
        var uphill = AnimalFooting.HipShift(stand, b, new FootGround(0.8f * legLength, 0f), default);
        var cliff = AnimalFooting.HipShift(stand, b, new FootGround(3f * legLength, 0f), default);
        var sloped = AnimalFooting.Plant(stand, b, new FootGround(0f, 0.3f), default, 0f);

        Assert.True(downhill < 0f, "the hips drop so a foot reaches low ground");
        Assert.True(uphill > 0f, "the hips rise so a foot clears high ground");
        Assert.Equal(AnimalFooting.MaxRise * legLength, cliff, 3);
        Assert.Equal(0.3f, (sloped.LeftLeg.Upper + sloped.LeftLeg.Lower + sloped.LeftLeg.End) - (stand.LeftLeg.Upper + stand.LeftLeg.Lower + stand.LeftLeg.End), 3);
        Assert.Equal(AnimalFooting.SteepestSlope, AnimalFooting.Plant(stand, b, new FootGround(0f, 2f), default, 0f).LeftLeg.End - stand.LeftLeg.End, 3);
    }

    /// <summary>The species' bodies.</summary>
    /// <returns>Their names.</returns>
    public static TheoryData<string> Bodies() => [.. AnimalPlans.Names];

    /// <summary>Every pose made for the ground: standing, a walk and a run through their strides, working, farming, uprooting, fighting, attacking and playing.</summary>
    private static IEnumerable<BodyPose> Poses(AnimalProportions b)
    {
        yield return AnimalMotion.Stand(b, 0.4f);
        for (var phase = 0f; phase < 1f; phase += 0.125f)
        {
            yield return AnimalMotion.Walk(b, phase, 0f);
            yield return AnimalMotion.Walk(b, phase, 1f);
        }

        foreach (var clock in new[] { 0f, 0.7f, 1.9f })
        {
            yield return AnimalMotion.Work(b, clock);
            yield return AnimalMotion.Farm(b, clock);
            yield return AnimalMotion.Uproot(b, clock);
            yield return AnimalMotion.Fight(b, clock, 0.3f);
            yield return AnimalMotion.Attack(b, clock);
            yield return AnimalMotion.Play(b, clock, 0.3f);
        }
    }

    /// <summary>Ground under the two feet: level, stepped, sloping and steep, up and down.</summary>
    private static IEnumerable<(FootGround Left, FootGround Right)> Grounds(float legLength)
    {
        foreach (var left in new[] { -0.35f, -0.15f, 0f, 0.1f, 0.3f, 0.6f })
        {
            foreach (var right in new[] { -0.3f, 0f, 0.2f, 0.5f })
            {
                yield return (new FootGround(left * legLength, left), new FootGround(right * legLength, -right));
            }
        }
    }
}
