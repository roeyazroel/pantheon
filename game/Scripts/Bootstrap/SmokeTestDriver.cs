using System;
using Godot;

namespace Pantheon.Client.Bootstrap;

/// <summary>
/// Unattended run support (<c>--quit-after-frames</c>, <c>--screenshot</c>): after a number of
/// frames it optionally saves a screenshot, prints a one-line summary and quits with exit code 0.
/// </summary>
public partial class SmokeTestDriver : Node
{
    private const string HeadlessDisplayServer = "headless";

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
