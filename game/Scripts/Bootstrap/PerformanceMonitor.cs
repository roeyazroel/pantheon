using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using Pantheon.Game;
using Pantheon.Game.Diagnostics;
using Pantheon.Game.Population;
using Pantheon.Persistence;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Measures the client for spec §104 and §106's performance metrics (ADR-044): frame times, the
/// simulation's share of each frame and of each tick, ticks per second, memory, the engine's objects,
/// nodes and draw calls, the citizens drawn, and a save's write and read times. Only the client
/// reads the clock; the readout that shows the numbers is pure.
/// </summary>
internal sealed class PerformanceMonitor
{
    // Frames kept for the averages.
    private const int Window = 240;

    private readonly Queue<(double Frame, double Simulation, long Ticks)> _frames = new();
    private double _pendingSimulation;
    private long _pendingTicks;
    private SaveTiming? _save;

    /// <summary>Advances the session, timing the simulation.</summary>
    /// <param name="session">The session.</param>
    /// <param name="delta">Frame time in seconds.</param>
    public void Simulate(GameSession session, double delta)
    {
        var tick = session.World.Tick;
        var world = session.World;
        var clock = Stopwatch.StartNew();
        session.Update(delta);
        _pendingSimulation = clock.Elapsed.TotalMilliseconds;
        _pendingTicks = ReferenceEquals(world, session.World) ? Math.Max(0, session.World.Tick - tick) : 0;
    }

    /// <summary>Records the frame that just ran.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public void Frame(double delta)
    {
        _frames.Enqueue((delta * 1000.0, _pendingSimulation, _pendingTicks));
        while (_frames.Count > Window)
        {
            _frames.Dequeue();
        }
    }

    /// <summary>The measurements over the last frames.</summary>
    /// <param name="crowds">Each settlement's crowd, for the citizens drawn.</param>
    /// <param name="debris">Pieces of debris in the world, and those still moving.</param>
    /// <returns>The sample.</returns>
    public PerformanceSample Sample(IReadOnlyList<CrowdStatus> crowds, (int Pieces, int Moving) debris)
    {
        var frames = _frames.Count > 0 ? _frames.ToList() : [(0.0, 0.0, 0L)];
        var seconds = frames.Sum(f => f.Frame) / 1000.0;
        var ticks = frames.Sum(f => f.Ticks);
        var ticking = frames.Where(f => f.Ticks > 0).ToList();
        return new PerformanceSample
        {
            FramesPerSecond = Engine.GetFramesPerSecond(),
            FrameMilliseconds = frames.Average(f => f.Frame),
            WorstFrameMilliseconds = frames.Max(f => f.Frame),
            SimulationMilliseconds = frames.Average(f => f.Simulation),
            WorstSimulationMilliseconds = frames.Max(f => f.Simulation),
            TickMilliseconds = ticks > 0 ? ticking.Sum(f => f.Simulation) / ticks : 0.0,
            TicksPerSecond = seconds > 0.0 ? ticks / seconds : 0.0,
            ManagedMegabytes = GC.GetTotalMemory(false) / 1048576.0,
            EngineMegabytes = Performance.GetMonitor(Performance.Monitor.MemoryStatic) / 1048576.0,
            Objects = (long)Performance.GetMonitor(Performance.Monitor.ObjectCount),
            Nodes = (long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
            DrawCalls = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame),
            Primitives = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame),
            CitizensShown = crowds.Sum(c => c.Representatives),
            Debris = debris.Pieces,
            DebrisMoving = debris.Moving,
            Save = _save,
        };
    }

    /// <summary>Times writing the world as a save and reading it back, without touching the disk.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The timing, also kept for later samples.</returns>
    public SaveTiming MeasureSave(GameSession session)
    {
        var clock = Stopwatch.StartNew();
        var json = WorldSaveSerializer.Serialize(session.World);
        var write = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        WorldSaveSerializer.Deserialize(json);
        _save = new SaveTiming(json.Length / 1048576.0, write, clock.Elapsed.TotalMilliseconds);
        return _save;
    }
}
