using System;
using Godot;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Unattended run support (<c>--quit-after-frames</c>, <c>--screenshot</c>): after a number of
/// frames it optionally saves a screenshot, prints a one-line summary with the frame times (spec
/// §104) and quits with exit code 0.
/// </summary>
public partial class SmokeTestDriver : Node
{
    private const string HeadlessDisplayServer = "headless";

    // The first frames build shaders and caches; they are left out of the frame times.
    private const int WarmUpFrames = 30;

    private readonly System.Collections.Generic.List<double> _frameTimes = [];

    private int _frameBudget;
    private int _framesSeen;
    private string? _screenshotPath;
    private Func<string> _summary = static () => string.Empty;

    /// <summary>Creates a driver.</summary>
    /// <param name="frames">Frames to run before quitting; must be positive.</param>
    /// <param name="screenshotPath">PNG path (absolute, <c>user://</c> or <c>res://</c>), or <c>null</c> for no screenshot.</param>
    /// <param name="summary">Produces the summary line printed before quitting.</param>
    /// <returns>The driver node, ready to be added to the tree.</returns>
    public static SmokeTestDriver Create(int frames, string? screenshotPath, Func<string> summary)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frames);
        ArgumentNullException.ThrowIfNull(summary);
        return new SmokeTestDriver
        {
            Name = nameof(SmokeTestDriver),
            _frameBudget = frames,
            _screenshotPath = screenshotPath,
            _summary = summary,
        };
    }

    /// <summary>Counts frames and finishes the run when the budget is used up.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        _framesSeen++;
        if (_framesSeen > WarmUpFrames)
        {
            _frameTimes.Add(delta * 1000.0);
        }

        if (_framesSeen < _frameBudget)
        {
            return;
        }

        SetProcess(false);
        if (_screenshotPath is { } path)
        {
            SaveScreenshot(path);
        }

        GD.Print($"Smoke run finished after {_framesSeen} frames: {_summary()}");
        if (_frameTimes.Count > 0)
        {
            _frameTimes.Sort();
            var average = 0.0;
            _frameTimes.ForEach(t => average += t / _frameTimes.Count);
            GD.Print(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Frame times over {_frameTimes.Count} frames: average {average:0.0} ms ({1000.0 / average:0} FPS), 95th percentile {_frameTimes[(int)(_frameTimes.Count * 0.95)]:0.0} ms, worst {_frameTimes[^1]:0.0} ms"));
        }

        GetTree().Quit();
    }

    /// <summary>Saves the most recently rendered frame as a PNG.</summary>
    private void SaveScreenshot(string path)
    {
        if (DisplayServer.GetName() == HeadlessDisplayServer)
        {
            GD.PushWarning("Screenshot skipped: the headless display server renders no frames.");
            return;
        }

        var fullPath = ProjectSettings.GlobalizePath(path);
        DirAccess.MakeDirRecursiveAbsolute(fullPath.GetBaseDir());
        var error = GetViewport().GetTexture().GetImage().SavePng(fullPath);
        if (error == Error.Ok)
        {
            GD.Print($"Screenshot saved to {fullPath}");
        }
        else
        {
            GD.PushError($"Could not save the screenshot to {fullPath}: {error}");
        }
    }
}
