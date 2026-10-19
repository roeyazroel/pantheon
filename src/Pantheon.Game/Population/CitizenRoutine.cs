using System;
using System.Collections.Generic;
using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>
/// A representative's routine as a closed-form function of the crowds' time (<see cref="CrowdClock"/>,
/// ADR-049): rest at home, walk to work, work, walk to the shrine, gather, walk home, and again.
/// Because it depends only on that time, citizens freeze while the game is paused, and they keep a
/// human pace when it runs faster. Given its village's ways (ADR-046), a figure walks round what
/// stands in its way, a little faster, so each leg still takes the time its straight line would.
/// </summary>
public static class CitizenRoutine
{
    // Breaths while sitting or standing, and prayers at the shrine, in cycles a second.
    private const float BreathsPerSecond = 0.25f;
    private const float PrayersPerSecond = 0.125f;

    /// <summary>Length of one cycle of the routine.</summary>
    /// <param name="citizen">Representative.</param>
    /// <param name="settings">Routine durations and walking speed.</param>
    /// <returns>Seconds of simulation time.</returns>
    public static float CycleSeconds(in RepresentativeCitizen citizen, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var walking = Vector2.Distance(citizen.Home, citizen.Work)
            + Vector2.Distance(citizen.Work, citizen.Gathering)
            + Vector2.Distance(citizen.Gathering, citizen.Home);
        return settings.RestSeconds + settings.WorkSeconds + settings.GatherSeconds + (walking / settings.WalkSpeed);
    }

    /// <summary>Where the routine puts a representative at a moment.</summary>
    /// <param name="citizen">Representative.</param>
    /// <param name="seconds">Simulation time in seconds.</param>
    /// <param name="settings">Routine settings.</param>
    /// <param name="ways">The ways round obstacles; straight lines when <c>null</c>.</param>
    /// <returns>Its pose on the ground.</returns>
    public static RoutinePose Evaluate(in RepresentativeCitizen citizen, double seconds, CitizenSettings settings, ICitizenWays? ways = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var cycle = CycleSeconds(citizen, settings);

        // Time into the current cycle, shifted by the citizen's phase so neighbours are out of step.
        var shifted = seconds + (citizen.Phase * (double)cycle);
        var t = (float)(shifted - (cycle * Math.Floor(shifted / cycle)));

        if (t < settings.RestSeconds)
        {
            return Stay(citizen.Home, GroundMath.YawTowards(citizen.Home, citizen.Gathering), CitizenActivity.AtHome, t * BreathsPerSecond);
        }

        t -= settings.RestSeconds;
        var toWork = Vector2.Distance(citizen.Home, citizen.Work) / settings.WalkSpeed;
        if (t < toWork)
        {
            return Walk(citizen.Home, citizen.Work, t, settings, ways?.Way(citizen.Index, CitizenLeg.ToWork, citizen.Home, citizen.Work));
        }

        t -= toWork;
        if (t < settings.WorkSeconds)
        {
            return new RoutinePose(citizen.Work, citizen.WorkYaw, WorkLean(t, settings), 0f, CitizenActivity.Working, t * settings.WorkStrokesPerSecond);
        }

        t -= settings.WorkSeconds;
        var toGathering = Vector2.Distance(citizen.Work, citizen.Gathering) / settings.WalkSpeed;
        if (t < toGathering)
        {
            return Walk(citizen.Work, citizen.Gathering, t, settings, ways?.Way(citizen.Index, CitizenLeg.ToGathering, citizen.Work, citizen.Gathering));
        }

        t -= toGathering;
        if (t < settings.GatherSeconds)
        {
            return Stay(citizen.Gathering, citizen.GatherYaw, CitizenActivity.Gathering, t * PrayersPerSecond);
        }

        // Whatever is left of the cycle is the walk home; rounding can leave it a hair long.
        return Walk(citizen.Gathering, citizen.Home, t - settings.GatherSeconds, settings, ways?.Way(citizen.Index, CitizenLeg.ToHome, citizen.Gathering, citizen.Home));
    }

    /// <summary>
    /// Height of the walking bob after walking a distance: the body rises gently over each step, as
    /// it passes over the planted foot, and is lowest as the feet change.
    /// </summary>
    /// <param name="walked">Distance walked, in metres.</param>
    /// <param name="settings">Stride length and bob height.</param>
    /// <returns>Lift above the ground, in metres.</returns>
    public static float Bounce(float walked, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.StepBounce * 0.5f * (1f - MathF.Cos(MathF.Tau * walked / settings.StrideLength));
    }

    /// <summary>The walking cycle after walking a distance: one cycle is a stride of two steps.</summary>
    /// <param name="walked">Distance walked, in metres.</param>
    /// <param name="settings">Stride length.</param>
    /// <returns>Cycles walked.</returns>
    public static float Strides(float walked, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return walked / (2f * settings.StrideLength);
    }

    /// <summary>Standing still at a spot.</summary>
    private static RoutinePose Stay(Vector2 spot, float yaw, CitizenActivity activity, float cycle = 0f) => new(spot, yaw, 0f, 0f, activity, cycle);

    /// <summary>
    /// Walking from one spot to another, <paramref name="t"/> seconds after setting off: straight at
    /// walking speed, or along a way round obstacles at the speed that keeps the straight line's time.
    /// </summary>
    private static RoutinePose Walk(Vector2 from, Vector2 to, float t, CitizenSettings settings, IReadOnlyList<Vector2>? way)
    {
        var distance = Vector2.Distance(from, to);
        if (distance <= 0f)
        {
            return Stay(to, 0f, CitizenActivity.Walking);
        }

        var cycle = Strides(t * settings.WalkSpeed, settings);

        var share = Math.Clamp(t * settings.WalkSpeed / distance, 0f, 1f);
        if (way is not { Count: > 0 })
        {
            var walked = share * distance;
            return new RoutinePose(Vector2.Lerp(from, to, share), GroundMath.YawTowards(from, to), 0f, Bounce(walked, settings), CitizenActivity.Walking, cycle);
        }

        var length = Vector2.Distance(from, way[0]);
        for (var i = 1; i < way.Count; i++)
        {
            length += Vector2.Distance(way[i - 1], way[i]);
        }

        var along = share * length;
        var start = from;
        foreach (var next in way)
        {
            var leg = Vector2.Distance(start, next);
            if (along <= leg || next == way[^1])
            {
                var ground = leg > 0f ? Vector2.Lerp(start, next, Math.Clamp(along / leg, 0f, 1f)) : next;
                return new RoutinePose(ground, GroundMath.YawTowards(start, next), 0f, Bounce(share * length, settings), CitizenActivity.Walking, Strides(share * length, settings));
            }

            along -= leg;
            start = next;
        }

        return Stay(to, 0f, CitizenActivity.Walking);
    }

    /// <summary>Forward lean of a work stroke: down and back up once per stroke.</summary>
    private static float WorkLean(float t, CitizenSettings settings) =>
        settings.WorkLean * 0.5f * (1f - MathF.Cos(MathF.Tau * settings.WorkStrokesPerSecond * t));
}
