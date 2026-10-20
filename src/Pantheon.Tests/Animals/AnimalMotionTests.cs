using System;
using System.Linq;
using Pantheon.Game.Presentation.Animals;
using Xunit;

namespace Pantheon.Tests.Animals;

/// <summary>How the Creatures move (ADR-061): limbs that reach, feet that stay planted, and poses that keep them on the ground.</summary>
public sealed class AnimalMotionTests
{
    [Theory]
    [InlineData(0f, 1.8f)]
    [InlineData(0.6f, 1.6f)]
    [InlineData(-0.5f, 1.4f)]
    public void ALimb_ReachesItsPoint_TheKneeForwardAndTheElbowBack(float forward, float down)
    {
        var (thigh, shin) = AnimalMotion.Reach(1f, 1f, forward, down, kneeForward: true);
        var (upper, fore) = AnimalMotion.Reach(1f, 1f, forward, down, kneeForward: false);

        Assert.Equal(forward, End(1f, 1f, thigh, shin).Z, 3);
        Assert.Equal(-down, End(1f, 1f, thigh, shin).Y, 3);
        Assert.Equal(forward, End(1f, 1f, upper, fore).Z, 3);
        Assert.True(shin < 0f && fore > 0f, "a knee bends forward and an elbow back");
        Assert.True(MathF.Sin(thigh) > forward / 2f, "the knee lies ahead of the line to the foot");
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void AWalkingCreaturesPlantedFoot_StaysWhereItTouchedDown(string name)
    {
        var b = AnimalPlans.For(name).Body;
        foreach (var pace in new[] { 0f, 0.5f, 1f })
        {
            var stride = AnimalMotion.Stride(b, pace);
            var duty = AnimalMotion.Duty(pace);
            var planted = Enumerable.Range(1, 19).Select(i => duty * i / 20f).Select(phase =>
            {
                var pose = AnimalMotion.Walk(b, phase, pace);
                var foot = End(b.Thigh, b.Shin, pose.LeftLeg.Upper, pose.LeftLeg.Lower);
                return (World: (phase * stride) + foot.Z, Height: pose.HipHeight + foot.Y);
            }).ToList();

            Assert.True(planted.Max(p => p.World) - planted.Min(p => p.World) < stride * 0.02f, $"{name}'s planted foot slides at pace {pace}");
            Assert.All(planted, p => Assert.Equal(b.Ankle, p.Height, 2));
        }
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void ASwingingFoot_LiftsAndComesDownAhead(string name)
    {
        var b = AnimalPlans.For(name).Body;
        var duty = AnimalMotion.Duty(0f);

        var lifts = Enumerable.Range(1, 9).Select(i => AnimalMotion.Foot(b, duty + ((1f - duty) * i / 10f), 0f)).ToList();

        Assert.All(lifts, f => Assert.True(f.Lift > 0f));
        Assert.True(AnimalMotion.Foot(b, 0.999f, 0f).Forward > AnimalMotion.Foot(b, duty, 0f).Forward, "it swings forward");
        Assert.Equal(AnimalMotion.Foot(b, 0f, 0f).Forward, AnimalMotion.Foot(b, 1f, 0f).Forward, 3);
    }

    [Theory]
    [MemberData(nameof(Bodies))]
    public void StandingWorkingAndFighting_KeepTheFeetOnTheGround_AndSittingBringsTheHipsDown(string name)
    {
        var b = AnimalPlans.For(name).Body;
        foreach (var pose in new[] { AnimalMotion.Stand(b, 1.3f), AnimalMotion.Work(b, 0.7f), AnimalMotion.Fight(b, 2.1f, 0.4f), AnimalMotion.Attack(b, 0.2f) })
        {
            Assert.Equal(b.Ankle, pose.HipHeight + End(b.Thigh, b.Shin, pose.LeftLeg.Upper, pose.LeftLeg.Lower).Y, 2);
            Assert.Equal(b.Ankle, pose.HipHeight + End(b.Thigh, b.Shin, pose.RightLeg.Upper, pose.RightLeg.Lower).Y, 2);
        }

        Assert.True(AnimalMotion.Sit(b, 0f).HipHeight < b.HipHeight * 0.45f, "it sits on the ground");
        Assert.True(AnimalMotion.Play(b, 0.45f, 0f).HipHeight > b.HipHeight, "it hops in play");
    }

    [Fact]
    public void Poses_BlendSmoothly()
    {
        var b = AnimalPlans.For("Cow").Body;
        var stand = AnimalMotion.Stand(b, 0f);
        var sit = AnimalMotion.Sit(b, 0f);

        var half = BodyPose.Lerp(stand, sit, 0.5f);

        Assert.Equal((stand.HipHeight + sit.HipHeight) / 2f, half.HipHeight, 4);
        Assert.Equal((stand.LeftLeg.Upper + sit.LeftLeg.Upper) / 2f, half.LeftLeg.Upper, 4);
        Assert.Equal(stand, BodyPose.Lerp(stand, sit, 0f));
    }

    public static TheoryData<string> Bodies() => [.. AnimalPlans.Names];

    /// <summary>Where a two-bone limb ends, below and ahead of its joint, by its angles (forward positive).</summary>
    private static (float Z, float Y) End(float upper, float lower, float a, float b) =>
        ((upper * MathF.Sin(a)) + (lower * MathF.Sin(a + b)), -(upper * MathF.Cos(a)) - (lower * MathF.Cos(a + b)));
}
