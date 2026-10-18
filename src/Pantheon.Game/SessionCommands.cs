using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Pantheon.Domain.World;
using Pantheon.Persistence;

namespace Pantheon.Game;

/// <summary>
/// Player commands on a <see cref="GameSession"/>. Each returns a one-line status for the debug
/// overlay. Save and load problems are reported in the status rather than thrown, so a bad file
/// never ends the game.
/// </summary>
public static class SessionCommands
{
    /// <summary>Time scales selected by the speed hotkeys, in order.</summary>
    public static IReadOnlyList<double> SpeedPresets { get; } = [1.0, 4.0, 16.0];

    /// <summary>Saves the current world.</summary>
    /// <param name="session">Session to save.</param>
    /// <param name="path">Save file path.</param>
    /// <returns>Status line.</returns>
    public static string QuickSave(GameSession session, string path)
    {
        ArgumentNullException.ThrowIfNull(session);
        try
        {
            session.SaveTo(path);
            return Format("Saved tick {0} to {1}", session.World.Tick, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Format("Save failed: {0}", exception.Message);
        }
    }

    /// <summary>Replaces the current world with a saved one. On failure the current world is kept.</summary>
    /// <param name="session">Session to load into.</param>
    /// <param name="path">Save file path.</param>
    /// <returns>Status line.</returns>
    public static string QuickLoad(GameSession session, string path)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!File.Exists(path))
        {
            return Format("No save found at {0}", Path.GetFileName(path));
        }

        try
        {
            session.LoadFrom(path);
            return Format("Loaded tick {0} of seed {1} from {2}", session.World.Tick, session.World.Seed, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is SaveFormatException or IOException or UnauthorizedAccessException)
        {
            return Format("Load failed, current world kept: {0}", exception.Message);
        }
    }

    /// <summary>Replaces the current world with a newly generated one.</summary>
    /// <param name="session">Session to replace the world of.</param>
    /// <param name="seed">Seed of the new world.</param>
    /// <returns>Status line.</returns>
    public static string NewWorld(GameSession session, WorldSeed seed)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.StartNewWorld(seed);
        return Format("New world from seed {0}", seed);
    }

    /// <summary>Pauses a running simulation or resumes a paused one.</summary>
    /// <param name="session">Session whose clock is toggled.</param>
    /// <returns>Status line.</returns>
    public static string TogglePause(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Scheduler.IsPaused = !session.Scheduler.IsPaused;
        return session.Scheduler.IsPaused ? "Simulation paused" : "Simulation resumed";
    }

    /// <summary>Selects one of the <see cref="SpeedPresets"/> and resumes the simulation.</summary>
    /// <param name="session">Session whose clock is changed.</param>
    /// <param name="preset">Zero-based index into <see cref="SpeedPresets"/>.</param>
    /// <returns>Status line.</returns>
    public static string SetSpeedPreset(GameSession session, int preset)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(preset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(preset, SpeedPresets.Count);

        session.Scheduler.TimeScale = SpeedPresets[preset];
        session.Scheduler.IsPaused = false;
        return Format("Simulation speed x{0}", SpeedPresets[preset]);
    }

    /// <summary>Pauses the simulation, then runs exactly one tick, so its effect can be inspected.</summary>
    /// <param name="session">Session to step.</param>
    /// <returns>Status line.</returns>
    public static string StepTick(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Scheduler.IsPaused = true;
        session.StepOneTick();
        return Format("Stepped to tick {0} (paused)", session.World.Tick);
    }

    /// <summary>Invariant-culture string formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
