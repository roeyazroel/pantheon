using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pantheon.Domain.World;

namespace Pantheon.Game.Diagnostics;

/// <summary>How long a save took to write and read back (spec §104's save time).</summary>
/// <param name="Megabytes">Its size.</param>
/// <param name="WriteMilliseconds">Milliseconds to write it.</param>
/// <param name="ReadMilliseconds">Milliseconds to read it back.</param>
public sealed record SaveTiming(double Megabytes, double WriteMilliseconds, double ReadMilliseconds);

/// <summary>What the client measured over its last frames (spec §104, §106's performance metrics, ADR-044).</summary>
public sealed record PerformanceSample
{
    /// <summary>Frames per second.</summary>
    public required double FramesPerSecond { get; init; }

    /// <summary>The average frame, in milliseconds.</summary>
    public required double FrameMilliseconds { get; init; }

    /// <summary>The slowest frame, in milliseconds.</summary>
    public required double WorstFrameMilliseconds { get; init; }

    /// <summary>The simulation's average share of a frame, in milliseconds.</summary>
    public required double SimulationMilliseconds { get; init; }

    /// <summary>The simulation's longest frame, in milliseconds.</summary>
    public required double WorstSimulationMilliseconds { get; init; }

    /// <summary>The average tick, in milliseconds, over the frames that ran one.</summary>
    public required double TickMilliseconds { get; init; }

    /// <summary>Ticks simulated per real second.</summary>
    public required double TicksPerSecond { get; init; }

    /// <summary>The managed heap, in megabytes.</summary>
    public required double ManagedMegabytes { get; init; }

    /// <summary>The engine's memory, in megabytes.</summary>
    public required double EngineMegabytes { get; init; }

    /// <summary>Objects alive in the engine.</summary>
    public required long Objects { get; init; }

    /// <summary>Nodes in the scene.</summary>
    public required long Nodes { get; init; }

    /// <summary>Draw calls in the last frame.</summary>
    public required long DrawCalls { get; init; }

    /// <summary>Primitives drawn in the last frame.</summary>
    public required long Primitives { get; init; }

    /// <summary>Representative citizens drawn.</summary>
    public required int CitizensShown { get; init; }

    /// <summary>Pieces of debris flying or lying (ADR-048).</summary>
    public int Debris { get; init; }

    /// <summary>Pieces of debris still moving: the rest have come to rest and sleep.</summary>
    public int DebrisMoving { get; init; }

    /// <summary>The last save measured, if any.</summary>
    public SaveTiming? Save { get; init; }

    /// <summary>The simulation's CPU budget and how it kept to it (spec §105, ADR-056), if there is one.</summary>
    public BudgetStatus? Budget { get; init; }
}

/// <summary>How the simulation kept to its CPU budget (spec §105, ADR-056).</summary>
/// <param name="Milliseconds">The budget, in milliseconds a frame.</param>
/// <param name="Hits">Frames the budget cut short.</param>
/// <param name="Backlog">Ticks due and waiting now.</param>
/// <param name="Dropped">Ticks dropped since the game began: too many to catch up.</param>
public readonly record struct BudgetStatus(double Milliseconds, long Hits, int Backlog, long Dropped);

/// <summary>The F1 panel's performance lines (spec §104, §106, ADR-044): what the client measured, and what the world holds. Pure.</summary>
public static class PerformanceReadout
{
    /// <summary>Adds the measurements, when there are any, and the census of the world.</summary>
    /// <param name="lines">Overlay lines to append to.</param>
    /// <param name="snapshot">Frame data.</param>
    public static void Append(List<string> lines, DebugSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Performance is { } sample)
        {
            lines.AddRange(Describe(sample));
        }

        lines.Add(Census(snapshot.World));
    }

    /// <summary>The measurements as lines.</summary>
    /// <param name="sample">The measurements.</param>
    /// <returns>Two lines, and one each for the budget, the debris and a save measured.</returns>
    public static IReadOnlyList<string> Describe(PerformanceSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var lines = new List<string>
        {
            Format(
                "Performance: {0:0} FPS, frame {1:0.0} ms (worst {2:0.0}); simulation {3:0.00} ms a frame (worst {4:0.0}), {5:0.00} ms a tick, {6:0} ticks/s",
                sample.FramesPerSecond, sample.FrameMilliseconds, sample.WorstFrameMilliseconds, sample.SimulationMilliseconds, sample.WorstSimulationMilliseconds, sample.TickMilliseconds, sample.TicksPerSecond),
            Format(
                "  memory {0:0} MB managed, {1:0} MB engine; {2} objects, {3} nodes; {4} draw calls, {5:0.0}k primitives; {6} citizens shown",
                sample.ManagedMegabytes, sample.EngineMegabytes, sample.Objects, sample.Nodes, sample.DrawCalls, sample.Primitives / 1000.0, sample.CitizensShown),
        };
        if (sample.Budget is { } budget)
        {
            lines.Add(Format("  simulation budget {0:0.0} ms a frame: cut short {1} times, {2} ticks waiting, {3} dropped", budget.Milliseconds, budget.Hits, budget.Backlog, budget.Dropped));
        }

        if (sample.Debris > 0)
        {
            lines.Add(Format("  physics: {0} debris pieces, {1} still moving", sample.Debris, sample.DebrisMoving));
        }

        if (sample.Save is { } save)
        {
            lines.Add(Format("  save: {0:0.0} MB written in {1:0} ms, read back in {2:0} ms", save.Megabytes, save.WriteMilliseconds, save.ReadMilliseconds));
        }

        return lines;
    }

    /// <summary>What the world holds: the load the simulation carries.</summary>
    /// <param name="world">World state.</param>
    /// <returns>One line.</returns>
    public static string Census(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return Format(
            "World load: {0} settlements, {1} people, {2} gods, {3} Creatures, {4} weather cells, {5} earthquakes, {6} regions burning, {7} flooded, {8} groups on the road, {9} notables, {10} followed villagers",
            world.Settlements.Count,
            world.Settlements.Sum(s => s.Population),
            world.Gods.Count,
            world.Creatures.Count,
            world.Weather.Cells.Count,
            world.Disasters.Earthquakes.Count,
            world.Regions.Cells.Count(r => r.Fire > 0f),
            world.Regions.Cells.Count(r => r.Flood > 0f),
            world.Migration.Groups.Count,
            world.Notables.Count,
            world.Villagers.Count);
    }

    /// <summary>Invariant-culture formatting.</summary>
    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
