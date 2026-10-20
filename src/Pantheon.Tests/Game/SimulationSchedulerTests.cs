using System;
using Pantheon.Game;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>The fixed-step clock decouples simulation ticks from the render frame rate.</summary>
public sealed class SimulationSchedulerTests
{
    [Fact]
    public void SimulationScheduler_RunsWholeTicksAndKeepsRemainder()
    {
        var scheduler = new SimulationScheduler(10.0, 32);
        var ticks = 0;

        Assert.Equal(2, scheduler.Advance(0.25, () => ticks++));
        Assert.Equal(0.5, scheduler.Alpha, 6);
        Assert.Equal(1, scheduler.Advance(0.05, () => ticks++));
        Assert.Equal(3, ticks);
    }

    [Fact]
    public void SimulationScheduler_IsIndependentOfFrameRate()
    {
        var fast = CountTicks(1.0 / 144.0, 10.0);
        var slow = CountTicks(1.0 / 23.0, 10.0);

        Assert.InRange(fast, 99, 100);
        Assert.InRange(slow, 99, 100);
    }

    [Fact]
    public void SimulationScheduler_PauseStopsTicksAndTime()
    {
        var scheduler = new SimulationScheduler(10.0, 32) { IsPaused = true };

        Assert.Equal(0, scheduler.Advance(5.0, () => { }));
        scheduler.IsPaused = false;
        Assert.Equal(0, scheduler.Advance(0.05, () => { }));
    }

    [Fact]
    public void SimulationScheduler_TimeScaleMultipliesTicks()
    {
        var scheduler = new SimulationScheduler(10.0, 32) { TimeScale = 4.0 };

        Assert.Equal(4, scheduler.Advance(0.1, () => { }));
        scheduler.TimeScale = 0.0;
        Assert.Equal(0, scheduler.Advance(1.0, () => { }));
    }

    [Fact]
    public void SimulationScheduler_CapsBacklogAndCountsDroppedTicks()
    {
        var scheduler = new SimulationScheduler(10.0, 5);

        Assert.Equal(5, scheduler.Advance(1.25, () => { }));
        Assert.Equal(7, scheduler.DroppedTicks);
        Assert.Equal(0.5, scheduler.Alpha, 6);
    }

    [Fact]
    public void SimulationScheduler_ResetDiscardsAccumulatedTime()
    {
        var scheduler = new SimulationScheduler(10.0, 32);
        scheduler.Advance(0.09, () => { });

        scheduler.Reset();

        Assert.Equal(0.0, scheduler.Alpha);
        Assert.Equal(0, scheduler.Advance(0.02, () => { }));
    }

    [Fact]
    public void SimulationScheduler_IgnoresInvalidFrameTimes()
    {
        var scheduler = new SimulationScheduler(10.0, 32);

        Assert.Equal(0, scheduler.Advance(-1.0, () => { }));
        Assert.Equal(0, scheduler.Advance(double.NaN, () => { }));
        Assert.Equal(0, scheduler.Advance(double.PositiveInfinity, () => { }));
    }

    [Fact]
    public void SimulationScheduler_RejectsInvalidConfiguration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationScheduler(0.0, 32));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationScheduler(10.0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationScheduler().TimeScale = -1.0);
    }

    /// <summary>Counts ticks over ten simulated seconds at a fixed frame time.</summary>
    private static int CountTicks(double frameSeconds, double totalSeconds)
    {
        var scheduler = new SimulationScheduler(10.0, 32);
        var ticks = 0;
        for (var elapsed = 0.0; elapsed < totalSeconds - 1e-9; elapsed += frameSeconds)
        {
            ticks += scheduler.Advance(frameSeconds, () => { });
        }

        return ticks;
    }

    [Fact]
    public void TheBudget_LeavesDueTicksForTheNextUpdate_ButAlwaysRunsOne()
    {
        // Each tick "takes" 2 ms of a 5 ms budget, so an update runs three and leaves the rest.
        var scheduler = new SimulationScheduler(10.0, 32) { BudgetSeconds = 0.005 };
        var spent = 0.0;
        var ticks = 0;
        void Step()
        {
            ticks++;
            spent += 0.002;
        }

        Assert.Equal(3, scheduler.Advance(1.0, Step, () => spent));
        Assert.Equal(7, scheduler.Backlog);
        Assert.Equal(1, scheduler.BudgetHits);
        Assert.Equal(0, scheduler.DroppedTicks);

        spent = 1.0;
        Assert.Equal(1, scheduler.Advance(0.001, Step, () => spent));
        spent = 0.0;
        Assert.Equal(3, scheduler.Advance(0.001, Step, () => spent));
        Assert.Equal(7, ticks);
        Assert.Equal(3, scheduler.Backlog);
    }

    [Fact]
    public void ABacklogPastItsLimit_IsDropped_AndWithoutAClockOrBudgetTheOldCapHolds()
    {
        var scheduler = new SimulationScheduler(10.0, 4) { BudgetSeconds = 0.001 };
        Assert.Equal(8, scheduler.MaxBacklogTicks);

        Assert.Equal(1, scheduler.Advance(5.0, () => { }, () => 1.0));
        Assert.Equal(8, scheduler.Backlog);
        Assert.Equal(41, scheduler.DroppedTicks);

        var unbudgeted = new SimulationScheduler(10.0, 4);
        Assert.Equal(4, unbudgeted.Advance(5.0, () => { }, () => 1.0));
        Assert.Equal(0, unbudgeted.Backlog);
        Assert.Equal(4, scheduler.Advance(0.0001, () => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.BudgetSeconds = 0.0);
    }
}
