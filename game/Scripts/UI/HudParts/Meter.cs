using Godot;

namespace Pantheon.Client.UI;

/// <summary>
/// A slim rounded meter (ADR-059): a share filled from the left, or from the middle for a value
/// between two poles, with an optional mark, such as the share that wins.
/// </summary>
internal sealed partial class Meter : Control
{
    private float _value;
    private float? _mark;
    private Color _fill = UiTheme.Gold;

    /// <summary>Builds a meter of a height.</summary>
    /// <param name="height">Its height at the design size.</param>
    /// <param name="centred">Whether it fills from the middle: a value of 0.5 shows nothing.</param>
    public Meter(float height = 8f, bool centred = false)
    {
        Centred = centred;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(0f, height);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>Whether it fills from the middle.</summary>
    public bool Centred { get; }

    /// <summary>Shows a value, a colour and a mark; it is drawn again only when they change.</summary>
    /// <param name="value">The share filled (0..1).</param>
    /// <param name="fill">The fill's colour.</param>
    /// <param name="mark">Where to draw a mark (0..1), if anywhere.</param>
    public void Show(float value, Color fill, float? mark = null)
    {
        value = Mathf.Clamp(value, 0f, 1f);
        if (Mathf.IsEqualApprox(value, _value) && fill == _fill && mark == _mark)
        {
            return;
        }

        (_value, _fill, _mark) = (value, fill, mark);
        QueueRedraw();
    }

    /// <summary>Draws the track, the fill and the mark.</summary>
    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var radius = (int)(Size.Y / 2f);
        DrawStyleBox(UiTheme.Panel(new Color(0f, 0f, 0f, 0.55f), UiTheme.Edge with { A = 0.3f }, 1, radius, margin: 0), rect);
        var from = Centred ? Mathf.Min(0.5f, _value) : 0f;
        var to = Centred ? Mathf.Max(0.5f, _value) : _value;
        if (to - from > 0.001f)
        {
            var fill = new Rect2(new Vector2(from * Size.X, 1f), new Vector2((to - from) * Size.X, Size.Y - 2f));
            DrawStyleBox(UiTheme.Panel(_fill, _fill.Lightened(0.25f), 0, radius, margin: 0), fill);
        }

        if (Centred)
        {
            DrawLine(new Vector2(Size.X / 2f, -1f), new Vector2(Size.X / 2f, Size.Y + 1f), UiTheme.Muted, 1f);
        }

        if (_mark is { } mark)
        {
            var x = mark * Size.X;
            DrawLine(new Vector2(x, -3f), new Vector2(x, Size.Y + 3f), UiTheme.Ink, 2f);
        }
    }
}
