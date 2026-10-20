using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace Pantheon.Client.Camera;

/// <summary>
/// The left button's hold on the land (ADR-058): a press becomes a grab once the mouse moves past a
/// threshold, so a click stays a click, and the grab's last moments give the fling on release.
/// </summary>
internal sealed class LandGrab
{
    // A left press becomes a grab of the land only after the mouse moves this far.
    private const float ThresholdPixels = 5f;

    // The grab's recent motion, in seconds, that sets the speed of a fling on release.
    private const double FlingWindowSeconds = 0.08;

    private readonly Queue<(double Seconds, NVector2 Focus)> _trail = new();
    private bool _pending;
    private Vector2 _pressedAt;

    /// <summary>Whether the land is held and dragged.</summary>
    public bool Active { get; private set; }

    /// <summary>The ground point under the cursor when the button went down.</summary>
    public NVector3 Point { get; private set; }

    /// <summary>The button went down over the land, or over nothing to hold.</summary>
    /// <param name="screen">Where it went down.</param>
    /// <param name="ground">The ground under it, when it may grab there.</param>
    public void Press(Vector2 screen, NVector3? ground)
    {
        _pending = ground is not null;
        Point = ground ?? default;
        _pressedAt = screen;
        Active = false;
    }

    /// <summary>The mouse moved; once past the threshold the press becomes a grab.</summary>
    /// <param name="screen">Where the mouse is.</param>
    /// <returns>Whether the grab began now.</returns>
    public bool Move(Vector2 screen)
    {
        if (!_pending || Active || screen.DistanceTo(_pressedAt) < ThresholdPixels)
        {
            return false;
        }

        Active = true;
        _trail.Clear();
        return true;
    }

    /// <summary>Notes where the focus is while held, for the fling.</summary>
    /// <param name="focus">The camera's focus now.</param>
    public void Track(NVector2 focus)
    {
        var now = Now();
        _trail.Enqueue((now, focus));
        while (_trail.Count > 2 && now - _trail.Peek().Seconds > FlingWindowSeconds)
        {
            _trail.Dequeue();
        }
    }

    /// <summary>Lets go: the velocity to fling with, or <c>null</c> when nothing was held.</summary>
    /// <returns>The ground velocity of the grab's last moments, zero when it was held still.</returns>
    public NVector2? Release()
    {
        var wasActive = Active;
        Cancel();
        if (!wasActive)
        {
            return null;
        }

        if (_trail.Count < 2)
        {
            return NVector2.Zero;
        }

        var first = _trail.Peek();
        var last = _trail.Last();
        var seconds = last.Seconds - first.Seconds;
        return seconds > 1e-3 && Now() - last.Seconds < FlingWindowSeconds ? (last.Focus - first.Focus) / (float)seconds : NVector2.Zero;
    }

    /// <summary>Forgets the press or the grab.</summary>
    public void Cancel() => _pending = Active = false;

    /// <summary>Real seconds, from the engine's clock (client code only).</summary>
    private static double Now() => Time.GetTicksUsec() / 1e6;
}
