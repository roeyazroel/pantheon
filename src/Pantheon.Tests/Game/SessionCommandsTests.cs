using System;
using System.IO;
using System.Linq;
using Pantheon.Domain.World;
using Pantheon.Game;
using Pantheon.Persistence;
using Pantheon.Tests.Support;
using Xunit;

namespace Pantheon.Tests.Game;

/// <summary>Hotkey commands report their outcome and never lose the current world.</summary>
public sealed class SessionCommandsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pantheon-tests", Guid.NewGuid().ToString("N"));
    private readonly GameSession _session = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(5));

    [Fact]
    public void QuickSaveThenQuickLoad_RestoresTheSavedWorld()
    {
        var path = Path.Combine(_directory, "quicksave.json");
        _session.Update(2.0);
        var savedTick = _session.World.Tick;

        var saveStatus = SessionCommands.QuickSave(_session, path);
        var savedJson = WorldSaveSerializer.Serialize(_session.World);
        _session.Update(2.0);
        var loadStatus = SessionCommands.QuickLoad(_session, path);

        Assert.Equal($"Saved tick {savedTick} to quicksave.json", saveStatus);
        Assert.Equal($"Loaded tick {savedTick} of seed 5 from quicksave.json", loadStatus);
        Assert.Equal(savedJson, WorldSaveSerializer.Serialize(_session.World));
    }

    [Fact]
    public void QuickLoad_WithoutSave_KeepsTheWorld()
    {
        var world = _session.World;

        var status = SessionCommands.QuickLoad(_session, Path.Combine(_directory, "missing.json"));

        Assert.Equal("No save found at missing.json", status);
        Assert.Same(world, _session.World);
    }

    [Fact]
    public void QuickLoad_WithUnsupportedSave_ReportsErrorAndKeepsTheWorld()
    {
        var path = Path.Combine(_directory, "future.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"SaveVersion\": 99}");
        var world = _session.World;

        var status = SessionCommands.QuickLoad(_session, path);

        Assert.StartsWith("Load failed, current world kept:", status, StringComparison.Ordinal);
        Assert.Contains("newer build", status, StringComparison.Ordinal);
        Assert.Same(world, _session.World);
    }

    [Fact]
    public void QuickSave_ToUnwritableLocation_ReportsError()
    {
        // A file where the save directory should be makes directory creation fail.
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, string.Empty);

        var status = SessionCommands.QuickSave(_session, Path.Combine(blocker, "quicksave.json"));

        Assert.StartsWith("Save failed:", status, StringComparison.Ordinal);
    }

    [Fact]
    public void PauseAndSpeedPresets_ControlTheClock()
    {
        Assert.Equal("Simulation paused", SessionCommands.TogglePause(_session));
        Assert.True(_session.Scheduler.IsPaused);

        Assert.Equal("Simulation speed x16", SessionCommands.SetSpeedPreset(_session, 2));
        Assert.False(_session.Scheduler.IsPaused);
        Assert.Equal(16.0, _session.Scheduler.TimeScale);

        Assert.Equal("Simulation paused", SessionCommands.TogglePause(_session));
        Assert.Equal("Simulation resumed", SessionCommands.TogglePause(_session));
        Assert.Throws<ArgumentOutOfRangeException>(() => SessionCommands.SetSpeedPreset(_session, SessionCommands.SpeedPresets.Count));
    }

    [Fact]
    public void StepTick_PausesAndRunsExactlyOneTick()
    {
        var tick = _session.World.Tick;

        var status = SessionCommands.StepTick(_session);

        Assert.Equal($"Stepped to tick {tick + 1} (paused)", status);
        Assert.True(_session.Scheduler.IsPaused);
        Assert.Equal(tick + 1, _session.World.Tick);
        Assert.Equal(0, _session.Update(1.0));
        Assert.Equal(tick + 1, _session.World.Tick);
    }

    [Fact]
    public void StepTick_RepeatedMatchesScheduledTicks()
    {
        var scheduled = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(5));
        Assert.Equal(20, scheduled.Update(2.0));

        for (var i = 0; i < 20; i++)
        {
            SessionCommands.StepTick(_session);
        }

        WorldAssert.Equal(scheduled.World, _session.World);
    }

    [Fact]
    public void StepSettlement_PausesAndRunsToTheNextSettlementStep()
    {
        Assert.Equal("Settlement step: ran 10 tick(s) to tick 10 (paused)", SessionCommands.StepSettlement(_session));
        Assert.True(_session.Scheduler.IsPaused);

        SessionCommands.StepTick(_session);
        SessionCommands.StepTick(_session);

        Assert.Equal("Settlement step: ran 8 tick(s) to tick 20 (paused)", SessionCommands.StepSettlement(_session));
        Assert.Equal(0, _session.Update(1.0));
    }

    [Fact]
    public void StepSettlement_MatchesScheduledTicks()
    {
        var scheduled = GameSession.CreateNew(TestContent.SmallBusy, new WorldSeed(5));
        Assert.Equal(30, scheduled.Update(3.0));

        for (var i = 0; i < 3; i++)
        {
            SessionCommands.StepSettlement(_session);
        }

        WorldAssert.Equal(scheduled.World, _session.World);
    }

    [Fact]
    public void AdvanceTicks_RunsTheTicksNow()
    {
        Assert.Equal("Advanced 25 ticks to tick 25", SessionCommands.AdvanceTicks(_session, 25));
        Assert.Equal(25, _session.World.Tick);
    }

    [Fact]
    public void SpoilFood_EmptiesEveryStore()
    {
        var settlements = _session.World.Settlements;
        Assert.True(settlements.Count > 1);

        var status = SessionCommands.SpoilFood(_session);

        Assert.Equal("Food stores spoiled: " + string.Join(", ", settlements.Select(s => $"{s.Name} lost 120 food")), status);
        Assert.All(settlements, settlement => Assert.Equal(0f, settlement.Food));
    }

    [Fact]
    public void AddSettlers_AddsHalfThePopulation_AtLeastTheMinimum()
    {
        var settlements = _session.World.Settlements;
        foreach (var each in settlements)
        {
            each.Population = 40;
        }

        Assert.Equal("Settlers arrived: " + string.Join(", ", settlements.Select(s => $"{s.Name} +20")) .Replace(", ", " (now 60), ", StringComparison.Ordinal) + " (now 60)", SessionCommands.AddSettlers(_session));

        var settlement = settlements[0];
        settlement.Population = 4;
        Assert.StartsWith($"Settlers arrived: {settlement.Name} +{SessionCommands.MinimumSettlers} (now 14)", SessionCommands.AddSettlers(_session), StringComparison.Ordinal);
    }

    [Fact]
    public void DebugCommands_WithoutSettlements_ReportIt()
    {
        _session.World.Settlements.Clear();

        Assert.Equal("No settlement to spoil", SessionCommands.SpoilFood(_session));
        Assert.Equal("No settlement for settlers", SessionCommands.AddSettlers(_session));
    }

    [Fact]
    public void NewWorld_ReplacesTheWorld()
    {
        var status = SessionCommands.NewWorld(_session, new WorldSeed(77));

        Assert.Equal("New world from seed 77", status);
        Assert.Equal(new WorldSeed(77), _session.World.Seed);
    }

    /// <summary>Deletes the temporary save directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
