using Godot;

namespace Pantheon.Client.UI;

/// <summary>
/// A short message above the miracle bar that fades away (ADR-059): "Saved", "Following Bramble",
/// a refusal. Before, such messages reached only the debug panel.
/// </summary>
internal sealed partial class StatusToast : PanelContainer
{
    private const double ShowSeconds = 3.5;
    private const double FadeSeconds = 0.8;

    private readonly Label _text;
    private double _age = double.MaxValue;

    /// <summary>Builds the toast, hidden.</summary>
    public StatusToast()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddThemeStyleboxOverride("panel", UiTheme.Panel(new Color(0.04f, 0.05f, 0.07f, 0.85f), UiTheme.Edge with { A = 0.6f }, 1, 14, margin: 8));
        _text = UiTheme.Label(this, UiTheme.SmallSize, UiTheme.Ink);
        _text.HorizontalAlignment = HorizontalAlignment.Center;
        Modulate = Colors.Transparent;
    }

    /// <summary>Shows a message.</summary>
    /// <param name="message">The message.</param>
    public void ShowMessage(string message)
    {
        _text.Text = message;
        _age = 0;
        Modulate = Colors.White;
    }

    /// <summary>Fades the message once it has been shown long enough.</summary>
    /// <param name="delta">Frame time in seconds.</param>
    public override void _Process(double delta)
    {
        if (_age > ShowSeconds + FadeSeconds)
        {
            return;
        }

        _age += delta;
        var fade = (float)Mathf.Clamp(1 - ((_age - ShowSeconds) / FadeSeconds), 0, 1);
        Modulate = new Color(1f, 1f, 1f, fade);
    }
}
