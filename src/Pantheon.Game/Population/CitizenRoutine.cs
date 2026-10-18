using System;
using System.Numerics;

namespace Pantheon.Game.Population;

/// <summary>
/// A representative's routine as a closed-form function of simulation time: rest at home, walk to
/// work, work, walk to the shrine, gather, walk home, and again. Because it depends only on the
/// time, citizens freeze while the game is paused and hurry when it runs faster.
/// </summary>
public static class CitizenRoutine
{
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
    /// <returns>Its pose on the ground.</returns>
    public static RoutinePose Evaluate(in RepresentativeCitizen citizen, double seconds, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var cycle = CycleSeconds(citizen, settings);

        // Time into the current cycle, shifted by the citizen's phase so neighbours are out of step.
        var shifted = seconds + (citizen.Phase * (double)cycle);
        var t = (float)(shifted - (cycle * Math.Floor(shifted / cycle)));

        if (t < settings.RestSeconds)
        {
            return Stay(citizen.Home, GroundMath.YawTowards(citizen.Home, citizen.Gathering), CitizenActivity.AtHome);
        }

        t -= settings.RestSeconds;
        var toWork = Vector2.Distance(citizen.Home, citizen.Work) / settings.WalkSpeed;
        if (t < toWork)
        {
            return Walk(citizen.Home, citizen.Work, t, settings);
        }

        t -= toWork;
        if (t < settings.WorkSeconds)
        {
            return new RoutinePose(citizen.Work, citizen.WorkYaw, WorkLean(t, settings), 0f, CitizenActivity.Working);
        }

        t -= settings.WorkSeconds;
        var toGathering = Vector2.Distance(citizen.Work, citizen.Gathering) / settings.WalkSpeed;
        if (t < toGathering)
        {
            return Walk(citizen.Work, citizen.Gathering, t, settings);
        }

        t -= toGathering;
        if (t < settings.GatherSeconds)
        {
            return Stay(citizen.Gathering, citizen.GatherYaw, CitizenActivity.Gathering);
        }

        // Whatever is left of the cycle is the walk home; rounding can leave it a hair long.
        return Walk(citizen.Gathering, citizen.Home, t - settings.GatherSeconds, settings);
    }

    /// <summary>Height of the walking bob after walking a distance: one hop per stride.</summary>
    /// <param name="walked">Distance walked, in metres.</param>
    /// <param name="settings">Stride length and bob height.</param>
    /// <returns>Lift above the ground, in metres.</returns>
    public static float Bounce(float walked, CitizenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.StepBounce * MathF.Abs(MathF.Sin(MathF.PI * walked / settings.StrideLength));
    }

    /// <summary>Standing still at a spot.</summary>
    private static RoutinePose Stay(Vector2 spot, float yaw, CitizenActivity activity) => new(spot, yaw, 0f, 0f, activity);

    /// <summary>Walking from one spot to another at walking speed, <paramref name="t"/> seconds after setting off.</summary>
    private static RoutinePose Walk(Vector2 from, Vector2 to, float t, CitizenSettings settings)
    {
        var distance = Vector2.Distance(from, to);
        if (distance <= 0f)
        {
            return Stay(to, 0f, CitizenActivity.Walking);
        }

        var walked = Math.Clamp(t * settings.WalkSpeed, 0f, distance);
        var ground = Vector2.Lerp(from, to, walked / distance);
        return new RoutinePose(ground, GroundMath.YawTowards(from, to), 0f, Bounce(walked, settings), CitizenActivity.Walking);
    }

    /// <summary>Forward lean of a work stroke: down and back up once per stroke.</summary>
    private static float WorkLean(float t, CitizenSettings settings) =>
        settings.WorkLean * 0.5f * (1f - MathF.Cos(MathF.Tau * settings.WorkStrokesPerSecond * t));
}
